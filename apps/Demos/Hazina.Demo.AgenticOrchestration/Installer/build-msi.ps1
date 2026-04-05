#!/usr/bin/env pwsh
# Build MSI installer for Hazina Orchestration
# Prerequisites: WiX Toolset v3.11 or newer must be installed

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# WiX toolset paths
$WixPath = "C:\Program Files (x86)\WiX Toolset v3.14\bin"
$Candle = Join-Path $WixPath "candle.exe"
$Light = Join-Path $WixPath "light.exe"

# Check WiX installation
if (-not (Test-Path $Candle)) {
    $WixPath = "C:\Program Files (x86)\WiX Toolset v3.11\bin"
    $Candle = Join-Path $WixPath "candle.exe"
    $Light = Join-Path $WixPath "light.exe"
}

if (-not (Test-Path $Candle)) {
    Write-Error "WiX Toolset not found. Please install from https://wixtoolset.org/releases/"
    exit 1
}

Write-Host "Using WiX from: $WixPath" -ForegroundColor Cyan

# Paths
$InstallerDir = $PSScriptRoot
$ProjectDir = Split-Path $InstallerDir -Parent
$PublishDir = Join-Path $ProjectDir "publish\release"
$OutputDir = Join-Path $InstallerDir "bin\$Configuration"
$IntermediateDir = Join-Path $InstallerDir "obj\$Configuration"

# Create output directories
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
New-Item -ItemType Directory -Force -Path $IntermediateDir | Out-Null

# Check if app is published
if (-not (Test-Path (Join-Path $PublishDir "HazinaOrchestration.exe"))) {
    Write-Error "Application not published. Run 'dotnet publish' first."
    Write-Host "Suggested command:" -ForegroundColor Yellow
    Write-Host "  cd '$ProjectDir'" -ForegroundColor Yellow
    Write-Host "  dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/release" -ForegroundColor Yellow
    exit 1
}

Write-Host "`n=== Building Hazina Orchestration MSI Installer ===" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "Publish Dir:   $PublishDir" -ForegroundColor Cyan
Write-Host "Output Dir:    $OutputDir" -ForegroundColor Cyan

# Step 1: Compile WiX source
Write-Host "`n[1/2] Compiling WiX source..." -ForegroundColor Yellow
$WxsFile = Join-Path $InstallerDir "Product.wxs"
$WixObjFile = Join-Path $IntermediateDir "Product.wixobj"

$candleArgs = @(
    "-nologo"
    "-ext", "WixUIExtension"
    "-dPublishDir=$PublishDir\"
    "-out", $WixObjFile
    $WxsFile
)

& $Candle $candleArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Candle compilation failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "  Compilation successful!" -ForegroundColor Green

# Step 2: Link to create MSI
Write-Host "`n[2/2] Linking MSI..." -ForegroundColor Yellow
$MsiFile = Join-Path $OutputDir "HazinaOrchestration-2.5.0.msi"

$lightArgs = @(
    "-nologo"
    "-ext", "WixUIExtension"
    "-cultures:en-US"
    "-out", $MsiFile
    $WixObjFile
)

& $Light $lightArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Light linking failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "  Linking successful!" -ForegroundColor Green

# Show results
Write-Host "`n=== Build Complete ===" -ForegroundColor Green
Write-Host "MSI Installer: $MsiFile" -ForegroundColor Cyan
$MsiSize = (Get-Item $MsiFile).Length / 1MB
Write-Host "Size: $([math]::Round($MsiSize, 2)) MB" -ForegroundColor Cyan

# Offer to open folder
Write-Host "`nOpening output folder..." -ForegroundColor Yellow
Start-Process explorer.exe -ArgumentList $OutputDir

Write-Host "`nTo install:" -ForegroundColor Green
Write-Host "  msiexec /i `"$MsiFile`"" -ForegroundColor Cyan
Write-Host "`nOr just double-click the MSI file." -ForegroundColor Green
