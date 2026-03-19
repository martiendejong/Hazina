<#
.SYNOPSIS
    Builds Hazina Agentic Orchestration MSI installer (FIXED VERSION - No Unicode issues)

.DESCRIPTION
    This script:
    1. Publishes the ASP.NET Core app as a self-contained Windows executable
    2. Builds the WiX installer to create an MSI package
    3. The MSI installs the app as a Windows Service on port 5123

.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Release

.PARAMETER Platform
    Target platform (win-x64, win-arm64). Default: win-x64

.EXAMPLE
    .\Build-MSI-Fixed.ps1
    .\Build-MSI-Fixed.ps1 -Configuration Debug
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("win-x64", "win-arm64")]
    [string]$Platform = "win-x64"
)

$ErrorActionPreference = "Stop"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "     Hazina Agentic Orchestration - MSI Build Script" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host ""

# Paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
$appProjectPath = Join-Path $projectDir "Hazina.Demo.AgenticOrchestration.csproj"
$installerProjectPath = Join-Path $scriptDir "Hazina.Demo.AgenticOrchestration.Installer.wixproj"
$publishDir = Join-Path $projectDir "bin\$Configuration\net9.0\$Platform\publish"
$msiOutputDir = Join-Path $scriptDir "bin\$Configuration"

Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Platform: $Platform" -ForegroundColor Yellow
Write-Host "App Project: $appProjectPath" -ForegroundColor Gray
Write-Host "Installer Project: $installerProjectPath" -ForegroundColor Gray
Write-Host "Publish Directory: $publishDir" -ForegroundColor Gray
Write-Host "MSI Output: $msiOutputDir" -ForegroundColor Gray
Write-Host ""

# Step 1: Publish the ASP.NET Core application
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "STEP 1: Publishing ASP.NET Core application..." -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$publishArgs = @(
    "publish"
    $appProjectPath
    "--configuration", $Configuration
    "--runtime", $Platform
    "--self-contained", "true"
    "/p:PublishSingleFile=true"
    "/p:IncludeNativeLibrariesForSelfExtract=true"
    "/p:EnableCompressionInSingleFile=true"
    "--output", $publishDir
)

Write-Host "Running: dotnet $($publishArgs -join ' ')" -ForegroundColor Gray
& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAILED] Failed to publish application" -ForegroundColor Red
    exit 1
}

Write-Host "[SUCCESS] Application published successfully" -ForegroundColor Green
Write-Host ""

# Verify publish output
if (-not (Test-Path (Join-Path $publishDir "HazinaOrchestration.exe"))) {
    Write-Host "[FAILED] HazinaOrchestration.exe not found in publish directory" -ForegroundColor Red
    exit 1
}

$exeFile = Get-Item (Join-Path $publishDir "HazinaOrchestration.exe")
Write-Host "Published executable: $($exeFile.Name) ($([math]::Round($exeFile.Length / 1MB, 2)) MB)" -ForegroundColor Gray
Write-Host ""

# Step 2: Build the WiX installer
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "STEP 2: Building WiX MSI installer..." -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

# Check if WiX is installed
$wixPath = "${env:WIX}bin\candle.exe"
if (-not (Test-Path $wixPath)) {
    Write-Host "[FAILED] WiX Toolset not found." -ForegroundColor Red
    Write-Host "Expected location: $wixPath" -ForegroundColor Yellow
    Write-Host "" -ForegroundColor Yellow
    Write-Host "SOLUTION: Install WiX Toolset v3.14" -ForegroundColor Yellow
    Write-Host "  1. Run: .\install-wix.ps1" -ForegroundColor Gray
    Write-Host "  2. Or download manually: https://github.com/wixtoolset/wix3/releases/download/wix314rtm/wix314.exe" -ForegroundColor Gray
    Write-Host "  3. Close and reopen PowerShell after installation" -ForegroundColor Gray
    exit 1
}

Write-Host "[SUCCESS] WiX Toolset found: $wixPath" -ForegroundColor Green
Write-Host ""

# Create output directory if not exists
if (-not (Test-Path $msiOutputDir)) {
    New-Item -ItemType Directory -Path $msiOutputDir -Force | Out-Null
}

# Build with candle + light (more reliable than MSBuild)
Write-Host "Compiling WiX source files..." -ForegroundColor Gray

Push-Location $scriptDir

$candleArgs = @(
    "Product.wxs"
    "-out", "obj\$Configuration\"
    "-ext", "WixUtilExtension"
    "-ext", "WixUIExtension"
    "-dPublishDir=$publishDir"
)

& $wixPath @candleArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAILED] Failed to compile WiX source (candle.exe)" -ForegroundColor Red
    Pop-Location
    exit 1
}

Write-Host "[SUCCESS] WiX source compiled" -ForegroundColor Green
Write-Host ""

Write-Host "Linking WiX object files..." -ForegroundColor Gray

$lightPath = "${env:WIX}bin\light.exe"
$lightArgs = @(
    "obj\$Configuration\Product.wixobj"
    "-out", "$msiOutputDir\HazinaOrchestrationSetup.msi"
    "-ext", "WixUtilExtension"
    "-ext", "WixUIExtension"
    "-sval"  # Suppress ICE validation warnings
)

& $lightPath @lightArgs

Pop-Location

if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAILED] Failed to link WiX object (light.exe)" -ForegroundColor Red
    exit 1
}

Write-Host "[SUCCESS] MSI built successfully" -ForegroundColor Green
Write-Host ""

# Step 3: Verify MSI output
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "BUILD COMPLETE" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host ""

$msiPath = Join-Path $msiOutputDir "HazinaOrchestrationSetup.msi"
if (Test-Path $msiPath) {
    $msiFile = Get-Item $msiPath
    Write-Host "[SUCCESS] MSI Installer created successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "   Location: $($msiFile.FullName)" -ForegroundColor White
    Write-Host "   Size: $([math]::Round($msiFile.Length / 1MB, 2)) MB" -ForegroundColor White
    Write-Host ""
    Write-Host "Installation Instructions:" -ForegroundColor Yellow
    Write-Host "  1. Run MSI as Administrator" -ForegroundColor Gray
    Write-Host "  2. The service will be installed as 'HazinaOrchestration'" -ForegroundColor Gray
    Write-Host "  3. Access the web UI at: http://localhost:5123" -ForegroundColor Gray
    Write-Host "  4. Manage the service with: " -ForegroundColor Gray
    Write-Host "     - Start:   sc start HazinaOrchestration" -ForegroundColor DarkGray
    Write-Host "     - Stop:    sc stop HazinaOrchestration" -ForegroundColor DarkGray
    Write-Host "     - Status:  sc query HazinaOrchestration" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "Distribution:" -ForegroundColor Yellow
    Write-Host "  You can now share this MSI with others for easy installation" -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Host "[FAILED] MSI file not found at expected location: $msiPath" -ForegroundColor Red
    exit 1
}
