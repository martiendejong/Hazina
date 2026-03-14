$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishDir = Join-Path $scriptDir "..\Hazina.Demo.AgenticOrchestration\bin\Release\net9.0-windows\win-x64\publish"
$wixDir = Join-Path $scriptDir "wix-tools"
$candleExe = Join-Path $wixDir "candle.exe"
$lightExe = Join-Path $wixDir "light.exe"
$objDir = Join-Path $scriptDir "obj\Release"
$outDir = Join-Path $scriptDir "bin\Release"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  Simple MSI Build - User Folder Installation" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""

# Verify publish directory exists
if (-not (Test-Path $publishDir)) {
    Write-Host "[FATAL] Publish directory not found: $publishDir" -ForegroundColor Red
    exit 1
}

Write-Host "Using published files from: $publishDir" -ForegroundColor Gray
Write-Host ""

# Create output dirs
New-Item -ItemType Directory -Path $objDir -Force | Out-Null
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

Push-Location $scriptDir

Write-Host "Compiling WiX (candle.exe)..." -ForegroundColor Cyan
& $candleExe "Product.wxs" `
    -ext WixUIExtension `
    -ext WixUtilExtension `
    -dPublishDir="$publishDir" `
    -out "$objDir\" 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAILED] candle.exe failed" -ForegroundColor Red
    Pop-Location
    exit 1
}

Write-Host "Linking MSI (light.exe)..." -ForegroundColor Cyan
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$msiName = "HazinaOrchestrationSetup-$timestamp.msi"

& $lightExe "$objDir\Product.wixobj" `
    -ext WixUIExtension `
    -ext WixUtilExtension `
    -out "$outDir\$msiName" `
    -sval 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAILED] light.exe failed" -ForegroundColor Red
    Pop-Location
    exit 1
}

Pop-Location

$msi = Join-Path $outDir $msiName
$size = [math]::Round((Get-Item $msi).Length / 1MB, 2)

Write-Host ""
Write-Host "==================================================================" -ForegroundColor Green
Write-Host "  BUILD COMPLETE" -ForegroundColor Green
Write-Host "==================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  MSI: $msi" -ForegroundColor White
Write-Host "  Size: $size MB" -ForegroundColor White
Write-Host ""
Write-Host "  Installation:" -ForegroundColor Gray
Write-Host "    - InstallScope: perUser (no admin required)" -ForegroundColor Gray
Write-Host "    - Location: %LOCALAPPDATA%\Hazina\Orchestration\" -ForegroundColor Gray
Write-Host "    - No Windows Service (runs as tray app)" -ForegroundColor Gray
Write-Host "    - Start Menu shortcut created" -ForegroundColor Gray
Write-Host ""
