using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenWigi.Sdk;

namespace WigiDash.StreamDeckMirrorWidget
{
    /// <summary>
    /// Uma instância do espelho: captura a janela do Virtual Device do Stream Deck e entrega quadros ao
    /// OpenWigiDashManager; toques viram cliques na janela.
    /// </summary>
    /// <remarks>
    /// Gestos (vêm prontos do host):
    /// tap no conteúdo = clique esquerdo; long-press no conteúdo = clique direito, com a barra do rodapé
    /// enchendo enquanto o dedo segura; tap no rodapé = mostra/esconde a janela original; double tap
    /// nas faixas pretas = mostra/esconde (se ligado nas configurações).
    /// </remarks>
    public sealed class StreamDeckMirrorWidgetInstance : IWidgetInstance
    {
        private const int MaxRetryCount = 3;

        private readonly IWidgetContext _context;
        private readonly CaptureDiagnostics _diagnostics;
        private readonly object _bitmapLock = new object();
        private readonly SemaphoreSlim _settingsLoaded = new SemaphoreSlim(1, 1);

        private WindowCapture _windowCapture = new WindowCapture();
        private DeviceIdentifier _savedDevice;
        private WidgetState _currentState = WidgetState.NotConfigured;
        private Bitmap _currentBitmap;
        private AspectRatioInfo _aspectInfo;
        private CancellationTokenSource _capturing;
        private bool _loaded;
        private float _pressProgress;

        // Configurações
        private int _refreshIntervalMs = 100; // 10 quadros por segundo
        private bool _hideOriginalWindow;
        private bool _showFooterBar = true;
        private bool _doubleTapLetterboxEnabled = true;
        private Color _backgroundColor = Color.Black;

        private int _retryCount;
        private DateTime _lastRetryTime = DateTime.MinValue;

        public StreamDeckMirrorWidgetInstance(IWidgetContext context, string resourcePath)
        {
            _context = context;
            _diagnostics = new CaptureDiagnostics(context.InstanceId, resourcePath);
        }

        public Type SettingsComponent => typeof(StreamDeckMirrorSettings);

        /// <summary>Mudou o estado ou a configuração; a tela de configurações escuta para se atualizar.</summary>
        public event Action Changed;

        private PixelSize Size => _context.PixelSize;

        #region Ciclo de vida

        public async ValueTask<WidgetFrame> RenderAsync(RenderReason reason, CancellationToken cancellationToken)
        {
            await EnsureLoadedAsync(cancellationToken);
            if (_currentState != WidgetState.Connected || _context.IsAsleep || !CaptureAndCompose())
                ComposeState();

            lock (_bitmapLock)
                return Frames.FromBitmap(_currentBitmap);
        }

        public ValueTask OnWakeAsync(CancellationToken cancellationToken)
        {
            StartCapture();
            return ValueTask.CompletedTask;
        }

        public ValueTask OnSleepAsync(CancellationToken cancellationToken)
        {
            StopCapture();
            return ValueTask.CompletedTask;
        }

        public ValueTask OnSizeChangedAsync(PixelSize pixelSize, CancellationToken cancellationToken)
        {
            // A moldura da página ou a linha mudou: o encaixe da captura e o mapa de toques são do tamanho
            // antigo. Zera para recalcular no próximo quadro; o host pede RenderAsync(SizeChanged) em seguida.
            _aspectInfo = null;
            return ValueTask.CompletedTask;
        }

        public ValueTask OnFullscreenChangedAsync(bool fullscreen, CancellationToken cancellationToken)
        {
            _aspectInfo = null;
            return ValueTask.CompletedTask;
        }

        public ValueTask OnRemovedAsync(CancellationToken cancellationToken)
        {
            // Tirado do layout: devolve a janela do Stream Deck ao normal.
            _windowCapture?.RestoreWindow();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            StopCapture();
            _windowCapture?.Dispose(); // restaura a janela se estiver escondida
            _windowCapture = null;
            lock (_bitmapLock)
            {
                _currentBitmap?.Dispose();
                _currentBitmap = null;
            }
            return ValueTask.CompletedTask;
        }

        private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
        {
            if (_loaded)
                return;
            await _settingsLoaded.WaitAsync(cancellationToken);
            try
            {
                if (_loaded)
                    return;
                await LoadSettingsAsync(cancellationToken);
                if (_savedDevice is { IsValid: true })
                    TryConnectToDevice();
                _loaded = true;
            }
            finally
            {
                _settingsLoaded.Release();
            }
        }

        #endregion

        #region Captura

        private void StartCapture()
        {
            StopCapture();
            _capturing = CancellationTokenSource.CreateLinkedTokenSource(_context.Stopping);
            var token = _capturing.Token;
            _ = Task.Run(() => CaptureLoopAsync(token), token);
        }

        private void StopCapture()
        {
            _capturing?.Cancel();
            _capturing = null;
        }

        private async Task CaptureLoopAsync(CancellationToken cancellationToken)
        {
            await EnsureLoadedAsync(cancellationToken);
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    UpdateState();
                    if (_currentState == WidgetState.Connected)
                    {
                        if (CaptureAndCompose())
                            await PresentAsync(cancellationToken);
                    }
                    else
                    {
                        ComposeState();
                        await PresentAsync(cancellationToken);
                        if (ShouldAttemptReconnect())
                            TryConnectToDevice();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _context.Logger.LogError(ex, "Erro no laço de captura");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(_refreshIntervalMs), _context.Host.Time, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private void TryConnectToDevice()
        {
            if (!StreamDeckWindowFinder.IsStreamDeckRunning())
            {
                SetState(WidgetState.AppNotRunning);
                return;
            }

            var hwnd = StreamDeckWindowFinder.FindSavedDevice(_savedDevice);
            if (hwnd == IntPtr.Zero)
            {
                SetState(WidgetState.DeviceNotFound);
                return;
            }

            _windowCapture.TargetHwnd = hwnd;
            if (_hideOriginalWindow)
                _windowCapture.HideWindow();
            _retryCount = 0;
            SetState(WidgetState.Connected);
        }

        private void UpdateState()
        {
            if (!StreamDeckWindowFinder.IsStreamDeckRunning())
            {
                if (_currentState != WidgetState.AppNotRunning)
                {
                    _windowCapture.TargetHwnd = IntPtr.Zero;
                    SetState(WidgetState.AppNotRunning);
                }
                return;
            }

            if (_savedDevice is not { IsValid: true })
            {
                SetState(WidgetState.NotConfigured);
                return;
            }

            if (!_windowCapture.IsWindowValid() && _currentState == WidgetState.Connected)
                SetState(WidgetState.DeviceNotFound);
        }

        private bool ShouldAttemptReconnect()
        {
            if (_currentState == WidgetState.NotConfigured)
                return false;
            if ((DateTime.Now - _lastRetryTime).TotalSeconds < 2)
                return false;
            _lastRetryTime = DateTime.Now;
            return true;
        }

        /// <summary>Captura a janela e monta o quadro do widget. False se a captura falhou.</summary>
        private bool CaptureAndCompose()
        {
            using var captured = _windowCapture.Capture();
            if (captured == null)
            {
                if (++_retryCount >= MaxRetryCount)
                {
                    _retryCount = 0;
                    SetState(WidgetState.CaptureError);
                    ComposeState();
                    return true;
                }
                return false;
            }
            _retryCount = 0;

            var size = Size;
            var contentHeight = _showFooterBar ? AspectRatioHelper.GetFooterY(size.Height) : size.Height;
            // Cópia local: OnSizeChangedAsync pode zerar o campo enquanto o laço de captura roda.
            var aspect = _aspectInfo;
            if (aspect == null ||
                aspect.SourceWidth != captured.Width || aspect.SourceHeight != captured.Height ||
                aspect.ContentAreaWidth != size.Width || aspect.ContentAreaHeight != contentHeight)
            {
                aspect = AspectRatioHelper.Calculate(captured.Width, captured.Height, size.Width, size.Height, _showFooterBar);
                _aspectInfo = aspect;
            }

            var output = new Bitmap(size.Width, size.Height);
            using (var g = Graphics.FromImage(output))
            {
                g.Clear(_backgroundColor);
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.DrawImage(captured, aspect.RenderX, aspect.RenderY, aspect.RenderWidth, aspect.RenderHeight);
                if (_showFooterBar)
                    FooterBarRenderer.Render(g, size.Width, size.Height, _windowCapture.IsWindowHidden, _backgroundColor, _pressProgress);
            }

            _diagnostics.Record(_windowCapture.TargetHwnd, captured, output, aspect, _windowCapture.IsWindowHidden,
                message => _context.Logger.LogInformation("{Message}", message));
            Replace(output);
            return true;
        }

        private void ComposeState()
        {
            var size = Size;
            Replace(StateRenderer.CreateStateBitmap(_currentState, size.Width, size.Height, _showFooterBar, _backgroundColor,
                _windowCapture?.IsWindowHidden == true, _savedDevice?.Title, MaxRetryCount - _retryCount));
        }

        private void Replace(Bitmap bitmap)
        {
            lock (_bitmapLock)
            {
                _currentBitmap?.Dispose();
                _currentBitmap = bitmap;
            }
        }

        private ValueTask PresentAsync(CancellationToken cancellationToken)
        {
            WidgetFrame frame;
            lock (_bitmapLock)
            {
                if (_currentBitmap == null)
                    return ValueTask.CompletedTask;
                frame = Frames.FromBitmap(_currentBitmap);
            }
            return _context.PresentAsync(frame, cancellationToken);
        }

        /// <summary>Redesenha agora (fora do ritmo da captura), para o toque ter resposta imediata.</summary>
        private async ValueTask RefreshNowAsync(CancellationToken cancellationToken)
        {
            if (_context.IsAsleep)
                return;
            if (_currentState != WidgetState.Connected || !CaptureAndCompose())
                ComposeState();
            await PresentAsync(cancellationToken);
        }

        private void SetState(WidgetState state)
        {
            if (_currentState == state)
                return;
            _currentState = state;
            Changed?.Invoke();
        }

        #endregion

        #region Toque

        public async ValueTask OnTouchAsync(TouchEvent touch, CancellationToken cancellationToken)
        {
            var size = Size;
            var inFooter = _showFooterBar && AspectRatioHelper.IsClickInFooter(touch.Y, size.Height, true);

            switch (touch.Kind)
            {
                case TouchKind.Tap when inFooter:
                    await ToggleOriginalWindowAsync(cancellationToken);
                    return;

                case TouchKind.PressProgress when !inFooter && _showFooterBar:
                    _pressProgress = (float)touch.Progress;
                    await RefreshNowAsync(cancellationToken);
                    return;

                case TouchKind.PressEnded when _pressProgress > 0:
                    _pressProgress = 0;
                    await RefreshNowAsync(cancellationToken);
                    return;

                case TouchKind.Tap:
                case TouchKind.LongPress:
                {
                    if (touch.Kind == TouchKind.LongPress)
                        _pressProgress = 0;
                    var aspect = _aspectInfo;
                    if (_currentState != WidgetState.Connected || aspect == null)
                        return;
                    var source = AspectRatioHelper.MapClickToSource(touch.X, touch.Y, aspect, _showFooterBar, size.Height);
                    if (source.HasValue)
                    {
                        // Long-press = clique direito no Stream Deck, como na versão anterior.
                        _windowCapture.SendClick(source.Value.x, source.Value.y, touch.Kind == TouchKind.LongPress,
                            _diagnostics.Enabled ? message => _context.Logger.LogInformation("{Message}", message) : null);
                    }
                    if (touch.Kind == TouchKind.LongPress)
                        await RefreshNowAsync(cancellationToken);
                    return;
                }

                case TouchKind.DoubleTap when !inFooter && _doubleTapLetterboxEnabled:
                    // Só nas faixas pretas: dentro do conteúdo o double tap já virou dois cliques.
                    if (_aspectInfo != null &&
                        AspectRatioHelper.MapClickToSource(touch.X, touch.Y, _aspectInfo, _showFooterBar, size.Height) == null)
                    {
                        await ToggleOriginalWindowAsync(cancellationToken);
                    }
                    return;
            }
        }

        private async ValueTask ToggleOriginalWindowAsync(CancellationToken cancellationToken)
        {
            _windowCapture.ToggleVisibility();
            _hideOriginalWindow = _windowCapture.IsWindowHidden;
            await SaveSettingsAsync(cancellationToken);
            await RefreshNowAsync(cancellationToken);
            Changed?.Invoke();
        }

        #endregion

        #region Configurações

        private async Task LoadSettingsAsync(CancellationToken cancellationToken)
        {
            var settings = _context.Settings;
            var title = await settings.GetAsync("device_title", cancellationToken);
            var width = ParseInt(await settings.GetAsync("device_width", cancellationToken));
            var height = ParseInt(await settings.GetAsync("device_height", cancellationToken));
            if (!string.IsNullOrEmpty(title) && width > 0 && height > 0)
            {
                _savedDevice = new DeviceIdentifier
                {
                    Title = title,
                    Width = width,
                    Height = height,
                    Index = ParseInt(await settings.GetAsync("device_index", cancellationToken)),
                    ClassName = await settings.GetAsync("device_class", cancellationToken),
                };
            }

            var refresh = ParseInt(await settings.GetAsync("refreshInterval", cancellationToken));
            if (refresh > 0)
                _refreshIntervalMs = Math.Clamp(refresh, 50, 1000);
            _hideOriginalWindow = await settings.GetAsync("hideOriginalWindow", cancellationToken) == "true";
            _showFooterBar = await settings.GetAsync("showFooterBar", cancellationToken) != "false";
            _doubleTapLetterboxEnabled = await settings.GetAsync("doubleClickLetterbox", cancellationToken) != "false";
            if (await settings.GetAsync("backgroundColor", cancellationToken) is { Length: > 0 } color)
            {
                try
                {
                    _backgroundColor = ColorTranslator.FromHtml(color);
                }
                catch (Exception)
                {
                    // Cor inválida gravada: fica o preto padrão.
                }
            }
        }

        private async Task SaveSettingsAsync(CancellationToken cancellationToken = default)
        {
            var settings = _context.Settings;
            if (_savedDevice != null)
            {
                // A janela escolhida é desta máquina: não vai junto ao duplicar o widget.
                await settings.SetAsync("device_title", _savedDevice.Title ?? "", false, cancellationToken);
                await settings.SetAsync("device_width", _savedDevice.Width.ToString(), false, cancellationToken);
                await settings.SetAsync("device_height", _savedDevice.Height.ToString(), false, cancellationToken);
                await settings.SetAsync("device_index", _savedDevice.Index.ToString(), false, cancellationToken);
                await settings.SetAsync("device_class", _savedDevice.ClassName ?? "", false, cancellationToken);
            }
            await settings.SetAsync("refreshInterval", _refreshIntervalMs.ToString(), true, cancellationToken);
            await settings.SetAsync("hideOriginalWindow", _hideOriginalWindow ? "true" : "false", true, cancellationToken);
            await settings.SetAsync("showFooterBar", _showFooterBar ? "true" : "false", true, cancellationToken);
            await settings.SetAsync("doubleClickLetterbox", _doubleTapLetterboxEnabled ? "true" : "false", true, cancellationToken);
            await settings.SetAsync("backgroundColor", ColorTranslator.ToHtml(_backgroundColor), true, cancellationToken);
        }

        private static int ParseInt(string text) => int.TryParse(text, out var value) ? value : 0;

        #endregion

        #region Para a tela de configurações

        public DeviceIdentifier SavedDevice => _savedDevice;
        public WidgetState CurrentState => _currentState;
        public bool IsWindowHidden => _windowCapture?.IsWindowHidden == true;
        public int RefreshInterval => _refreshIntervalMs;
        public bool HideOriginalWindow => _hideOriginalWindow;
        public bool ShowFooterBar => _showFooterBar;
        public bool DoubleTapLetterboxEnabled => _doubleTapLetterboxEnabled;
        public Color BackgroundColor => _backgroundColor;

        public async Task SetDeviceAsync(StreamDeckWindowInfo window, int index)
        {
            _savedDevice = DeviceIdentifier.FromWindowInfo(window, index);
            _windowCapture.TargetHwnd = window.Hwnd;
            if (_hideOriginalWindow)
                _windowCapture.HideWindow();
            _aspectInfo = null;
            SetState(WidgetState.Connected);
            await SaveSettingsAsync();
            _context.RequestRender();
        }

        public async Task SetRefreshIntervalAsync(int ms)
        {
            _refreshIntervalMs = Math.Clamp(ms, 50, 1000);
            await SaveSettingsAsync();
        }

        public async Task SetHideOriginalWindowAsync(bool hide)
        {
            _hideOriginalWindow = hide;
            if (hide)
                _windowCapture.HideWindow();
            else
                _windowCapture.RestoreWindow();
            await SaveSettingsAsync();
            _context.RequestRender();
            Changed?.Invoke();
        }

        public async Task SetShowFooterBarAsync(bool show)
        {
            _showFooterBar = show;
            _aspectInfo = null;
            await SaveSettingsAsync();
            _context.RequestRender();
        }

        public async Task SetDoubleTapLetterboxAsync(bool enabled)
        {
            _doubleTapLetterboxEnabled = enabled;
            await SaveSettingsAsync();
        }

        public async Task SetBackgroundColorAsync(Color color)
        {
            _backgroundColor = color;
            await SaveSettingsAsync();
            _context.RequestRender();
        }

        #endregion
    }
}
