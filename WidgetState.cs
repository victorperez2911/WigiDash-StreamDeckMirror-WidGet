namespace WigiDash.StreamDeckMirrorWidget
{
    /// <summary>
    /// Possible states of the widget
    /// </summary>
    public enum WidgetState
    {
        /// <summary>
        /// Stream Deck application is not running
        /// </summary>
        AppNotRunning,

        /// <summary>
        /// App is running but configured device was not found
        /// (may have been removed or app restarted)
        /// </summary>
        DeviceNotFound,

        /// <summary>
        /// Device found but capture failed
        /// (PrintWindow failed, window issues, etc.)
        /// </summary>
        CaptureError,

        /// <summary>
        /// No device configured yet (first run)
        /// </summary>
        NotConfigured,

        /// <summary>
        /// Everything working - displaying Virtual Device content
        /// </summary>
        Connected
    }
}
