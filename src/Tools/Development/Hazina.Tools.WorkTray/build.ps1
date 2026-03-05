# Build script for Work Tracker System Tray App

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Write-Host "Building Work Tracker System Tray App..." -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray

# Navigate to project directory
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $projectDir

try {
    # Restore packages
    Write-Host "`nRestoring NuGet packages..." -ForegroundColor Yellow
    dotnet restore
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to restore packages"
    }

    # Build
    Write-Host "`nBuilding project..." -ForegroundColor Yellow
    dotnet build -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }

    # Publish self-contained executable
    Write-Host "`nPublishing self-contained executable..." -ForegroundColor Yellow
    dotnet publish -c $Configuration -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed"
    }

    $outputPath = "bin\$Configuration\net8.0-windows\win-x64\publish\WorkTray.exe"

    if (Test-Path $outputPath) {
        Write-Host "`n✅ Build successful!" -ForegroundColor Green
        Write-Host "Executable: $outputPath" -ForegroundColor Gray

        # Copy to tools directory for easy access
        $targetPath = "C:\scripts\tools\WorkTray.exe"
        Copy-Item $outputPath $targetPath -Force
        Write-Host "Copied to: $targetPath" -ForegroundColor Gray

        Write-Host "`nTo run: C:\scripts\tools\WorkTray.exe" -ForegroundColor Cyan
    }
    else {
        throw "Build succeeded but executable not found"
    }
}
catch {
    Write-Host "`n❌ Build failed: $_" -ForegroundColor Red
    exit 1
}
finally {
    Pop-Location
}
