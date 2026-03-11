$scriptDir = "C:\Projects\hazina\apps\Demos\Hazina.Demo.AgenticOrchestration.Installer"
$wixDir = Join-Path $scriptDir "wix-tools"
$candleExe = Join-Path $wixDir "candle.exe"
$lightExe = Join-Path $wixDir "light.exe"
$objDir = Join-Path $scriptDir "obj\Release"
$outDir = Join-Path $scriptDir "bin\Release2"

New-Item -ItemType Directory -Path $objDir -Force | Out-Null
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

Push-Location $scriptDir

Write-Host "Compiling WiX..." -ForegroundColor Cyan
& $candleExe "Product-Generated.wxs" -ext WixUIExtension -ext WixUtilExtension -out "obj\Release\" 2>&1
if ($LASTEXITCODE -ne 0) { Write-Host "candle FAILED" -ForegroundColor Red; Pop-Location; exit 1 }

Write-Host "Linking MSI..." -ForegroundColor Cyan
& $lightExe "obj\Release\Product-Generated.wixobj" -ext WixUIExtension -ext WixUtilExtension -out "$outDir\HazinaOrchestrationSetup.msi" -sval 2>&1
if ($LASTEXITCODE -ne 0) { Write-Host "light FAILED" -ForegroundColor Red; Pop-Location; exit 1 }

Pop-Location

$msi = Join-Path $outDir "HazinaOrchestrationSetup.msi"
$size = [math]::Round((Get-Item $msi).Length / 1MB, 2)
Write-Host "MSI built: $msi ($size MB)" -ForegroundColor Green
