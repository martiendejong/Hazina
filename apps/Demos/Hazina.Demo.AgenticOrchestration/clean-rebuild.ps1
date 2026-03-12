$ErrorActionPreference = "Stop"
Write-Host "=== FULL CLEAN ===" -ForegroundColor Cyan

$projectDir = "C:\Projects\hazina\apps\Demos\Hazina.Demo.AgenticOrchestration"
$installerDir = "C:\Projects\hazina\apps\Demos\Hazina.Demo.AgenticOrchestration.Installer"

# Clean project build artifacts
foreach ($dir in @("bin", "obj", "wwwroot")) {
    $path = Join-Path $projectDir $dir
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force
        Write-Host "  Removed: $dir/" -ForegroundColor Green
    } else {
        Write-Host "  Already clean: $dir/" -ForegroundColor Gray
    }
}

# Clean installer build artifacts
foreach ($dir in @("bin", "obj")) {
    $path = Join-Path $installerDir $dir
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force
        Write-Host "  Removed: installer/$dir/" -ForegroundColor Green
    } else {
        Write-Host "  Already clean: installer/$dir/" -ForegroundColor Gray
    }
}

# Clean NuGet caches for this project
Write-Host "`nCleaning dotnet build cache..." -ForegroundColor Cyan
dotnet clean "$projectDir\Hazina.Demo.AgenticOrchestration.csproj" --configuration Release 2>$null
dotnet clean "$projectDir\Hazina.Demo.AgenticOrchestration.csproj" --configuration Debug 2>$null
Write-Host "  dotnet clean complete" -ForegroundColor Green

Write-Host "`n=== CLEAN COMPLETE ===" -ForegroundColor Green
