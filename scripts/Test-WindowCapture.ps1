param(
    [string]$WidgetDll = (Join-Path $PSScriptRoot '..\bin\Debug\net472\B7E4D1A2-5C8F-4E9B-A3D6-1F2E3B4C5D6E.dll')
)

# Run with Windows PowerShell (.NET Framework). Captures only; sends no mouse input.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $WidgetDll).Path) | Out-Null
$windows = [WigiDash.StreamDeckMirrorWidget.StreamDeckWindowFinder]::FindAllStreamDeckWindows()
if ($windows.Count -eq 0) { throw 'No Stream Deck window available for the capture integration check.' }
$window = $windows[0]
$capture = New-Object WigiDash.StreamDeckMirrorWidget.WindowCapture
$capture.TargetHwnd = $window.Hwnd
$outputDirectory = Join-Path $PSScriptRoot '..\.diagnostics\capture-check'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
try {
    # Exercise callers with different DPI contexts, including a DPI-unaware host.
    foreach ($mode in @(-1, -2, -3)) {
        $previous = [WigiDash.StreamDeckMirrorWidget.NativeMethods]::SetThreadDpiAwarenessContext([IntPtr]$mode)
        if ($previous -eq [IntPtr]::Zero) { throw 'Could not select test DPI context.' }
        try {
            $before = [WigiDash.StreamDeckMirrorWidget.NativeMethods]::GetThreadDpiAwarenessContext()
            $bitmap = $capture.Capture()
            if ($null -eq $bitmap) { throw 'Capture failed.' }
            try {
                $after = [WigiDash.StreamDeckMirrorWidget.NativeMethods]::GetThreadDpiAwarenessContext()
                if (-not [WigiDash.StreamDeckMirrorWidget.NativeMethods]::AreDpiAwarenessContextsEqual($before, $after)) {
                    throw 'Capture did not restore caller DPI context.'
                }
                $physicalPrevious = [WigiDash.StreamDeckMirrorWidget.NativeMethods]::SetThreadDpiAwarenessContext([IntPtr](-3))
                try {
                    $rect = New-Object WigiDash.StreamDeckMirrorWidget.NativeMethods+RECT
                    if (-not [WigiDash.StreamDeckMirrorWidget.NativeMethods]::GetWindowRect($window.Hwnd, [ref]$rect)) {
                        throw 'Cannot query physical dimensions.'
                    }
                }
                finally { [WigiDash.StreamDeckMirrorWidget.NativeMethods]::SetThreadDpiAwarenessContext($physicalPrevious) | Out-Null }
                if ($bitmap.Width -ne $rect.Width -or $bitmap.Height -ne $rect.Height) {
                    throw 'Captured bitmap does not match physical window dimensions.'
                }
                $bitmap.Save((Join-Path $outputDirectory "context$mode.png"), [Drawing.Imaging.ImageFormat]::Png)
                Write-Output "PASS: context $mode; physical capture $($bitmap.Width)x$($bitmap.Height); caller DPI restored."
            }
            finally { $bitmap.Dispose() }
        }
        finally { [WigiDash.StreamDeckMirrorWidget.NativeMethods]::SetThreadDpiAwarenessContext($previous) | Out-Null }
    }
}
finally { $capture.Dispose() }
if ($null -ne $capture.Capture()) { throw 'Disposed capture should return no image.' }
Write-Output 'PASS: disposed capture returns no image.'
