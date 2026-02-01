using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Controls;
using WigiDashWidgetFramework;
using WigiDashWidgetFramework.WidgetUtility;

namespace WigiDash.StreamDeckMirrorWidget
{
    /// <summary>
    /// Represents a single instance of the Stream Deck Mirror widget.
    /// Captures and displays Stream Deck Virtual Device content.
    /// </summary>
    public class StreamDeckMirrorWidgetInstance : IWidgetInstance, IWidgetInstanceWithRemoval
    {
        #region Fields

        private readonly StreamDeckMirrorWidgetObject _widgetObject;
        private readonly string _resourcePath;

        private WindowCapture _windowCapture;
        private DeviceIdentifier _savedDevice;
        private WidgetState _currentState = WidgetState.NotConfigured;

        private Thread _captureThread;
        private volatile bool _isRunning;
        private volatile bool _isPaused;

        private Bitmap _currentBitmap;
        private readonly object _bitmapLock = new object();

        private AspectRatioInfo _aspectInfo;

        private bool _isDisposed;

        // Settings
        private int _refreshIntervalMs = 100; // 100ms = 10 FPS
        private bool _hideOriginalWindow = false;
        private bool _showFooterBar = true;
        private int _longPressDurationMs = 600;
        private Color _backgroundColor = Color.Black;

        // Retry tracking
        private int _retryCount = 0;
        private const int MAX_RETRY_COUNT = 3;
        private DateTime _lastRetryTime = DateTime.MinValue;

        #endregion

        #region IWidgetInstance Properties

        public IWidgetObject WidgetObject { get; set; }
        public Guid Guid { get; set; }
        public WidgetSize WidgetSize { get; set; }
        public event WidgetUpdatedEventHandler WidgetUpdated;

        #endregion

        #region Constructor

        public StreamDeckMirrorWidgetInstance(
            StreamDeckMirrorWidgetObject widgetObject,
            WidgetSize widgetSize,
            Guid instanceGuid,
            string resourcePath)
        {
            _widgetObject = widgetObject;
            WidgetObject = widgetObject;
            WidgetSize = widgetSize;
            Guid = instanceGuid;
            _resourcePath = resourcePath;

            _windowCapture = new WindowCapture();

            Initialize();
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            try
            {
                Log("Initializing Stream Deck Mirror Widget...");

                // Load saved settings
                LoadSettings();

                // Try to find saved device
                if (_savedDevice != null && _savedDevice.IsValid)
                {
                    TryConnectToDevice();
                }
                else
                {
                    _currentState = WidgetState.NotConfigured;
                }

                // Start capture thread
                StartCaptureThread();

                // Initial render
                RenderCurrentState();

                Log("Initialization complete.");
            }
            catch (Exception ex)
            {
                LogError($"Failed to initialize: {ex.Message}");
                _currentState = WidgetState.NotConfigured;
                RenderCurrentState();
            }
        }

        private void TryConnectToDevice()
        {
            if (!StreamDeckWindowFinder.IsStreamDeckRunning())
            {
                _currentState = WidgetState.AppNotRunning;
                return;
            }

            IntPtr hwnd = StreamDeckWindowFinder.FindSavedDevice(_savedDevice);

            if (hwnd == IntPtr.Zero)
            {
                _currentState = WidgetState.DeviceNotFound;
                return;
            }

            _windowCapture.TargetHwnd = hwnd;

            // Apply hide setting if enabled
            if (_hideOriginalWindow)
            {
                _windowCapture.HideWindow();
            }

            _currentState = WidgetState.Connected;
            _retryCount = 0;
        }

        #endregion

        #region Settings

        private void LoadSettings()
        {
            var manager = _widgetObject.WidgetManager;
            if (manager == null) return;

            // Device identification
            string deviceTitle = null, deviceClass = null;
            int deviceWidth = 0, deviceHeight = 0, deviceIndex = 0;

            if (manager.LoadSetting(this, "device_title", out string title))
                deviceTitle = title;
            if (manager.LoadSetting(this, "device_width", out string w) && int.TryParse(w, out int width))
                deviceWidth = width;
            if (manager.LoadSetting(this, "device_height", out string h) && int.TryParse(h, out int height))
                deviceHeight = height;
            if (manager.LoadSetting(this, "device_index", out string idx) && int.TryParse(idx, out int index))
                deviceIndex = index;
            if (manager.LoadSetting(this, "device_class", out string cls))
                deviceClass = cls;

            if (!string.IsNullOrEmpty(deviceTitle) && deviceWidth > 0 && deviceHeight > 0)
            {
                _savedDevice = new DeviceIdentifier
                {
                    Title = deviceTitle,
                    Width = deviceWidth,
                    Height = deviceHeight,
                    Index = deviceIndex,
                    ClassName = deviceClass
                };
            }

            // Other settings
            if (manager.LoadSetting(this, "refreshInterval", out string interval) &&
                int.TryParse(interval, out int ms))
            {
                _refreshIntervalMs = Math.Max(50, Math.Min(1000, ms));
            }

            if (manager.LoadSetting(this, "hideOriginalWindow", out string hide))
            {
                _hideOriginalWindow = hide == "true";
            }

            if (manager.LoadSetting(this, "showFooterBar", out string footer))
            {
                _showFooterBar = footer != "false"; // default true
            }

            if (manager.LoadSetting(this, "longPressDuration", out string lpd) &&
                int.TryParse(lpd, out int duration))
            {
                _longPressDurationMs = Math.Max(400, Math.Min(1000, duration));
            }

            if (manager.LoadSetting(this, "backgroundColor", out string bgColor))
            {
                try
                {
                    _backgroundColor = ColorTranslator.FromHtml(bgColor);
                }
                catch { }
            }
        }

        public void SaveSettings()
        {
            var manager = _widgetObject.WidgetManager;
            if (manager == null) return;

            // Device identification
            if (_savedDevice != null)
            {
                manager.StoreSetting(this, "device_title", _savedDevice.Title ?? "");
                manager.StoreSetting(this, "device_width", _savedDevice.Width.ToString());
                manager.StoreSetting(this, "device_height", _savedDevice.Height.ToString());
                manager.StoreSetting(this, "device_index", _savedDevice.Index.ToString());
                manager.StoreSetting(this, "device_class", _savedDevice.ClassName ?? "");
            }

            // Other settings
            manager.StoreSetting(this, "refreshInterval", _refreshIntervalMs.ToString());
            manager.StoreSetting(this, "hideOriginalWindow", _hideOriginalWindow ? "true" : "false");
            manager.StoreSetting(this, "showFooterBar", _showFooterBar ? "true" : "false");
            manager.StoreSetting(this, "longPressDuration", _longPressDurationMs.ToString());
            manager.StoreSetting(this, "backgroundColor", ColorTranslator.ToHtml(_backgroundColor));
        }

        #endregion

        #region Capture Thread

        private void StartCaptureThread()
        {
            _isRunning = true;
            _captureThread = new Thread(CaptureLoop)
            {
                IsBackground = true,
                Name = $"StreamDeckMirror_{Guid}"
            };
            _captureThread.Start();
        }

        private void CaptureLoop()
        {
            while (_isRunning)
            {
                try
                {
                    if (!_isPaused)
                    {
                        UpdateState();

                        if (_currentState == WidgetState.Connected)
                        {
                            CaptureAndRender();
                        }
                        else
                        {
                            // Render state screen and try to reconnect periodically
                            RenderCurrentState();

                            if (ShouldAttemptReconnect())
                            {
                                TryConnectToDevice();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Capture loop error: {ex.Message}");
                }

                Thread.Sleep(_refreshIntervalMs);
            }
        }

        private void UpdateState()
        {
            // Check if app is running
            if (!StreamDeckWindowFinder.IsStreamDeckRunning())
            {
                if (_currentState != WidgetState.AppNotRunning)
                {
                    _currentState = WidgetState.AppNotRunning;
                    _windowCapture.TargetHwnd = IntPtr.Zero;
                }
                return;
            }

            // Check if we have a configured device
            if (_savedDevice == null || !_savedDevice.IsValid)
            {
                _currentState = WidgetState.NotConfigured;
                return;
            }

            // Check if window is still valid
            if (!_windowCapture.IsWindowValid())
            {
                if (_currentState == WidgetState.Connected)
                {
                    _currentState = WidgetState.DeviceNotFound;
                }
            }
        }

        private bool ShouldAttemptReconnect()
        {
            if (_currentState == WidgetState.NotConfigured)
                return false;

            // Attempt reconnect every 2-3 seconds
            if ((DateTime.Now - _lastRetryTime).TotalSeconds < 2)
                return false;

            _lastRetryTime = DateTime.Now;
            return true;
        }

        private void CaptureAndRender()
        {
            // Capture window
            Bitmap captured = _windowCapture.Capture();

            if (captured == null)
            {
                _retryCount++;
                if (_retryCount >= MAX_RETRY_COUNT)
                {
                    _currentState = WidgetState.CaptureError;
                    RenderCurrentState();
                    _retryCount = 0;
                }
                return;
            }

            _retryCount = 0;

            // Get widget size
            var size = WidgetSize.ToSize();

            // Calculate aspect ratio if needed
            if (_aspectInfo == null ||
                _aspectInfo.SourceWidth != captured.Width ||
                _aspectInfo.SourceHeight != captured.Height)
            {
                _aspectInfo = AspectRatioHelper.Calculate(
                    captured.Width, captured.Height,
                    size.Width, size.Height,
                    _showFooterBar);
            }

            // Create output bitmap
            var output = new Bitmap(size.Width, size.Height);

            using (var g = Graphics.FromImage(output))
            {
                // Clear with background color
                g.Clear(_backgroundColor);

                // Draw captured content with letterbox
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.DrawImage(captured,
                    _aspectInfo.RenderX, _aspectInfo.RenderY,
                    _aspectInfo.RenderWidth, _aspectInfo.RenderHeight);

                // Draw footer bar if enabled
                if (_showFooterBar)
                {
                    FooterBarRenderer.Render(g, size.Width, size.Height,
                        _windowCapture.IsWindowHidden, _backgroundColor, 0f);
                }
            }

            captured.Dispose();

            // Update current bitmap
            lock (_bitmapLock)
            {
                _currentBitmap?.Dispose();
                _currentBitmap = output;
            }

            RaiseWidgetUpdated();
        }

        private void RenderCurrentState()
        {
            var size = WidgetSize.ToSize();

            var bitmap = StateRenderer.CreateStateBitmap(
                _currentState,
                size.Width,
                size.Height,
                _showFooterBar,
                _backgroundColor,
                _windowCapture.IsWindowHidden,
                _savedDevice?.Title,
                MAX_RETRY_COUNT - _retryCount);

            lock (_bitmapLock)
            {
                _currentBitmap?.Dispose();
                _currentBitmap = bitmap;
            }

            RaiseWidgetUpdated();
        }

        private void RaiseWidgetUpdated()
        {
            Bitmap copy;
            lock (_bitmapLock)
            {
                if (_currentBitmap == null) return;
                copy = (Bitmap)_currentBitmap.Clone();
            }

            var args = new WidgetUpdatedEventArgs
            {
                WidgetBitmap = copy,
                Offset = new Point(0, 0),
                WaitMax = 0
            };

            WidgetUpdated?.Invoke(this, args);
        }

        #endregion

        #region IWidgetInstance Methods

        public void RequestUpdate()
        {
            if (!_isPaused && _currentState == WidgetState.Connected)
            {
                CaptureAndRender();
            }
        }

        public void ClickEvent(ClickType clickType, int x, int y)
        {
            var size = WidgetSize.ToSize();

            // Check if click is in footer
            if (_showFooterBar && AspectRatioHelper.IsClickInFooter(y, size.Height, true))
            {
                HandleFooterClick(clickType);
                return;
            }

            // Only forward clicks when connected
            if (_currentState != WidgetState.Connected || _aspectInfo == null)
                return;

            // Map click to source coordinates
            var sourceCoords = AspectRatioHelper.MapClickToSource(
                x, y, _aspectInfo, _showFooterBar, size.Height);

            if (sourceCoords.HasValue)
            {
                bool rightClick = clickType == ClickType.Long;
                _windowCapture.SendClick(sourceCoords.Value.x, sourceCoords.Value.y, rightClick);
            }
        }

        private void HandleFooterClick(ClickType clickType)
        {
            // Toggle visibility on any click in footer (Single or Long)
            // This works better with WigiDash device touch which may not
            // properly detect long presses
            _windowCapture.ToggleVisibility();
            _hideOriginalWindow = _windowCapture.IsWindowHidden;
            SaveSettings();

            // Force immediate re-render to show updated footer icon
            if (_currentState == WidgetState.Connected)
            {
                CaptureAndRender();
            }
            else
            {
                RenderCurrentState();
            }
        }

        public UserControl GetSettingsControl()
        {
            return new StreamDeckMirrorSettingsControl(this);
        }

        public void EnterSleep()
        {
            _isPaused = true;
        }

        public void ExitSleep()
        {
            _isPaused = false;
        }

        #endregion

        #region IWidgetInstanceWithRemoval

        public void OnRemove()
        {
            Dispose();
        }

        #endregion

        #region Public Methods (for Settings UI)

        public DeviceIdentifier GetSavedDevice() => _savedDevice;

        public void SetDevice(StreamDeckWindowInfo windowInfo, int index = 0)
        {
            _savedDevice = DeviceIdentifier.FromWindowInfo(windowInfo, index);
            _windowCapture.TargetHwnd = windowInfo.Hwnd;

            if (_hideOriginalWindow)
            {
                _windowCapture.HideWindow();
            }

            _currentState = WidgetState.Connected;
            _aspectInfo = null; // Force recalculation
            SaveSettings();
        }

        public int GetRefreshInterval() => _refreshIntervalMs;
        public void SetRefreshInterval(int ms)
        {
            _refreshIntervalMs = Math.Max(50, Math.Min(1000, ms));
            SaveSettings();
        }

        public bool GetHideOriginalWindow() => _hideOriginalWindow;
        public void SetHideOriginalWindow(bool hide)
        {
            _hideOriginalWindow = hide;
            if (hide)
                _windowCapture.HideWindow();
            else
                _windowCapture.RestoreWindow();
            SaveSettings();
        }

        public bool GetShowFooterBar() => _showFooterBar;
        public void SetShowFooterBar(bool show)
        {
            _showFooterBar = show;
            _aspectInfo = null; // Force recalculation
            SaveSettings();
        }

        public int GetLongPressDuration() => _longPressDurationMs;
        public void SetLongPressDuration(int ms)
        {
            _longPressDurationMs = Math.Max(400, Math.Min(1000, ms));
            SaveSettings();
        }

        public Color GetBackgroundColor() => _backgroundColor;
        public void SetBackgroundColor(Color color)
        {
            _backgroundColor = color;
            SaveSettings();
        }

        public WidgetState GetCurrentState() => _currentState;
        public bool IsWindowHidden() => _windowCapture.IsWindowHidden;

        #endregion

        #region Logging

        private void Log(string message)
        {
            try
            {
                _widgetObject.WidgetManager?.WriteLogMessage(this, LogLevel.INFO,
                    $"[StreamDeckMirror] {message}");
            }
            catch { }
        }

        private void LogError(string message)
        {
            try
            {
                _widgetObject.WidgetManager?.WriteLogMessage(this, LogLevel.ERROR,
                    $"[StreamDeckMirror] {message}");
            }
            catch { }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            // Stop capture thread
            _isRunning = false;
            _captureThread?.Join(2000);

            // Dispose window capture (restores window if hidden)
            _windowCapture?.Dispose();
            _windowCapture = null;

            // Dispose bitmap
            lock (_bitmapLock)
            {
                _currentBitmap?.Dispose();
                _currentBitmap = null;
            }
        }

        #endregion
    }
}
