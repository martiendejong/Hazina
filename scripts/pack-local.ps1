# PowerShell script to pack all Hazina packages to local NuGet feed
# Usage: .\scripts\pack-local.ps1 [-Configuration Release] [-Version 1.0.0-local]

param(
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",

    [Parameter(Mandatory=$false)]
    [string]$Version = "",

    [Parameter(Mandatory=$false)]
    [string]$LocalFeedPath = "C:\nuget-local",

    [Parameter(Mandatory=$false)]
    [switch]$SkipBuild = $false
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Hazina Local NuGet Package Builder" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Local Feed: $LocalFeedPath" -ForegroundColor Yellow
if ($Version) {
    Write-Host "Version Override: $Version" -ForegroundColor Yellow
}
Write-Host ""

# Ensure local feed directory exists
if (!(Test-Path $LocalFeedPath)) {
    Write-Host "Creating local NuGet feed directory: $LocalFeedPath" -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $LocalFeedPath | Out-Null
}

# Check if local feed is in NuGet sources
$sources = dotnet nuget list source | Out-String
if ($sources -notlike "*$LocalFeedPath*") {
    Write-Host "Adding local NuGet source..." -ForegroundColor Cyan
    dotnet nuget add source $LocalFeedPath --name Local
    Write-Host "✓ Local source added" -ForegroundColor Green
} else {
    Write-Host "✓ Local NuGet source already configured" -ForegroundColor Green
}
Write-Host ""

# Build solution first (unless skipped)
if (!$SkipBuild) {
    Write-Host "Step 1: Building solution..." -ForegroundColor Cyan
    dotnet build Hazina.sln --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "✓ Build succeeded!" -ForegroundColor Green
    Write-Host ""
}

Write-Host "Step 2: Packing packages to local feed..." -ForegroundColor Cyan
Write-Host ""

# Find all .csproj files in src directory
$projects = Get-ChildItem -Path "src" -Filter "*.csproj" -Recurse

$successCount = 0
$failCount = 0
$skippedCount = 0

foreach ($project in $projects) {
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project.Name)

    # Skip test projects
    if ($projectName -like "*Tests*" -or $projectName -like "*Test") {
        $skippedCount++
        continue
    }

    Write-Host "Packing: $projectName" -ForegroundColor White

    try {
        $packArgs = @(
            "pack"
            $project.FullName
            "--configuration", $Configuration
            "--no-build"
            "--output", $LocalFeedPath
        )

        # Add version override if specified
        if ($Version) {
            $packArgs += "/p:Version=$Version"
            $packArgs += "/p:PackageVersion=$Version"
        }

        & dotnet @packArgs

        if ($LASTEXITCODE -eq 0) {
            $packageVersion = if ($Version) { $Version } else { "default" }
            Write-Host "✓ Packed: $projectName [$packageVersion]" -ForegroundColor Green
            $successCount++
        } else {
            Write-Host "✗ Failed to pack: $projectName" -ForegroundColor Red
            $failCount++
        }
    } catch {
        Write-Host "✗ Error packing $projectName : $_" -ForegroundColor Red
        $failCount++
    }

    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Packing Summary:" -ForegroundColor Cyan
Write-Host "  Success: $successCount" -ForegroundColor Green
Write-Host "  Failed: $failCount" -ForegroundColor Red
Write-Host "  Skipped (tests): $skippedCount" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($failCount -gt 0) {
    Write-Host "⚠ Some packages failed to pack" -ForegroundColor Yellow
    exit 1
}

Write-Host "✓ All packages packed successfully to: $LocalFeedPath" -ForegroundColor Green
Write-Host ""
Write-Host "To use these packages in your project:" -ForegroundColor Cyan
Write-Host "  dotnet add package Hazina --source Local" -ForegroundColor White
Write-Host "  dotnet restore" -ForegroundColor White
Write-Host ""
