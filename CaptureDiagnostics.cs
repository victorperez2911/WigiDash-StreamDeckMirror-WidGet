using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace WigiDash.StreamDeckMirrorWidget
{
    // Temporary, bounded diagnostic recording. Never changes process-wide DPI awareness.
    internal sealed class CaptureDiagnostics
    {
        private readonly object _gate = new object();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly string _directory;
        private long _nextSampleMs;
        private int _samples;
        private bool _failed;
        private const int MaxSamples = 240; // At most twenty minutes, one pair every five seconds.
        internal bool Enabled { get; }

        internal CaptureDiagnostics(Guid instanceGuid, string resourcePath)
        {
            Enabled = !string.IsNullOrEmpty(resourcePath) &&
                File.Exists(Path.Combine(resourcePath, "capture-diagnostics.enabled"));
            _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WigiDash", "StreamDeckMirror", "Diagnostics",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + instanceGuid);
        }

        internal void Record(IntPtr hwnd, Bitmap source, Bitmap output, AspectRatioInfo aspect,
            bool hidden, Action<string> log)
        {
            if (!Enabled) return;
            if (!Monitor.TryEnter(_gate)) return;
            try
            {
                if (_failed || _samples >= MaxSamples || _clock.ElapsedMilliseconds < _nextSampleMs) return;
                _nextSampleMs = _clock.ElapsedMilliseconds + 5000;
                Directory.CreateDirectory(_directory);

                var originalContext = NativeMethods.GetThreadDpiAwarenessContext();
                NativeMethods.GetWindowRect(hwnd, out var logical);
                NativeMethods.GetClientRect(hwnd, out var client);
                uint dpi = NativeMethods.GetDpiForWindow(hwnd);
                int desktopW = NativeMethods.GetSystemMetrics(0);
                int desktopH = NativeMethods.GetSystemMetrics(1);

                // Query the same window in physical coordinates, then immediately restore the caller.
                NativeMethods.RECT physical = default;
                int physicalDesktopW = 0, physicalDesktopH = 0;
                var previous = NativeMethods.SetThreadDpiAwarenessContext(new IntPtr(-3));
                bool physicalValid = false;
                if (previous != IntPtr.Zero)
                {
                    try
                    {
                        physicalValid = NativeMethods.GetWindowRect(hwnd, out physical);
                        physicalDesktopW = NativeMethods.GetSystemMetrics(0);
                        physicalDesktopH = NativeMethods.GetSystemMetrics(1);
                    }
                    finally { NativeMethods.SetThreadDpiAwarenessContext(previous); }
                }

                NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out uint pid);
                string foreground = "unknown";
                try { using (var process = Process.GetProcessById((int)pid)) foreground = process.ProcessName; }
                catch (ArgumentException) { }

                string name = (++_samples).ToString("D4", CultureInfo.InvariantCulture);
                source.Save(Path.Combine(_directory, name + "-source.png"), ImageFormat.Png);
                output.Save(Path.Combine(_directory, name + "-output.png"), ImageFormat.Png);
                string table = Path.Combine(_directory, "samples.tsv");
                if (!File.Exists(table))
                {
                    File.WriteAllText(table,
                        "utc\tsample\tforeground\thwnd\tthreadDpiContext\twindowDpi\twindowW\twindowH\tclientW\tclientH\tphysicalValid\tphysicalW\tphysicalH\tdesktopW\tdesktopH\tphysicalDesktopW\tphysicalDesktopH\tsourceW\tsourceH\tsourceDpiX\tsourceDpiY\toutputW\toutputH\toutputDpiX\toutputDpiY\trenderX\trenderY\trenderW\trenderH\tcachedSourceW\tcachedSourceH\tcachedContentW\tcachedContentH\thidden\n", Encoding.UTF8);
                    log("Diagnostic images and measurements: " + _directory);
                }
                object[] values = { DateTime.UtcNow.ToString("O"), name, foreground, hwnd.ToInt64(),
                    originalContext.ToInt64(), dpi, logical.Width, logical.Height, client.Width, client.Height,
                    physicalValid, physical.Width, physical.Height, desktopW, desktopH, physicalDesktopW,
                    physicalDesktopH, source.Width, source.Height, source.HorizontalResolution,
                    source.VerticalResolution, output.Width, output.Height, output.HorizontalResolution,
                    output.VerticalResolution, aspect.RenderX, aspect.RenderY, aspect.RenderWidth,
                    aspect.RenderHeight, aspect.SourceWidth, aspect.SourceHeight, aspect.ContentAreaWidth,
                    aspect.ContentAreaHeight, hidden };
                var line = new StringBuilder();
                foreach (object value in values)
                {
                    if (line.Length > 0) line.Append('\t');
                    line.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                }
                File.AppendAllText(table, line + "\n", Encoding.UTF8);
                if (_samples == MaxSamples) log("Diagnostic recording reached its sample limit.");
            }
            catch (Exception ex)
            {
                _failed = true; // Avoid repeated disk/API failures in the capture loop.
                log("Diagnostic recording stopped: " + ex.Message);
            }
            finally { Monitor.Exit(_gate); }
        }
    }
}
