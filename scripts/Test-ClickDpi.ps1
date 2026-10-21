param(
    [string]$WidgetDll = (Join-Path $PSScriptRoot '..\bin\Debug\net472\B7E4D1A2-5C8F-4E9B-A3D6-1F2E3B4C5D6E.dll')
)

# Run with Windows PowerShell. All clicks go to an invisible test window owned
# by this process, never to Stream Deck or another application.
$ErrorActionPreference = 'Stop'
[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $WidgetDll).Path) | Out-Null
Add-Type -AssemblyName System.Windows.Forms
Add-Type -ReferencedAssemblies System.Windows.Forms -TypeDefinition @'
using System;
using System.Windows.Forms;
public sealed class ClickDpiProbe : NativeWindow, IDisposable
{
    public int DownX = -1, DownY = -1, UpX = -1, UpY = -1;
    public ClickDpiProbe()
    {
        var parameters = new CreateParams();
        parameters.Caption = "StreamDeckMirror input integration check";
        parameters.Style = unchecked((int)0x80000000); // Hidden, borderless popup.
        parameters.X = 100; parameters.Y = 100;
        parameters.Width = 128; parameters.Height = 128;
        CreateHandle(parameters);
    }
    protected override void WndProc(ref Message message)
    {
        long coordinates = message.LParam.ToInt64();
        if (message.Msg == 0x0201 || message.Msg == 0x0204) {
            DownX = (short)(coordinates & 0xffff);
            DownY = (short)((coordinates >> 16) & 0xffff);
        }
        if (message.Msg == 0x0202 || message.Msg == 0x0205) {
            UpX = (short)(coordinates & 0xffff);
            UpY = (short)((coordinates >> 16) & 0xffff);
        }
        base.WndProc(ref message);
    }
    public void Reset() { DownX = DownY = UpX = UpY = -1; }
    public void Dispose() { DestroyHandle(); }
}
'@

$creationPrevious = [WigiDash.StreamDeckMirrorWidget.NativeMethods]::SetThreadDpiAwarenessContext([IntPtr](-3))
try { $probe = New-Object ClickDpiProbe }
finally { [WigiDash.StreamDeckMirrorWidget.NativeMethods]::SetThreadDpiAwarenessContext($creationPrevious) | Out-Null }
$capture = New-Object WigiDash.StreamDeckMirrorWidget.WindowCapture
$capture.TargetHwnd = $probe.Handle
try {
    foreach ($mode in @(-1, -2, -3)) {
        $previous = [WigiDash.StreamDeckMirrorWidget.NativeMethods]::SetThreadDpiAwarenessContext([IntPtr]$mode)
        try {
            foreach ($rightClick in @($false, $true)) {
                $probe.Reset()
                $before = [WigiDash.StreamDeckMirrorWidget.NativeMethods]::GetThreadDpiAwarenessContext()
                $capture.SendClick(23, 29, $rightClick, $null)
                $after = [WigiDash.StreamDeckMirrorWidget.NativeMethods]::GetThreadDpiAwarenessContext()
                if (-not [WigiDash.StreamDeckMirrorWidget.NativeMethods]::AreDpiAwarenessContextsEqual($before, $after)) {
                    throw 'Click dispatch did not restore caller DPI context.'
                }
                [Windows.Forms.Application]::DoEvents()
                if ($probe.DownX -ne 23 -or $probe.DownY -ne 29 -or $probe.UpX -ne 23 -or $probe.UpY -ne 29) {
                    throw "Unexpected coordinates: down=$($probe.DownX),$($probe.DownY); up=$($probe.UpX),$($probe.UpY)"
                }
                Write-Output "PASS: sender context $mode; rightClick=$rightClick; test window received (23,29); caller DPI restored."
            }
        }
        finally { [WigiDash.StreamDeckMirrorWidget.NativeMethods]::SetThreadDpiAwarenessContext($previous) | Out-Null }
    }
}
finally { $capture.Dispose(); $probe.Dispose() }
