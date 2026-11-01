# OpenWigiDash Widget Development - Deploy Script
# Build e deploy do widget Stream Deck Mirror para o OpenWigiDashManager.
# A versão para o manager oficial da G.Skill (net472) fica na branch legacy-net472.

param(
    [string]$Configuration = "Debug",
    [switch]$RestartManager = $false,
    [switch]$SkipBuild = $false
)

$ErrorActionPreference = 'Stop'

$projectPath = "$PSScriptRoot"
$name = "StreamDeckMirrorWidget"
# Mesma pasta que a instalação pelo nuget.org usa (Widgets\<id do pacote>), para não haver duas cópias do widget.
$packageId = "OpenWigiDash.StreamDeckMirror"
$widgetsPath = Join-Path $env:LOCALAPPDATA 'OpenWigiDash\Widgets'
$targetFolder = Join-Path $widgetsPath $packageId
$legacyFolder = Join-Path $widgetsPath $name
# Pasta do id antigo do pacote, de antes da Org OpenWigiDash.
$oldIdFolder = Join-Path $widgetsPath "victorperez2911.OpenWigiDash.StreamDeckMirror"
$staging = Join-Path $projectPath "bin\publish\$Configuration"

Write-Host "=== Stream Deck Mirror (OpenWigiDash) Deploy ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host ""

# Passo 1: build (publish leva as dependências do plugin; SDK e MudBlazor ficam com o host)
Write-Host "[1/4] Building..." -ForegroundColor Green
if (-not $SkipBuild) {
    dotnet publish "$projectPath\StreamDeckMirrorWidget.csproj" -c $Configuration -o $staging
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $staging "$name.dll"))) {
    throw "Build output not found: $staging"
}

# Passo 2: o manager segura a DLL carregada; precisa estar fechado para trocar
Write-Host "[2/4] Checking for running OpenWigiDash Manager..." -ForegroundColor Green
$manager = Get-Process -Name 'OpenWigiDashManager' -ErrorAction SilentlyContinue
if ($manager) {
    if ($RestartManager) {
        $managerPath = $manager[0].Path
        Write-Host "Stopping OpenWigiDash Manager to release file locks..." -ForegroundColor Yellow
        $manager | Stop-Process -Force
        Start-Sleep -Seconds 3
    }
    else {
        Write-Host "OpenWigiDash Manager is running. Use -RestartManager to close it automatically." -ForegroundColor Red
        exit 1
    }
}

# Passo 3: backup e cópia (as pastas antigas, Widgets\StreamDeckMirrorWidget e a do id antigo, também saem, com backup)
Write-Host "[3/4] Copying widget files..." -ForegroundColor Green
foreach ($folder in $targetFolder, $legacyFolder, $oldIdFolder) {
    if (Test-Path $folder) {
        $backupFolder = Join-Path $env:LOCALAPPDATA ("OpenWigiDash\Backups\$name\" + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
        New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
        Copy-Item -Path (Join-Path $folder '*') -Destination $backupFolder -Recurse -Force
        Write-Host "Previous widget backed up to: $backupFolder" -ForegroundColor Cyan
        Remove-Item $folder -Recurse -Force
    }
}
New-Item -ItemType Directory -Path $targetFolder -Force | Out-Null
Copy-Item -Path (Join-Path $staging '*') -Destination $targetFolder -Recurse -Force
# A pasta tem o nome do pacote, não da DLL: o host precisa do plugin.json para achar a entrada.
[IO.File]::WriteAllText((Join-Path $targetFolder 'plugin.json'), "{ `"entry`": `"$name.dll`" }")
Write-Host "Widget deployed to: $targetFolder" -ForegroundColor Cyan

# Passo 4: reabrir o manager
if ($RestartManager -and $managerPath) {
    Write-Host "[4/4] Restarting OpenWigiDash Manager..." -ForegroundColor Green
    Start-Process $managerPath -ArgumentList '--tray'
}
else {
    Write-Host "[4/4] Open the OpenWigiDash Manager to load the widget." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Deploy Complete! ===" -ForegroundColor Green
