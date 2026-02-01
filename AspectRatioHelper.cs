using System;

namespace WigiDash.StreamDeckMirrorWidget
{
    /// <summary>
    /// Information about aspect ratio calculations for rendering
    /// </summary>
    public class AspectRatioInfo
    {
        /// <summary>Source window width</summary>
        public int SourceWidth { get; set; }

        /// <summary>Source window height</summary>
        public int SourceHeight { get; set; }

        /// <summary>Content area width (may be less than widget width if footer is shown)</summary>
        public int ContentAreaWidth { get; set; }

        /// <summary>Content area height (may be less than widget height if footer is shown)</summary>
        public int ContentAreaHeight { get; set; }

        /// <summary>X offset where content is rendered (for letterbox)</summary>
        public int RenderX { get; set; }

        /// <summary>Y offset where content is rendered (for letterbox)</summary>
        public int RenderY { get; set; }

        /// <summary>Width of rendered content</summary>
        public int RenderWidth { get; set; }

        /// <summary>Height of rendered content</summary>
        public int RenderHeight { get; set; }

        /// <summary>Scale factor from source to rendered size</summary>
        public double Scale { get; set; }
    }

    /// <summary>
    /// Helper class for aspect ratio calculations and coordinate mapping
    /// </summary>
    public static class AspectRatioHelper
    {
        /// <summary>
        /// Footer bar height as percentage of widget height (5%)
        /// </summary>
        public const double FOOTER_HEIGHT_PERCENT = 0.05;

        /// <summary>
        /// Calculate aspect ratio information for rendering
        /// </summary>
        /// <param name="sourceW">Source window width</param>
        /// <param name="sourceH">Source window height</param>
        /// <param name="widgetW">Widget width</param>
        /// <param name="widgetH">Widget height</param>
        /// <param name="showFooterBar">Whether footer bar is shown</param>
        /// <returns>Aspect ratio information</returns>
        public static AspectRatioInfo Calculate(
            int sourceW, int sourceH,
            int widgetW, int widgetH,
            bool showFooterBar)
        {
            // Calculate available content area
            int contentH = showFooterBar
                ? (int)(widgetH * (1.0 - FOOTER_HEIGHT_PERCENT))
                : widgetH;
            int contentW = widgetW;

            var info = new AspectRatioInfo
            {
                SourceWidth = sourceW,
                SourceHeight = sourceH,
                ContentAreaWidth = contentW,
                ContentAreaHeight = contentH
            };

            // Handle invalid dimensions
            if (sourceW <= 0 || sourceH <= 0 || contentW <= 0 || contentH <= 0)
            {
                info.RenderWidth = contentW;
                info.RenderHeight = contentH;
                info.Scale = 1.0;
                return info;
            }

            double sourceAspect = (double)sourceW / sourceH;
            double contentAspect = (double)contentW / contentH;

            if (sourceAspect > contentAspect)
            {
                // Source is wider - letterbox vertical (bars top/bottom)
                info.RenderWidth = contentW;
                info.RenderHeight = (int)(contentW / sourceAspect);
                info.RenderX = 0;
                info.RenderY = (contentH - info.RenderHeight) / 2;
            }
            else
            {
                // Source is taller - letterbox horizontal (bars left/right)
                info.RenderHeight = contentH;
                info.RenderWidth = (int)(contentH * sourceAspect);
                info.RenderX = (contentW - info.RenderWidth) / 2;
                info.RenderY = 0;
            }

            info.Scale = (double)info.RenderWidth / sourceW;

            return info;
        }

        /// <summary>
        /// Map click coordinates from widget to source window
        /// </summary>
        /// <param name="clickX">Click X in widget coordinates</param>
        /// <param name="clickY">Click Y in widget coordinates</param>
        /// <param name="info">Aspect ratio info</param>
        /// <param name="showFooterBar">Whether footer bar is shown</param>
        /// <param name="widgetHeight">Full widget height</param>
        /// <returns>Source coordinates, or null if click is outside content area</returns>
        public static (int x, int y)? MapClickToSource(
            int clickX, int clickY,
            AspectRatioInfo info,
            bool showFooterBar,
            int widgetHeight)
        {
            // Check if click is in footer area
            if (showFooterBar)
            {
                int footerY = (int)(widgetHeight * (1.0 - FOOTER_HEIGHT_PERCENT));
                if (clickY >= footerY)
                {
                    return null; // Click in footer
                }
            }

            // Check if click is in content area (not letterbox)
            if (clickX < info.RenderX || clickX >= info.RenderX + info.RenderWidth ||
                clickY < info.RenderY || clickY >= info.RenderY + info.RenderHeight)
            {
                return null; // Click in letterbox
            }

            // Convert to source coordinates
            int relativeX = clickX - info.RenderX;
            int relativeY = clickY - info.RenderY;

            int sourceX = (int)(relativeX / info.Scale);
            int sourceY = (int)(relativeY / info.Scale);

            // Clamp to bounds
            sourceX = Math.Max(0, Math.Min(sourceX, info.SourceWidth - 1));
            sourceY = Math.Max(0, Math.Min(sourceY, info.SourceHeight - 1));

            return (sourceX, sourceY);
        }

        /// <summary>
        /// Check if click is in the footer bar area
        /// </summary>
        public static bool IsClickInFooter(int clickY, int widgetHeight, bool showFooterBar)
        {
            if (!showFooterBar) return false;
            int footerY = (int)(widgetHeight * (1.0 - FOOTER_HEIGHT_PERCENT));
            return clickY >= footerY;
        }

        /// <summary>
        /// Get the Y coordinate where footer starts
        /// </summary>
        public static int GetFooterY(int widgetHeight)
        {
            return (int)(widgetHeight * (1.0 - FOOTER_HEIGHT_PERCENT));
        }

        /// <summary>
        /// Get the height of the footer bar
        /// </summary>
        public static int GetFooterHeight(int widgetHeight)
        {
            return widgetHeight - GetFooterY(widgetHeight);
        }
    }
}
