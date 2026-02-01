# WigiDash Widget Development - Deploy Script
# Automatiza build e deploy do widget Stream Deck Mirror para o WigiDash

param(
    [string]$Configuration = "Debug",
    [switch]$RestartWigiDash = $false
)

# Paths
$projectPath = "$PSScriptRoot"
$widgetGuid = "B7E4D1A2-5C8F-4E9B-A3D6-1F2E3B4C5D6E"
$widgetDllName = "$widgetGuid.dll"
$wigiDashWidgetsPath = "$env:APPDATA\G.SKILL\WigiDashManager\Widgets"

Write-Host "=== WigiDash Stream Deck Mirror Widget Deploy ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host ""

# Step 1: Build
Write-Host "[1/4] Building project..." -ForegroundColor Green
dotnet build "$projectPath\StreamDeckMirrorWidget.csproj" -c $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Step 1.5: Stop WigiDash (Critical for file access)
Write-Host "[1.5/4] Checks for running WigiDash..." -ForegroundColor Green
$wigiDashProcess = Get-Process -Name "WigiDashManager" -ErrorAction SilentlyContinue

if ($wigiDashProcess) {
    if ($RestartWigiDash) {
        Write-Host "Stopping WigiDash to release file locks..." -ForegroundColor Yellow
        Stop-Process -Name "WigiDashManager" -Force
        Start-Sleep -Seconds 2
    }
    else {
        Write-Host "WARNING: WigiDash is running. File copy might fail." -ForegroundColor Red
        Write-Host "Use -RestartWigiDash to auto-close it, or close manually." -ForegroundColor Yellow
    }
}

# Step 2: Prepare widget folder
$targetFolder = Join-Path $wigiDashWidgetsPath $widgetGuid

if (-not (Test-Path $wigiDashWidgetsPath)) {
    Write-Host "WigiDash Widgets folder not found: $wigiDashWidgetsPath" -ForegroundColor Red
    Write-Host "Make sure WigiDash is installed." -ForegroundColor Yellow
    exit 1
}

Write-Host "[2/4] Preparing widget folder..." -ForegroundColor Green
if (Test-Path $targetFolder) {
    try {
        Remove-Item $targetFolder -Recurse -Force -ErrorAction Stop
    }
    catch {
        Write-Host "Failed to clean folder. WigiDash might still be locking files." -ForegroundColor Red
        if (-not $RestartWigiDash) {
            Write-Host "Try running with -RestartWigiDash" -ForegroundColor Cyan
        }
        exit 1
    }
}
New-Item -ItemType Directory -Path $targetFolder -Force | Out-Null

# Step 3: Copy files
Write-Host "[3/4] Copying widget files..." -ForegroundColor Green
$sourcePath = "$projectPath\bin\$Configuration\net472"

if (-not (Test-Path $sourcePath)) {
    Write-Host "Build output not found: $sourcePath" -ForegroundColor Red
    exit 1
}

# Copy main DLL
Copy-Item "$sourcePath\$widgetDllName" $targetFolder -Force

# Copy dependencies (excluding the main DLL and system assemblies)
Get-ChildItem "$sourcePath\*.dll" | Where-Object {
    $_.Name -ne $widgetDllName -and
    $_.Name -notlike "System.*" -and
    $_.Name -notlike "Microsoft.*" -and
    $_.Name -ne "netstandard.dll"
} | ForEach-Object {
    Copy-Item $_.FullName $targetFolder -Force
}

# Copy icon from Resources folder or root
if (Test-Path "$projectPath\Resources\icon.png") {
    Copy-Item "$projectPath\Resources\icon.png" $targetFolder -Force
    Write-Host "Copied icon.png from Resources" -ForegroundColor Green
}
elseif (Test-Path "$projectPath\icon.png") {
    Copy-Item "$projectPath\icon.png" $targetFolder -Force
    Write-Host "Copied icon.png from root" -ForegroundColor Green
}
else {
    Write-Host "WARNING: icon.png not found. Widget will use fallback icon." -ForegroundColor Yellow
}

Write-Host "Widget deployed to: $targetFolder" -ForegroundColor Cyan

# Step 4: Restart WigiDash (optional)
if ($RestartWigiDash) {
    Write-Host "[4/4] Restarting WigiDash..." -ForegroundColor Green

    # Try to start WigiDash
    $wigiDashExe = "C:\Program Files (x86)\G.SKILL\WigiDash Manager\WigiDashManager.exe"
    if (Test-Path $wigiDashExe) {
        Write-Host "Starting WigiDash..." -ForegroundColor Yellow
        Start-Process $wigiDashExe
    }
    else {
        Write-Host "WigiDashManager.exe not found at: $wigiDashExe" -ForegroundColor Yellow
        Write-Host "Please restart WigiDash manually." -ForegroundColor Yellow
    }
}
else {
    Write-Host "[4/4] Skipped WigiDash restart" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To load the widget, restart WigiDash manually or run:" -ForegroundColor Cyan
    Write-Host "  .\deploy.ps1 -RestartWigiDash" -ForegroundColor White
}

Write-Host ""
Write-Host "=== Deploy Complete! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Widget Path: $targetFolder" -ForegroundColor Cyan
Write-Host "Widget GUID: $widgetGuid" -ForegroundColor Cyan
