using System;

namespace WigiDash.StreamDeckMirrorWidget
{
    // Keep DPI changes local to the native operation; the Manager owns the process and UI.
    internal sealed class DpiScope : IDisposable
    {
        private readonly IntPtr _previous;

        internal DpiScope(IntPtr context)
        {
            _previous = NativeMethods.SetThreadDpiAwarenessContext(context);
            if (_previous == IntPtr.Zero)
                throw new InvalidOperationException("Cannot set the thread DPI context.");
        }

        internal static DpiScope PhysicalPixels() => new DpiScope(new IntPtr(-3));

        public void Dispose()
        {
            NativeMethods.SetThreadDpiAwarenessContext(_previous);
        }
    }
}
