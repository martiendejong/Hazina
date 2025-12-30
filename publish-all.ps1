# Publish all Hazina NuGet packages to NuGet.org
param(
    [Parameter(Mandatory=$false)]
    [string]$ApiKey = $env:NUGET_API_KEY,

    [Parameter(Mandatory=$false)]
    [string]$Source = "https://api.nuget.org/v3/index.json"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrEmpty($ApiKey)) {
    Write-Host "ERROR: NuGet API key not provided!" -ForegroundColor Red
    Write-Host "Set NUGET_API_KEY environment variable or pass -ApiKey parameter" -ForegroundColor Yellow
    exit 1
}

Write-Host "Publishing all Hazina NuGet packages..." -ForegroundColor Cyan

$packagesDir = "nupkgs"
if (!(Test-Path $packagesDir)) {
    Write-Host "ERROR: $packagesDir directory not found. Run pack-all.ps1 first!" -ForegroundColor Red
    exit 1
}

# Get all .nupkg files (exclude symbol packages)
$packages = Get-ChildItem -Path $packagesDir -Filter "*.nupkg" |
    Where-Object { $_.Name -notmatch "\.symbols\.nupkg$" }

Write-Host "Found $($packages.Count) packages to publish" -ForegroundColor Green

$successCount = 0
$failCount = 0
$skippedCount = 0

foreach ($package in $packages) {
    Write-Host ""
    Write-Host "Publishing: $($package.Name)..." -ForegroundColor Yellow

    try {
        dotnet nuget push $package.FullName `
            --api-key $ApiKey `
            --source $Source `
            --skip-duplicate

        if ($LASTEXITCODE -eq 0) {
            $successCount++
            Write-Host "  ✓ Published" -ForegroundColor Green
        } elseif ($LASTEXITCODE -eq 1) {
            $skippedCount++
            Write-Host "  ⊘ Skipped (already exists)" -ForegroundColor Yellow
        } else {
            $failCount++
            Write-Host "  ✗ Failed" -ForegroundColor Red
        }
    }
    catch {
        $failCount++
        Write-Host "  ✗ Error: $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Publishing complete!" -ForegroundColor Cyan
Write-Host "Published: $successCount" -ForegroundColor Green
Write-Host "Skipped: $skippedCount" -ForegroundColor Yellow
Write-Host "Failed: $failCount" -ForegroundColor Red
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
