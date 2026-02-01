using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using WigiDashWidgetFramework;
using WigiDashWidgetFramework.WidgetUtility;

namespace WigiDash.StreamDeckMirrorWidget
{
    /// <summary>
    /// Main widget object that implements the WigiDash framework contract.
    /// Manages widget metadata and instance creation.
    /// </summary>
    public class StreamDeckMirrorWidgetObject : IWidgetObject
    {
        private string _resourcePath;
        private Bitmap _iconBitmap;

        #region IWidgetBase Implementation

        /// <summary>
        /// Unique identifier for this widget. Must match the assembly name.
        /// </summary>
        public Guid Guid => new Guid("B7E4D1A2-5C8F-4E9B-A3D6-1F2E3B4C5D6E");

        /// <summary>
        /// Display name of the widget shown in WigiDash App
        /// </summary>
        public string Name => "Stream Deck Mirror";

        /// <summary>
        /// Widget author information
        /// </summary>
        public string Author => "Victor Perez";

        /// <summary>
        /// Author website or repository URL
        /// </summary>
        public string Website => "https://github.com/victorperez2911/WigiDash-StreamDeckMirror-WidGet";

        /// <summary>
        /// Brief description of widget functionality
        /// </summary>
        public string Description => "Mirrors Elgato Stream Deck Virtual Device on WigiDash";

        /// <summary>
        /// Widget version
        /// </summary>
        public Version Version => new Version(1, 0, 0);

        /// <summary>
        /// Target SDK version
        /// </summary>
        public SdkVersion TargetSdk => WidgetUtility.CurrentSdkVersion;

        /// <summary>
        /// Supported widget sizes (all sizes from 1x1 to 5x4)
        /// </summary>
        public List<WidgetSize> SupportedSizes
        {
            get
            {
                var sizes = new List<WidgetSize>();
                for (int y = 1; y <= 4; y++)
                {
                    for (int x = 1; x <= 5; x++)
                    {
                        sizes.Add(new WidgetSize(x, y));
                    }
                }
                return sizes;
            }
        }

        /// <summary>
        /// Preview image shown in widget gallery (1x1 size)
        /// </summary>
        public Bitmap PreviewImage => GetWidgetPreview(new WidgetSize(1, 1));

        #endregion

        #region IWidgetObject Implementation

        /// <summary>
        /// Widget manager provided by WigiDash framework
        /// </summary>
        public IWidgetManager WidgetManager { get; set; }

        /// <summary>
        /// Widget thumbnail displayed in the widget selector
        /// </summary>
        public Bitmap WidgetThumbnail => PreviewImage;

        /// <summary>
        /// Last error message (for debugging)
        /// </summary>
        public string LastErrorMessage { get; set; }

        /// <summary>
        /// Load widget resources when widget is first loaded
        /// </summary>
        /// <param name="resourcePath">Path to widget resource directory</param>
        /// <returns>Error code</returns>
        public WidgetError Load(string resourcePath)
        {
            try
            {
                _resourcePath = resourcePath;

                // Load icon from resources
                string iconPath = Path.Combine(_resourcePath, "icon.png");
                if (File.Exists(iconPath))
                {
                    _iconBitmap = new Bitmap(iconPath);
                }
                else
                {
                    // Create fallback icon if file not found
                    _iconBitmap = CreateFallbackIcon();
                }

                return WidgetError.NO_ERROR;
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"Failed to load widget: {ex.Message}";
                return WidgetError.CUSTOM_ERROR;
            }
        }

        /// <summary>
        /// Unload widget resources when widget is removed
        /// </summary>
        /// <returns>Error code</returns>
        public WidgetError Unload()
        {
            try
            {
                _iconBitmap?.Dispose();
                _iconBitmap = null;
                return WidgetError.NO_ERROR;
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"Failed to unload widget: {ex.Message}";
                return WidgetError.CUSTOM_ERROR;
            }
        }

        /// <summary>
        /// Generate preview image for a specific widget size
        /// </summary>
        /// <param name="widgetSize">Size to generate preview for</param>
        /// <returns>Preview bitmap</returns>
        public Bitmap GetWidgetPreview(WidgetSize widgetSize)
        {
            try
            {
                Size size = widgetSize.ToSize();
                Bitmap preview = new Bitmap(size.Width, size.Height);

                using (Graphics g = Graphics.FromImage(preview))
                {
                    // Dark background matching WigiDash theme
                    g.Clear(Color.FromArgb(48, 48, 48));

                    // Draw centered icon if available
                    if (_iconBitmap != null)
                    {
                        // Calculate centered position
                        int iconWidth = Math.Min(_iconBitmap.Width, size.Width - 20);
                        int iconHeight = Math.Min(_iconBitmap.Height, size.Height - 20);
                        int x = (size.Width - iconWidth) / 2;
                        int y = (size.Height - iconHeight) / 2;

                        g.DrawImage(_iconBitmap, x, y, iconWidth, iconHeight);
                    }
                    else
                    {
                        // Draw text placeholder
                        using (Font font = new Font("Segoe UI", 10, FontStyle.Bold))
                        using (Brush brush = new SolidBrush(Color.White))
                        {
                            var sf = new StringFormat
                            {
                                Alignment = StringAlignment.Center,
                                LineAlignment = StringAlignment.Center
                            };
                            g.DrawString("SD\nMirror", font, brush,
                                new RectangleF(0, 0, size.Width, size.Height), sf);
                        }
                    }
                }

                return preview;
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"Failed to create preview: {ex.Message}";
                return CreateFallbackIcon();
            }
        }

        /// <summary>
        /// Create a new widget instance
        /// </summary>
        /// <param name="widgetSize">Size of the widget instance</param>
        /// <param name="instanceGuid">Unique identifier for this instance</param>
        /// <returns>New widget instance</returns>
        public IWidgetInstance CreateWidgetInstance(WidgetSize widgetSize, Guid instanceGuid)
        {
            try
            {
                return new StreamDeckMirrorWidgetInstance(this, widgetSize, instanceGuid, _resourcePath);
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"Failed to create instance: {ex.Message}";
                return null;
            }
        }

        /// <summary>
        /// Remove a widget instance
        /// </summary>
        /// <param name="instanceGuid">Unique identifier of instance to remove</param>
        /// <returns>True if successfully removed</returns>
        public bool RemoveWidgetInstance(Guid instanceGuid)
        {
            // Instance cleanup is handled in the instance's Dispose method
            return true;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a simple fallback icon when icon.png is not available
        /// </summary>
        private Bitmap CreateFallbackIcon()
        {
            Bitmap fallback = new Bitmap(200, 145);
            using (Graphics g = Graphics.FromImage(fallback))
            {
                g.Clear(Color.FromArgb(48, 48, 48));

                // Draw Stream Deck representation
                using (Brush brush = new SolidBrush(Color.FromArgb(60, 60, 60)))
                {
                    g.FillRoundedRectangle(brush, 40, 30, 120, 85, 10);
                }

                // Draw grid of buttons
                using (Brush buttonBrush = new SolidBrush(Color.FromArgb(80, 80, 80)))
                {
                    for (int row = 0; row < 2; row++)
                    {
                        for (int col = 0; col < 3; col++)
                        {
                            int x = 50 + col * 35;
                            int y = 40 + row * 35;
                            g.FillRectangle(buttonBrush, x, y, 28, 28);
                        }
                    }
                }

                // Draw text
                using (Font font = new Font("Segoe UI", 8, FontStyle.Bold))
                using (Brush textBrush = new SolidBrush(Color.White))
                {
                    StringFormat sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString("SD Mirror", font, textBrush,
                        new RectangleF(0, 115, 200, 25), sf);
                }
            }
            return fallback;
        }

        #endregion
    }

    /// <summary>
    /// Extension methods for Graphics
    /// </summary>
    public static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics g, Brush brush, int x, int y, int width, int height, int radius)
        {
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddArc(x, y, radius * 2, radius * 2, 180, 90);
                path.AddArc(x + width - radius * 2, y, radius * 2, radius * 2, 270, 90);
                path.AddArc(x + width - radius * 2, y + height - radius * 2, radius * 2, radius * 2, 0, 90);
                path.AddArc(x, y + height - radius * 2, radius * 2, radius * 2, 90, 90);
                path.CloseFigure();
                g.FillPath(brush, path);
            }
        }
    }
}
