using System;
using System.Drawing;

namespace WigiDash.StreamDeckMirrorWidget
{
    /// <summary>
    /// Handles capturing window content using PrintWindow
    /// </summary>
    public class WindowCapture : IDisposable
    {
        private IntPtr _hwnd;
        private Bitmap _captureBuffer;
        private int _lastWidth;
        private int _lastHeight;
        private bool _isDisposed;

        /// <summary>
        /// Current target window handle
        /// </summary>
        public IntPtr TargetHwnd
        {
            get => _hwnd;
            set
            {
                if (_hwnd != value)
                {
                    _hwnd = value;
                    // Reset buffer when target changes
                    _captureBuffer?.Dispose();
                    _captureBuffer = null;
                }
            }
        }

        /// <summary>
        /// Whether window visibility is being hidden via transparency
        /// </summary>
        public bool IsWindowHidden { get; private set; }

        private int _originalWindowStyle;

        /// <summary>
        /// Capture the current window content
        /// </summary>
        /// <returns>Captured bitmap, or null if capture failed</returns>
        public Bitmap Capture()
        {
            if (_hwnd == IntPtr.Zero || !NativeMethods.IsWindow(_hwnd))
                return null;

            try
            {
                // Get window dimensions
                if (!NativeMethods.GetWindowRect(_hwnd, out var rect))
                    return null;

                int width = rect.Width;
                int height = rect.Height;

                if (width <= 0 || height <= 0)
                    return null;

                // Recreate buffer if size changed
                if (_captureBuffer == null || _lastWidth != width || _lastHeight != height)
                {
                    _captureBuffer?.Dispose();
                    _captureBuffer = new Bitmap(width, height);
                    _lastWidth = width;
                    _lastHeight = height;
                }

                // Capture window using PrintWindow
                using (var g = Graphics.FromImage(_captureBuffer))
                {
                    IntPtr hdc = g.GetHdc();
                    try
                    {
                        bool success = NativeMethods.PrintWindow(_hwnd, hdc, NativeMethods.PW_RENDERFULLCONTENT);
                        if (!success)
                            return null;
                    }
                    finally
                    {
                        g.ReleaseHdc(hdc);
                    }
                }

                // Return a clone to avoid threading issues
                return (Bitmap)_captureBuffer.Clone();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the current window dimensions
        /// </summary>
        public (int width, int height) GetWindowSize()
        {
            if (_hwnd == IntPtr.Zero || !NativeMethods.IsWindow(_hwnd))
                return (0, 0);

            if (NativeMethods.GetWindowRect(_hwnd, out var rect))
                return (rect.Width, rect.Height);

            return (0, 0);
        }

        /// <summary>
        /// Hide the window from the monitor using transparency
        /// </summary>
        public bool HideWindow()
        {
            if (_hwnd == IntPtr.Zero || IsWindowHidden)
                return false;

            try
            {
                // Save original style
                _originalWindowStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);

                // Add layered style and set alpha to 0
                NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE,
                    _originalWindowStyle | NativeMethods.WS_EX_LAYERED);
                NativeMethods.SetLayeredWindowAttributes(_hwnd, 0, 0, NativeMethods.LWA_ALPHA);

                IsWindowHidden = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Restore window visibility
        /// </summary>
        public bool RestoreWindow()
        {
            if (_hwnd == IntPtr.Zero || !IsWindowHidden)
                return false;

            try
            {
                // Restore full opacity
                NativeMethods.SetLayeredWindowAttributes(_hwnd, 0, 255, NativeMethods.LWA_ALPHA);

                IsWindowHidden = false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Toggle window visibility
        /// </summary>
        public bool ToggleVisibility()
        {
            if (IsWindowHidden)
                return RestoreWindow();
            else
                return HideWindow();
        }

        /// <summary>
        /// Check if the target window is still valid
        /// </summary>
        public bool IsWindowValid()
        {
            return _hwnd != IntPtr.Zero && NativeMethods.IsWindow(_hwnd);
        }

        /// <summary>
        /// Send a click event to the window
        /// </summary>
        /// <param name="x">X coordinate in window space</param>
        /// <param name="y">Y coordinate in window space</param>
        /// <param name="rightClick">True for right click, false for left click</param>
        public void SendClick(int x, int y, bool rightClick = false)
        {
            if (_hwnd == IntPtr.Zero)
                return;

            IntPtr lParam = NativeMethods.MakeLParam(x, y);

            if (rightClick)
            {
                NativeMethods.PostMessage(_hwnd, NativeMethods.WM_RBUTTONDOWN, (IntPtr)0x0002, lParam);
                NativeMethods.PostMessage(_hwnd, NativeMethods.WM_RBUTTONUP, IntPtr.Zero, lParam);
            }
            else
            {
                NativeMethods.PostMessage(_hwnd, NativeMethods.WM_LBUTTONDOWN, (IntPtr)0x0001, lParam);
                NativeMethods.PostMessage(_hwnd, NativeMethods.WM_LBUTTONUP, IntPtr.Zero, lParam);
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            // Restore window if we hid it
            if (IsWindowHidden)
            {
                RestoreWindow();
            }

            _captureBuffer?.Dispose();
            _captureBuffer = null;
        }
    }
}
