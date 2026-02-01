using System.Drawing;
using System.Drawing.Drawing2D;

namespace WigiDash.StreamDeckMirrorWidget
{
    /// <summary>
    /// Renders the footer bar with visibility toggle control
    /// </summary>
    public static class FooterBarRenderer
    {
        /// <summary>
        /// Render the footer bar
        /// </summary>
        /// <param name="g">Graphics context</param>
        /// <param name="widgetWidth">Widget width</param>
        /// <param name="widgetHeight">Widget height</param>
        /// <param name="isWindowHidden">Whether the original window is hidden</param>
        /// <param name="backgroundColor">Background color</param>
        /// <param name="longPressProgress">Progress of long press (0.0 to 1.0)</param>
        public static void Render(
            Graphics g,
            int widgetWidth,
            int widgetHeight,
            bool isWindowHidden,
            Color backgroundColor,
            float longPressProgress = 0f)
        {
            int footerY = AspectRatioHelper.GetFooterY(widgetHeight);
            int footerHeight = AspectRatioHelper.GetFooterHeight(widgetHeight);

            // Footer background
            using (var brush = new SolidBrush(backgroundColor))
            {
                g.FillRectangle(brush, 0, footerY, widgetWidth, footerHeight);
            }

            // Subtle separator line
            using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255), 1))
            {
                g.DrawLine(pen, 0, footerY, widgetWidth, footerY);
            }

            // Icon area
            int iconSize = (int)(footerHeight * 0.6);
            int iconX = (widgetWidth - iconSize) / 2;
            int iconY = footerY + (footerHeight - iconSize) / 2;

            // Draw eye icon
            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawEyeIcon(g, iconX, iconY, iconSize, isWindowHidden);

            // Long press progress indicator
            if (longPressProgress > 0f)
            {
                int progressWidth = (int)(widgetWidth * longPressProgress);
                using (var progressBrush = new SolidBrush(Color.FromArgb(150, 100, 200, 100)))
                {
                    g.FillRectangle(progressBrush, 0, footerY, progressWidth, 3);
                }
            }
        }

        /// <summary>
        /// Draw an eye icon indicating visibility state
        /// </summary>
        private static void DrawEyeIcon(Graphics g, int x, int y, int size, bool isClosed)
        {
            // Colors based on state
            Color eyeColor = isClosed
                ? Color.FromArgb(150, 100, 100, 100)    // Gray = hidden
                : Color.FromArgb(200, 100, 180, 100);   // Green = visible

            Color outlineColor = isClosed
                ? Color.FromArgb(100, 150, 150, 150)
                : Color.FromArgb(150, 80, 150, 80);

            int eyeWidth = size;
            int eyeHeight = (int)(size * 0.5);
            int eyeY = y + (size - eyeHeight) / 2;

            // Eye outline (almond shape)
            using (var path = new GraphicsPath())
            {
                // Create almond shape using bezier curves
                path.AddBezier(
                    x, eyeY + eyeHeight / 2,                    // Left point
                    x + eyeWidth / 4, eyeY,                      // Top-left control
                    x + eyeWidth * 3 / 4, eyeY,                  // Top-right control
                    x + eyeWidth, eyeY + eyeHeight / 2           // Right point
                );
                path.AddBezier(
                    x + eyeWidth, eyeY + eyeHeight / 2,          // Right point
                    x + eyeWidth * 3 / 4, eyeY + eyeHeight,      // Bottom-right control
                    x + eyeWidth / 4, eyeY + eyeHeight,          // Bottom-left control
                    x, eyeY + eyeHeight / 2                      // Left point
                );

                // Fill eye
                using (var brush = new SolidBrush(Color.FromArgb(40, eyeColor)))
                {
                    g.FillPath(brush, path);
                }

                // Outline
                using (var pen = new Pen(outlineColor, 1.5f))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Pupil (only if eye is open)
            if (!isClosed)
            {
                int pupilSize = eyeHeight / 2;
                int pupilX = x + (eyeWidth - pupilSize) / 2;
                int pupilY = eyeY + (eyeHeight - pupilSize) / 2;

                // Iris
                using (var brush = new SolidBrush(eyeColor))
                {
                    g.FillEllipse(brush, pupilX - 2, pupilY - 2, pupilSize + 4, pupilSize + 4);
                }

                // Pupil center
                using (var brush = new SolidBrush(Color.FromArgb(200, 30, 30, 30)))
                {
                    g.FillEllipse(brush, pupilX, pupilY, pupilSize, pupilSize);
                }

                // Highlight
                int highlightSize = pupilSize / 3;
                using (var brush = new SolidBrush(Color.FromArgb(180, 255, 255, 255)))
                {
                    g.FillEllipse(brush, pupilX + 1, pupilY + 1, highlightSize, highlightSize);
                }
            }
            else
            {
                // Draw crossed line for closed eye
                using (var pen = new Pen(Color.FromArgb(150, 200, 60, 60), 2))
                {
                    g.DrawLine(pen, x, eyeY + eyeHeight / 2, x + eyeWidth, eyeY + eyeHeight / 2);
                }
            }
        }

        /// <summary>
        /// Render footer with a specific icon (for custom rendering)
        /// </summary>
        public static void RenderWithIcon(
            Graphics g,
            int widgetWidth,
            int widgetHeight,
            Color backgroundColor,
            Image icon)
        {
            int footerY = AspectRatioHelper.GetFooterY(widgetHeight);
            int footerHeight = AspectRatioHelper.GetFooterHeight(widgetHeight);

            // Footer background
            using (var brush = new SolidBrush(backgroundColor))
            {
                g.FillRectangle(brush, 0, footerY, widgetWidth, footerHeight);
            }

            // Separator line
            using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255), 1))
            {
                g.DrawLine(pen, 0, footerY, widgetWidth, footerY);
            }

            // Draw icon centered
            if (icon != null)
            {
                int iconSize = (int)(footerHeight * 0.6);
                int iconX = (widgetWidth - iconSize) / 2;
                int iconY = footerY + (footerHeight - iconSize) / 2;
                g.DrawImage(icon, iconX, iconY, iconSize, iconSize);
            }
        }
    }
}
