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

        private IWidgetHost _host;

        public WidgetManifest Manifest { get; } = new WidgetManifest
        {
            Id = Id,
            Name = "Stream Deck Mirror",
            Version = new Version(2, 0, 0),
            Author = "Victor Perez",
            Website = "https://github.com/victorperez2911/WigiDash-StreamDeckMirror-WidGet",
            Description = "Espelha o Virtual Device do Elgato Stream Deck no WigiDash.",
            // Todos os tamanhos da grade, do 1x1 ao 5x4.
            SupportedSizes = Enumerable.Range(1, WidgetGrid.Rows)
                .SelectMany(rows => Enumerable.Range(1, WidgetGrid.Columns).Select(columns => new WidgetSize(columns, rows)))
                .ToList(),
        };

        public ValueTask LoadAsync(IWidgetHost host, CancellationToken cancellationToken)
        {
            _host = host;
            return ValueTask.CompletedTask;
        }

        public ValueTask<IWidgetInstance> CreateInstanceAsync(IWidgetContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IWidgetInstance>(new StreamDeckMirrorWidgetInstance(context, _host?.ResourceDirectory));

        public ValueTask<WidgetFrame> GetPreviewAsync(WidgetSize size, CancellationToken cancellationToken)
        {
            var pixels = size.PixelSize;
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
