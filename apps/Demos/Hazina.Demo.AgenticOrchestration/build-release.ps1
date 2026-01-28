<#
.SYNOPSIS
    Build Hazina Agentic Orchestration executables for Windows and Linux.

.DESCRIPTION
    This script builds self-contained, single-file executables for:
    - Windows x64 (HazinaOrchestration.exe)
    - Linux x64 (HazinaOrchestration)

    The executables include:
    - .NET runtime (no .NET installation required)
    - React frontend (pre-built)
    - All dependencies

.PARAMETER Platform
    Target platform: 'all', 'windows', or 'linux'. Default: 'all'

.PARAMETER Configuration
    Build configuration: 'Release' or 'Debug'. Default: 'Release'

.PARAMETER OutputDir
    Output directory for built artifacts. Default: './publish'

.PARAMETER SkipFrontend
    Skip building the React frontend (use existing wwwroot)

.PARAMETER CreateArchive
    Create zip/tar.gz archives of the output

.EXAMPLE
    .\build-release.ps1
    Builds for all platforms with defaults

.EXAMPLE
    .\build-release.ps1 -Platform windows -CreateArchive
    Builds only Windows executable and creates a zip archive
#>

param(
    [ValidateSet('all', 'windows', 'linux')]
    [string]$Platform = 'all',

    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [string]$OutputDir = './publish',

    [switch]$SkipFrontend,

    [switch]$CreateArchive
)

$ErrorActionPreference = 'Stop'
$ProjectDir = $PSScriptRoot
$ProjectFile = Join-Path $ProjectDir 'Hazina.Demo.AgenticOrchestration.csproj'
$ClientAppDir = Join-Path $ProjectDir 'ClientApp'

# Banner
Write-Host ""
Write-Host "=======================================================================" -ForegroundColor Cyan
Write-Host "      HAZINA AGENTIC ORCHESTRATION - Release Builder                   " -ForegroundColor Cyan
Write-Host "=======================================================================" -ForegroundColor Cyan
Write-Host "  Platform: $Platform" -ForegroundColor Cyan
Write-Host "  Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "  Output: $OutputDir" -ForegroundColor Cyan
Write-Host "=======================================================================" -ForegroundColor Cyan
Write-Host ""

# Create output directory
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# Step 1: Build React frontend
if (-not $SkipFrontend) {
    Write-Host "[FRONTEND] Building React frontend..." -ForegroundColor Yellow

    Push-Location $ClientAppDir
    try {
        Write-Host "   Installing npm dependencies..."
        npm install --silent 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "npm install failed" }

        Write-Host "   Building production bundle..."
        npm run build 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "npm build failed" }

        Write-Host "   [OK] Frontend built successfully" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Host "[SKIP] Skipping frontend build (using existing wwwroot)" -ForegroundColor Gray
}

# Function to publish for a specific runtime
function Publish-Platform {
    param(
        [string]$RuntimeId,
        [string]$FriendlyName
    )

    Write-Host ""
    Write-Host "[BUILD] Building for $FriendlyName ($RuntimeId)..." -ForegroundColor Yellow

    $PlatformOutputDir = Join-Path $OutputDir $RuntimeId

    # Clean previous build
    if (Test-Path $PlatformOutputDir) {
        Remove-Item -Recurse -Force $PlatformOutputDir
    }

    # Publish
    $publishArgs = @(
        'publish'
        $ProjectFile
        '--configuration', $Configuration
        '--runtime', $RuntimeId
        '--output', $PlatformOutputDir
        '--self-contained', 'true'
        '-p:PublishSingleFile=true'
        '-p:EnableCompressionInSingleFile=true'
        '-p:IncludeNativeLibrariesForSelfExtract=true'
        '-p:DebugType=none'
        '-p:DebugSymbols=false'
    )

    dotnet @publishArgs 2>&1 | ForEach-Object {
        if ($_ -match 'error') {
            Write-Host "   $_" -ForegroundColor Red
        }
        elseif ($_ -match 'warning') {
            Write-Host "   $_" -ForegroundColor Yellow
        }
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Publishing for $RuntimeId failed"
    }

    # Get output file info
    $exeName = if ($RuntimeId -like 'win*') { 'HazinaOrchestration.exe' } else { 'HazinaOrchestration' }
    $exePath = Join-Path $PlatformOutputDir $exeName

    if (Test-Path $exePath) {
        $fileInfo = Get-Item $exePath
        $sizeMB = [math]::Round($fileInfo.Length / 1MB, 2)
        Write-Host "   [OK] Built: $exeName ($sizeMB MB)" -ForegroundColor Green

        # Create archive if requested
        if ($CreateArchive) {
            $archiveName = "HazinaOrchestration-$RuntimeId"

            if ($RuntimeId -like 'win*') {
                $archivePath = Join-Path $OutputDir "$archiveName.zip"
                Write-Host "   [ARCHIVE] Creating: $archiveName.zip"
                Compress-Archive -Path "$PlatformOutputDir\*" -DestinationPath $archivePath -Force
            }
            else {
                $archivePath = Join-Path $OutputDir "$archiveName.tar.gz"
                Write-Host "   [ARCHIVE] Creating: $archiveName.tar.gz"
                Push-Location $OutputDir
                try {
                    tar -czf "$archiveName.tar.gz" -C $RuntimeId .
                }
                finally {
                    Pop-Location
                }
            }

            $archiveInfo = Get-Item $archivePath
            $archiveSizeMB = [math]::Round($archiveInfo.Length / 1MB, 2)
            Write-Host "   [OK] Archive created ($archiveSizeMB MB)" -ForegroundColor Green
        }
    }
    else {
        throw "Expected executable not found: $exePath"
    }
}

# Step 2: Build for selected platforms
try {
    if ($Platform -eq 'all' -or $Platform -eq 'windows') {
        Publish-Platform -RuntimeId 'win-x64' -FriendlyName 'Windows x64'
    }

    if ($Platform -eq 'all' -or $Platform -eq 'linux') {
        Publish-Platform -RuntimeId 'linux-x64' -FriendlyName 'Linux x64'
    }
}
catch {
    Write-Host ""
    Write-Host "[ERROR] Build failed: $_" -ForegroundColor Red
    exit 1
}

# Summary
Write-Host ""
Write-Host "=======================================================================" -ForegroundColor Green
Write-Host "                     BUILD COMPLETE                                    " -ForegroundColor Green
Write-Host "=======================================================================" -ForegroundColor Green
Write-Host "  Output directory: $OutputDir" -ForegroundColor Green
Write-Host "=======================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "[CONTENTS]" -ForegroundColor Cyan

Get-ChildItem $OutputDir -Recurse -File | Where-Object {
    $_.Name -like 'HazinaOrchestration*' -or $_.Extension -in '.zip', '.gz'
} | ForEach-Object {
    $relativePath = $_.FullName.Replace($OutputDir, '').TrimStart('\', '/')
    $sizeMB = [math]::Round($_.Length / 1MB, 2)
    Write-Host "   $relativePath ($sizeMB MB)"
}

Write-Host ""
Write-Host "[RUN] To execute:" -ForegroundColor Yellow
Write-Host "   Windows: .\publish\win-x64\HazinaOrchestration.exe"
Write-Host "   Linux:   ./publish/linux-x64/HazinaOrchestration"
Write-Host ""
