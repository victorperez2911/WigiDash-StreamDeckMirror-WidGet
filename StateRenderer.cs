using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace WigiDash.StreamDeckMirrorWidget
{
    /// <summary>
    /// Renders visual feedback for different widget states
    /// </summary>
    public static class StateRenderer
    {
        /// <summary>
        /// Render a state screen
        /// </summary>
        /// <param name="g">Graphics context</param>
        /// <param name="state">Current widget state</param>
        /// <param name="widgetWidth">Widget width</param>
        /// <param name="widgetHeight">Widget height</param>
        /// <param name="showFooterBar">Whether footer bar is shown</param>
        /// <param name="backgroundColor">Background color</param>
        /// <param name="deviceName">Device name (for DeviceNotFound state)</param>
        /// <param name="retryCountdown">Retry countdown seconds (for CaptureError state)</param>
        public static void RenderState(
            Graphics g,
            WidgetState state,
            int widgetWidth,
            int widgetHeight,
            bool showFooterBar,
            Color backgroundColor,
            string deviceName = null,
            int retryCountdown = 0)
        {
            // Calculate content area (excluding footer)
            int contentHeight = showFooterBar
                ? AspectRatioHelper.GetFooterY(widgetHeight)
                : widgetHeight;

            // Clear background
            using (var brush = new SolidBrush(backgroundColor))
            {
                g.FillRectangle(brush, 0, 0, widgetWidth, contentHeight);
            }

            // Configure text rendering
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            // Calculate text area
            int centerY = contentHeight / 2;
            int iconSize = Math.Min(widgetWidth, contentHeight) / 5;
            int iconY = contentHeight / 3 - iconSize / 2;

            // Render based on state
            switch (state)
            {
                case WidgetState.AppNotRunning:
                    RenderAppNotRunning(g, widgetWidth, contentHeight, centerY, iconY, iconSize);
                    break;

                case WidgetState.DeviceNotFound:
                    RenderDeviceNotFound(g, widgetWidth, contentHeight, centerY, iconY, iconSize, deviceName);
                    break;

                case WidgetState.CaptureError:
                    RenderCaptureError(g, widgetWidth, contentHeight, centerY, iconY, iconSize, retryCountdown);
                    break;

                case WidgetState.NotConfigured:
                    RenderNotConfigured(g, widgetWidth, contentHeight, centerY, iconY, iconSize);
                    break;
            }
        }

        private static void RenderAppNotRunning(Graphics g, int width, int height, int centerY, int iconY, int iconSize)
        {
            // Icon
            DrawIconText(g, "X", width / 2, iconY + iconSize / 2, iconSize, Color.FromArgb(220, 200, 60, 60));

            // Text
            DrawCenteredText(g, "Stream Deck não está", width / 2, centerY - 5, 12, true, Color.White);
            DrawCenteredText(g, "em execução", width / 2, centerY + 20, 12, true, Color.White);
            DrawCenteredText(g, "Inicie o aplicativo Elgato", width / 2, centerY + 55, 9, false, Color.LightGray);
            DrawCenteredText(g, "Stream Deck para continuar", width / 2, centerY + 72, 9, false, Color.LightGray);
        }

        private static void RenderDeviceNotFound(Graphics g, int width, int height, int centerY, int iconY, int iconSize, string deviceName)
        {
            // Icon
            DrawIconText(g, "?", width / 2, iconY + iconSize / 2, iconSize, Color.FromArgb(220, 200, 150, 50));

            // Text
            DrawCenteredText(g, "Virtual Device", width / 2, centerY - 5, 12, true, Color.White);

            if (!string.IsNullOrEmpty(deviceName))
            {
                DrawCenteredText(g, $"\"{deviceName}\"", width / 2, centerY + 18, 10, false, Color.White);
                DrawCenteredText(g, "não encontrado", width / 2, centerY + 40, 12, true, Color.White);
                DrawCenteredText(g, "Reconectando...", width / 2, centerY + 75, 9, false, Color.LightGray);
            }
            else
            {
                DrawCenteredText(g, "não encontrado", width / 2, centerY + 20, 12, true, Color.White);
                DrawCenteredText(g, "Reconectando...", width / 2, centerY + 55, 9, false, Color.LightGray);
            }
        }

        private static void RenderCaptureError(Graphics g, int width, int height, int centerY, int iconY, int iconSize, int retryCountdown)
        {
            // Icon
            DrawIconText(g, "!", width / 2, iconY + iconSize / 2, iconSize, Color.FromArgb(220, 200, 150, 50));

            // Text
            DrawCenteredText(g, "Erro ao capturar", width / 2, centerY - 5, 12, true, Color.White);
            DrawCenteredText(g, "imagem", width / 2, centerY + 20, 12, true, Color.White);

            if (retryCountdown > 0)
            {
                DrawCenteredText(g, $"Tentando novamente em {retryCountdown}s...", width / 2, centerY + 55, 9, false, Color.LightGray);
            }
            else
            {
                DrawCenteredText(g, "Tentando novamente...", width / 2, centerY + 55, 9, false, Color.LightGray);
            }
        }

        private static void RenderNotConfigured(Graphics g, int width, int height, int centerY, int iconY, int iconSize)
        {
            // Icon (gear)
            DrawIconText(g, "*", width / 2, iconY + iconSize / 2, iconSize, Color.FromArgb(220, 100, 150, 200));

            // Text
            DrawCenteredText(g, "Nenhum Virtual Device", width / 2, centerY - 5, 11, true, Color.White);
            DrawCenteredText(g, "configurado", width / 2, centerY + 18, 11, true, Color.White);
            DrawCenteredText(g, "Abra as configurações do widget", width / 2, centerY + 53, 9, false, Color.LightGray);
            DrawCenteredText(g, "para selecionar um device", width / 2, centerY + 70, 9, false, Color.LightGray);
        }

        private static void DrawIconText(Graphics g, string text, int x, int y, int size, Color color)
        {
            using (var font = new Font("Arial", size * 0.6f, FontStyle.Bold))
            using (var brush = new SolidBrush(color))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                // Draw circle background
                int circleSize = size;
                using (var circleBrush = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                {
                    g.FillEllipse(circleBrush, x - circleSize / 2, y - circleSize / 2, circleSize, circleSize);
                }

                // Draw border
                using (var pen = new Pen(color, 2))
                {
                    g.DrawEllipse(pen, x - circleSize / 2, y - circleSize / 2, circleSize, circleSize);
                }

                // Draw text
                g.DrawString(text, font, brush, x, y, sf);
            }
        }

        private static void DrawCenteredText(Graphics g, string text, int x, int y, float fontSize, bool bold, Color color)
        {
            var style = bold ? FontStyle.Bold : FontStyle.Regular;
            using (var font = new Font("Segoe UI", fontSize, style))
            using (var brush = new SolidBrush(color))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(text, font, brush, x, y, sf);
            }
        }

        /// <summary>
        /// Create a complete state bitmap
        /// </summary>
        public static Bitmap CreateStateBitmap(
            WidgetState state,
            int widgetWidth,
            int widgetHeight,
            bool showFooterBar,
            Color backgroundColor,
            bool isWindowHidden = false,
            string deviceName = null,
            int retryCountdown = 0)
        {
            var bitmap = new Bitmap(widgetWidth, widgetHeight);

            using (var g = Graphics.FromImage(bitmap))
            {
                // Render state content
                RenderState(g, state, widgetWidth, widgetHeight, showFooterBar,
                    backgroundColor, deviceName, retryCountdown);

                // Render footer if enabled
                if (showFooterBar)
                {
                    FooterBarRenderer.Render(g, widgetWidth, widgetHeight, isWindowHidden, backgroundColor);
                }
            }

            return bitmap;
        }
    }
}
