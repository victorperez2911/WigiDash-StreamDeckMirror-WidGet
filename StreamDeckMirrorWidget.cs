using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenWigi.Sdk;
using WigiDash.StreamDeckMirrorWidget;

[assembly: OpenWigiWidget(typeof(StreamDeckMirrorWidget))]

namespace WigiDash.StreamDeckMirrorWidget
{
    /// <summary>Entrada do plugin no OpenWigiDashManager: ficha do widget e fábrica de instâncias.</summary>
    public sealed class StreamDeckMirrorWidget : IWidget
    {
        // Mesmo id da versão net472: o layout migrado do manager oficial continua achando o widget.
        public static readonly Guid Id = new Guid("B7E4D1A2-5C8F-4E9B-A3D6-1F2E3B4C5D6E");

        private const int MinSpan = 2;

        private IWidgetHost _host;

        public WidgetManifest Manifest { get; } = new WidgetManifest
        {
            Id = Id,
            Name = "Stream Deck Mirror",
            Version = new Version(3, 0, 0),
            Author = "Victor Perez",
            Website = "https://github.com/OpenWigiDash/WigiDash-StreamDeckMirror-WidGet",
            Description = "Espelha o Virtual Device do Elgato Stream Deck no WigiDash.",
            // Grade de meias células (10x8): do 2x2 (o antigo 1x1, 200x145) até a tela inteira, incluindo
            // os tamanhos ímpares. Com uma meia célula só de largura ou altura (98 ou 70 px), os botões do
            // Stream Deck e a barra do rodapé ficam pequenos demais para tocar.
            SupportedSizes = Enumerable.Range(MinSpan, WidgetGrid.Rows - MinSpan + 1)
                .SelectMany(rows => Enumerable.Range(MinSpan, WidgetGrid.Columns - MinSpan + 1).Select(columns => new WidgetSize(columns, rows)))
                .ToList(),
        };

        public ValueTask LoadAsync(IWidgetHost host, CancellationToken cancellationToken)
        {
            _host = host;
            return ValueTask.CompletedTask;
        }

        public ValueTask<IWidgetInstance> CreateInstanceAsync(IWidgetContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IWidgetInstance>(new StreamDeckMirrorWidgetInstance(context, _host?.ResourceDirectory));

        public ValueTask<WidgetFrame> GetPreviewAsync(WidgetSize size, PixelSize pixels, CancellationToken cancellationToken)
        {
            // Desenha no tamanho que o host pede (área de conteúdo, já sem a moldura da página).
            var iconPath = _host is null ? null : Path.Combine(_host.ResourceDirectory, "icon.png");
            using var bitmap = new Bitmap(pixels.Width, pixels.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.FromArgb(48, 48, 48));
                if (iconPath is not null && File.Exists(iconPath))
                {
                    using var icon = new Bitmap(iconPath);
                    // Encaixa o ícone (quadrado) sem distorcer, com 10 px de margem.
                    var scale = Math.Min(1.0, Math.Min((pixels.Width - 20.0) / icon.Width, (pixels.Height - 20.0) / icon.Height));
                    var w = (int)(icon.Width * scale);
                    var h = (int)(icon.Height * scale);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(icon, (pixels.Width - w) / 2, (pixels.Height - h) / 2, w, h);
                }
            }
            return ValueTask.FromResult<WidgetFrame>(Frames.FromBitmap(bitmap));
        }
    }
}
