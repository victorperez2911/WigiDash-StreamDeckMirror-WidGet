using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenWigi.Sdk;

namespace WigiDash.StreamDeckMirrorWidget
{
    /// <summary>Converte um <see cref="Bitmap"/> do System.Drawing no quadro do SDK (Bgra32, sem cópia extra de formato).</summary>
    internal static class Frames
    {
        public static WidgetFrame FromBitmap(Bitmap bitmap)
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var pixels = new byte[data.Stride * bitmap.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                return new WidgetFrame(new PixelSize(bitmap.Width, bitmap.Height), FramePixelFormat.Bgra32, pixels, data.Stride);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }
}
