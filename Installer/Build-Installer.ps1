param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [switch]$SkipPublish,
    [switch]$Verbose
)

<#
.SYNOPSIS
    Builds the Hazina Task Runner MSI installer

.DESCRIPTION
    Publishes the WPF application and builds the WiX MSI installer package

.PARAMETER Configuration
    Build configuration (Release or Debug)

.PARAMETER SkipPublish
    Skip the dotnet publish step (use existing publish folder)

.PARAMETER Verbose
    Show detailed build output

.EXAMPLE
    .\Build-Installer.ps1
    Builds Release MSI

.EXAMPLE
    .\Build-Installer.ps1 -Configuration Debug -Verbose
    Builds Debug MSI with verbose output
#>

$ErrorActionPreference = "Stop"

Write-Host "`n=== Hazina Task Runner - MSI Build Script ===" -ForegroundColor Cyan
Write-Host ""

# Paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionDir = Split-Path -Parent $scriptDir
$uiProjectPath = Join-Path $solutionDir "src\Hazina.TaskRunner.UI\Hazina.TaskRunner.UI.csproj"
$publishDir = Join-Path $solutionDir "src\Hazina.TaskRunner.UI\bin\$Configuration\net8.0-windows\win-x64\publish"
$installerProjectPath = Join-Path $scriptDir "Hazina.TaskRunner.Installer.wixproj"
$outputDir = Join-Path $scriptDir "bin\x64\$Configuration"

Write-Host "Configuration: $Configuration" -ForegroundColor White
Write-Host "Solution Directory: $solutionDir" -ForegroundColor Gray
Write-Host "Publish Directory: $publishDir" -ForegroundColor Gray
Write-Host "Output Directory: $outputDir" -ForegroundColor Gray
Write-Host ""

# Step 1: Publish the WPF application
if (-not $SkipPublish) {
    Write-Host "[1/3] Publishing WPF Application..." -ForegroundColor Cyan

    $publishArgs = @(
        "publish"
        $uiProjectPath
        "-c", $Configuration
        "-r", "win-x64"
        "--self-contained", "false"
        "-p:PublishSingleFile=false"
        "-p:PublishReadyToRun=false"
    )

    if ($Verbose) {
        $publishArgs += "-v", "detailed"
    }

    & dotnet @publishArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to publish application" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "  Published to: $publishDir" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "[1/3] Skipping publish (using existing files)" -ForegroundColor Yellow
    Write-Host ""
}

# Step 2: Verify publish directory exists
if (-not (Test-Path $publishDir)) {
    Write-Host "Publish directory not found: $publishDir" -ForegroundColor Red
    Write-Host "Run without -SkipPublish to create it" -ForegroundColor Yellow
    exit 1
}

# List published files
$publishedFiles = Get-ChildItem $publishDir -Recurse -File
Write-Host "Published files: $($publishedFiles.Count)" -ForegroundColor White
if ($Verbose) {
    foreach ($file in $publishedFiles | Select-Object -First 10) {
        Write-Host "  $($file.Name)" -ForegroundColor Gray
    }
    if ($publishedFiles.Count -gt 10) {
        Write-Host "  ... and $($publishedFiles.Count - 10) more" -ForegroundColor Gray
    }
}
Write-Host ""

# Step 3: Check if WiX is installed
Write-Host "[2/3] Checking WiX Toolset..." -ForegroundColor Cyan

$wixInstalled = $false
try {
    $wixVersion = & dotnet wix --version 2>&1
    if ($LASTEXITCODE -eq 0) {
        $wixInstalled = $true
        Write-Host "  WiX Toolset found: $wixVersion" -ForegroundColor Green
    }
} catch {
    # WiX not installed
}

if (-not $wixInstalled) {
    Write-Host "  WiX Toolset not found. Installing..." -ForegroundColor Yellow
    & dotnet tool install --global wix

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to install WiX Toolset" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "  WiX Toolset installed successfully" -ForegroundColor Green
}
Write-Host ""

# Step 4: Build the MSI
Write-Host "[3/3] Building MSI Installer..." -ForegroundColor Cyan

$buildArgs = @(
    "build"
    $installerProjectPath
    "-c", $Configuration
    "-p:UIBinPath=$publishDir"
)

if ($Verbose) {
    $buildArgs += "-v", "detailed"
}

& dotnet @buildArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build MSI" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Green
Write-Host ""

# Find the MSI file
$msiFiles = Get-ChildItem $outputDir -Filter "*.msi" -ErrorAction SilentlyContinue

if ($msiFiles) {
    Write-Host "MSI created successfully:" -ForegroundColor Green
    foreach ($msi in $msiFiles) {
        $sizeKB = [Math]::Round($msi.Length / 1KB, 2)
        Write-Host "  $($msi.FullName)" -ForegroundColor White
        Write-Host "  Size: $sizeKB KB" -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "To install:" -ForegroundColor Cyan
    Write-Host "  msiexec /i `"$($msiFiles[0].FullName)`"" -ForegroundColor White
    Write-Host ""
    Write-Host "To install silently:" -ForegroundColor Cyan
    Write-Host "  msiexec /i `"$($msiFiles[0].FullName)`" /quiet" -ForegroundColor White
} else {
    Write-Host "Warning: MSI file not found in $outputDir" -ForegroundColor Yellow
}

Write-Host ""
