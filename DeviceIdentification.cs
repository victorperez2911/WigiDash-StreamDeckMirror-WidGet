using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace WigiDash.StreamDeckMirrorWidget
{
    /// <summary>
    /// Information about a Stream Deck Virtual Device window
    /// </summary>
    public class StreamDeckWindowInfo
    {
        /// <summary>Window handle</summary>
        public IntPtr Hwnd { get; set; }

        /// <summary>Window title</summary>
        public string Title { get; set; }

        /// <summary>Window class name (Qt class)</summary>
        public string ClassName { get; set; }

        /// <summary>Window width</summary>
        public int Width { get; set; }

        /// <summary>Window height</summary>
        public int Height { get; set; }

        /// <summary>
        /// Display text for dropdown/selection UI
        /// Format: "Stream Deck (536x662)"
        /// </summary>
        public string DisplayText => $"{Title} ({Width}x{Height})";

        /// <summary>
        /// Display text with index for duplicate devices
        /// Format: "Stream Deck (536x662) #1"
        /// </summary>
        public string GetDisplayTextWithIndex(int index)
        {
            return $"{Title} ({Width}x{Height}) #{index + 1}";
        }
    }

    /// <summary>
    /// Persisted data to identify a device across sessions
    /// </summary>
    public class DeviceIdentifier
    {
        /// <summary>Window title at selection time</summary>
        public string Title { get; set; }

        /// <summary>Window width at selection time</summary>
        public int Width { get; set; }

        /// <summary>Window height at selection time</summary>
        public int Height { get; set; }

        /// <summary>
        /// Index if multiple identical devices exist
        /// (e.g., two "Stream Deck" windows of 536x662 - which one?)
        /// </summary>
        public int Index { get; set; }

        /// <summary>Qt window class name for validation</summary>
        public string ClassName { get; set; }

        /// <summary>
        /// Check if this identifier is valid (has required data)
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(Title) && Width > 0 && Height > 0;

        /// <summary>
        /// Create identifier from window info
        /// </summary>
        public static DeviceIdentifier FromWindowInfo(StreamDeckWindowInfo info, int index = 0)
        {
            return new DeviceIdentifier
            {
                Title = info.Title,
                Width = info.Width,
                Height = info.Height,
                Index = index,
                ClassName = info.ClassName
            };
        }
    }

    /// <summary>
    /// Helper class for finding Stream Deck windows
    /// </summary>
    public static class StreamDeckWindowFinder
    {
        // Cache for StreamDeck process IDs to avoid repeated process enumeration
        private static HashSet<uint> _cachedProcessIds;
        private static DateTime _cacheTime = DateTime.MinValue;
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Get cached StreamDeck process IDs
        /// </summary>
        private static HashSet<uint> GetStreamDeckProcessIds()
        {
            // Return cached IDs if still valid
            if (_cachedProcessIds != null && (DateTime.Now - _cacheTime) < CacheExpiry)
            {
                return _cachedProcessIds;
            }

            // Refresh cache
            _cachedProcessIds = new HashSet<uint>();
            try
            {
                var processes = Process.GetProcessesByName("StreamDeck");
                foreach (var proc in processes)
                {
                    _cachedProcessIds.Add((uint)proc.Id);
                    proc.Dispose();
                }
            }
            catch { }

            _cacheTime = DateTime.Now;
            return _cachedProcessIds;
        }

        /// <summary>
        /// Find all Stream Deck Virtual Device windows
        /// </summary>
        /// <returns>List of found windows</returns>
        public static List<StreamDeckWindowInfo> FindAllStreamDeckWindows()
        {
            var windows = new List<StreamDeckWindowInfo>();

            // Get StreamDeck process IDs upfront (cached)
            var streamDeckPids = GetStreamDeckProcessIds();
            if (streamDeckPids.Count == 0)
                return windows; // StreamDeck not running

            NativeMethods.EnumWindows((hwnd, lParam) =>
            {
                // FILTER 1: Check process ID (fast lookup in HashSet)
                NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId);
                if (!streamDeckPids.Contains(processId))
                    return true; // Not a StreamDeck window, skip

                // FILTER 2: Check Qt window class (fast check before title)
                var sbClass = new StringBuilder(256);
                NativeMethods.GetClassName(hwnd, sbClass, 256);
                string className = sbClass.ToString();

                // Virtual Devices use Qt window class
                if (!className.StartsWith("Qt", StringComparison.OrdinalIgnoreCase))
                    return true; // Not a Qt window, skip

                // FILTER 3: Check window has title
                var sbTitle = new StringBuilder(256);
                NativeMethods.GetWindowText(hwnd, sbTitle, 256);
                string title = sbTitle.ToString();

                if (string.IsNullOrWhiteSpace(title))
                    return true; // No title, skip

                // FILTER 4: Check valid dimensions
                NativeMethods.GetWindowRect(hwnd, out var rect);
                if (rect.Width <= 0 || rect.Height <= 0)
                    return true; // Invalid dimensions, skip

                // Passed all filters - add to list
                windows.Add(new StreamDeckWindowInfo
                {
                    Hwnd = hwnd,
                    Title = title,
                    ClassName = className,
                    Width = rect.Width,
                    Height = rect.Height
                });

                return true; // Continue enumeration
            }, IntPtr.Zero);

            return windows;
        }

        /// <summary>
        /// Find a previously saved device
        /// </summary>
        /// <param name="saved">Saved device identifier</param>
        /// <returns>Window handle, or IntPtr.Zero if not found</returns>
        public static IntPtr FindSavedDevice(DeviceIdentifier saved)
        {
            if (saved == null || !saved.IsValid)
                return IntPtr.Zero;

            // Find all current windows
            var windows = FindAllStreamDeckWindows();

            if (windows.Count == 0)
                return IntPtr.Zero; // App not running

            // Filter by title and dimensions
            var matches = windows
                .Where(w => w.Title == saved.Title &&
                           w.Width == saved.Width &&
                           w.Height == saved.Height)
                .ToList();

            if (matches.Count == 0)
                return IntPtr.Zero; // Device not found

            if (matches.Count == 1)
                return matches[0].Hwnd; // Unique match

            // Multiple identical windows - use saved index
            if (saved.Index < matches.Count)
                return matches[saved.Index].Hwnd;

            // Invalid index - return first
            return matches[0].Hwnd;
        }

        /// <summary>
        /// Check if Stream Deck application is running
        /// </summary>
        public static bool IsStreamDeckRunning()
        {
            return GetStreamDeckProcessIds().Count > 0;
        }

        /// <summary>
        /// Group windows by title+dimensions to identify duplicates
        /// </summary>
        public static Dictionary<string, List<StreamDeckWindowInfo>> GroupBySignature(
            List<StreamDeckWindowInfo> windows)
        {
            return windows
                .GroupBy(w => $"{w.Title}|{w.Width}x{w.Height}")
                .ToDictionary(g => g.Key, g => g.ToList());
        }
    }
}
