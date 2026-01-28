#!/usr/bin/env pwsh
# Hazina CLI Release Builder
# Creates distributable packages for Windows and Linux
# Usage: ./build-release.ps1 [-Version "1.0.0"] [-Platform "all|win|linux"]

param(
    [string]$Version = "",
    [ValidateSet("all", "win", "linux")]
    [string]$Platform = "all",
    [string]$OutputDir = "./release-artifacts",
    [switch]$SkipBuild,
    [switch]$CreateInstaller
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path "$ScriptDir/../..").Path
$ProjectPath = "$RepoRoot/apps/CLI/Hazina.App.HazinaCoder/Hazina.App.HazinaCoder.csproj"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Hazina CLI - Release Builder" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Get version from project file if not specified
if ([string]::IsNullOrEmpty($Version)) {
    [xml]$csproj = Get-Content $ProjectPath
    $Version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if ([string]::IsNullOrEmpty($Version)) {
        $Version = "1.0.0"
    }
}

Write-Host "Building version: $Version" -ForegroundColor Green
Write-Host "Output directory: $OutputDir" -ForegroundColor Gray
Write-Host ""

# Create output directories
$ArtifactsDir = Join-Path $RepoRoot $OutputDir
$WinDir = "$ArtifactsDir/win-x64"
$LinuxDir = "$ArtifactsDir/linux-x64"
$InstallerDir = "$ArtifactsDir/installers"

New-Item -ItemType Directory -Force -Path $ArtifactsDir | Out-Null
New-Item -ItemType Directory -Force -Path $WinDir | Out-Null
New-Item -ItemType Directory -Force -Path $LinuxDir | Out-Null
New-Item -ItemType Directory -Force -Path $InstallerDir | Out-Null

# Runtime identifiers
$Runtimes = @()
if ($Platform -eq "all" -or $Platform -eq "win") {
    $Runtimes += @{ RID = "win-x64"; Dir = $WinDir; Ext = ".exe"; Name = "Windows x64" }
}
if ($Platform -eq "all" -or $Platform -eq "linux") {
    $Runtimes += @{ RID = "linux-x64"; Dir = $LinuxDir; Ext = ""; Name = "Linux x64" }
}

if (-not $SkipBuild) {
    foreach ($runtime in $Runtimes) {
        Write-Host "Building for $($runtime.Name)..." -ForegroundColor Yellow
        Write-Host "  Runtime: $($runtime.RID)" -ForegroundColor Gray

        $publishArgs = @(
            "publish"
            $ProjectPath
            "-c", "Release"
            "-r", $runtime.RID
            "--self-contained", "true"
            "-p:PublishSingleFile=true"
            "-p:IncludeNativeLibrariesForSelfExtract=true"
            "-p:EnableCompressionInSingleFile=true"
            "-p:DebugType=embedded"
            "-p:Version=$Version"
            "-o", $runtime.Dir
        )

        & dotnet @publishArgs

        if ($LASTEXITCODE -ne 0) {
            Write-Host "  FAILED to build for $($runtime.Name)" -ForegroundColor Red
            exit 1
        }

        # Get the executable
        $exeName = "hazinacoder$($runtime.Ext)"
        $exePath = Join-Path $runtime.Dir $exeName

        if (Test-Path $exePath) {
            $size = (Get-Item $exePath).Length / 1MB
            Write-Host "  SUCCESS: $exeName ($([math]::Round($size, 2)) MB)" -ForegroundColor Green
        } else {
            Write-Host "  WARNING: Executable not found at $exePath" -ForegroundColor Yellow
        }

        Write-Host ""
    }
}

# Create ZIP archives
Write-Host "Creating distribution archives..." -ForegroundColor Yellow

foreach ($runtime in $Runtimes) {
    $archiveName = "hazinacoder-$Version-$($runtime.RID).zip"
    $archivePath = Join-Path $InstallerDir $archiveName

    Write-Host "  Creating $archiveName..." -ForegroundColor Gray

    # Remove old archive if exists
    if (Test-Path $archivePath) {
        Remove-Item $archivePath -Force
    }

    # Create ZIP (include only the essential files)
    $filesToInclude = @(
        "hazinacoder$($runtime.Ext)"
        "appsettings.json"
    )

    Push-Location $runtime.Dir
    try {
        Compress-Archive -Path $filesToInclude -DestinationPath $archivePath -Force
        $zipSize = (Get-Item $archivePath).Length / 1MB
        Write-Host "  Created: $archiveName ($([math]::Round($zipSize, 2)) MB)" -ForegroundColor Green
    } catch {
        Write-Host "  Failed to create archive: $_" -ForegroundColor Red
    }
    Pop-Location
}

Write-Host ""

# Create Linux installer script
if ($Platform -eq "all" -or $Platform -eq "linux") {
    Write-Host "Creating Linux installer script..." -ForegroundColor Yellow

    $linuxInstallerPath = Join-Path $InstallerDir "install-hazinacoder.sh"
    $linuxInstallerContent = @"
#!/bin/bash
# Hazina CLI Installer for Linux
# Usage: curl -fsSL https://martiendejong.nl/hazina/install.sh | bash

set -e

VERSION="${Version}"
INSTALL_DIR="\${HOME}/.hazina"
BIN_DIR="\${HOME}/.local/bin"

echo "================================================"
echo "  Hazina CLI Installer"
echo "  Version: \$VERSION"
echo "================================================"
echo ""

# Detect architecture
ARCH=\$(uname -m)
case \$ARCH in
    x86_64)
        PLATFORM="linux-x64"
        ;;
    aarch64|arm64)
        PLATFORM="linux-arm64"
        echo "Warning: ARM64 builds may not be available yet"
        ;;
    *)
        echo "Error: Unsupported architecture: \$ARCH"
        exit 1
        ;;
esac

echo "Detected platform: \$PLATFORM"
echo ""

# Create directories
echo "Creating directories..."
mkdir -p "\$INSTALL_DIR"
mkdir -p "\$BIN_DIR"

# Download URL
DOWNLOAD_URL="https://github.com/martiendejong/hazina/releases/download/v\$VERSION/hazinacoder-\$VERSION-\$PLATFORM.zip"
TEMP_FILE="/tmp/hazinacoder-\$VERSION.zip"

echo "Downloading Hazina CLI..."
echo "  URL: \$DOWNLOAD_URL"

if command -v curl &> /dev/null; then
    curl -fsSL "\$DOWNLOAD_URL" -o "\$TEMP_FILE"
elif command -v wget &> /dev/null; then
    wget -q "\$DOWNLOAD_URL" -O "\$TEMP_FILE"
else
    echo "Error: curl or wget required"
    exit 1
fi

echo "Extracting..."
unzip -o "\$TEMP_FILE" -d "\$INSTALL_DIR"

echo "Setting permissions..."
chmod +x "\$INSTALL_DIR/hazinacoder"

echo "Creating symlink..."
ln -sf "\$INSTALL_DIR/hazinacoder" "\$BIN_DIR/hazinacoder"

# Cleanup
rm -f "\$TEMP_FILE"

echo ""
echo "================================================"
echo "  Installation Complete!"
echo "================================================"
echo ""
echo "Hazina CLI has been installed to: \$INSTALL_DIR"
echo ""

# Check if bin is in PATH
if [[ ":\$PATH:" != *":\$BIN_DIR:"* ]]; then
    echo "NOTE: Add the following to your shell profile (.bashrc, .zshrc, etc.):"
    echo ""
    echo "  export PATH=\"\$BIN_DIR:\\\$PATH\""
    echo ""
fi

echo "To get started, run:"
echo ""
echo "  hazinacoder --help"
echo ""
echo "Or start an interactive session:"
echo ""
echo "  hazinacoder"
echo ""
"@

    Set-Content -Path $linuxInstallerPath -Value $linuxInstallerContent -NoNewline
    Write-Host "  Created: install-hazinacoder.sh" -ForegroundColor Green
}

# Create Windows Inno Setup script
if (($Platform -eq "all" -or $Platform -eq "win") -and $CreateInstaller) {
    Write-Host "Creating Windows installer (Inno Setup)..." -ForegroundColor Yellow

    $innoScriptPath = Join-Path $ScriptDir "hazinacoder-setup.iss"
    $innoScriptContent = @"
; Hazina CLI Installer Script for Inno Setup
; Compile with: iscc hazinacoder-setup.iss

#define MyAppName "Hazina CLI"
#define MyAppVersion "$Version"
#define MyAppPublisher "Hazina"
#define MyAppURL "https://martiendejong.nl/hazina"
#define MyAppExeName "hazinacoder.exe"

[Setup]
AppId={{8A1B2C3D-4E5F-6789-ABCD-EF0123456789}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\Hazina
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\..\LICENSE
OutputDir=..\release-artifacts\installers
OutputBaseFilename=hazinacoder-{#MyAppVersion}-win-x64-setup
SetupIconFile=..\..\assets\hazina-icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ChangesEnvironment=yes
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "addtopath"; Description: "Add to PATH (recommended)"; GroupDescription: "Additional options:"; Flags: checkedonce

[Files]
Source: "..\release-artifacts\win-x64\hazinacoder.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\release-artifacts\win-x64\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

[Registry]
Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; Tasks: addtopath; Check: NeedsAddPath('{app}')

[Code]
function NeedsAddPath(Param: string): boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', OrigPath) then
  begin
    Result := True;
    exit;
  end;
  Result := Pos(';' + Param + ';', ';' + OrigPath + ';') = 0;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    // Notify the system about the PATH change
    RegWriteStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', '');
  end;
end;
"@

    Set-Content -Path $innoScriptPath -Value $innoScriptContent -NoNewline
    Write-Host "  Created: hazinacoder-setup.iss" -ForegroundColor Green

    # Try to compile if Inno Setup is installed
    $isccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    if (Test-Path $isccPath) {
        Write-Host "  Compiling installer..." -ForegroundColor Gray
        & $isccPath $innoScriptPath
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  Installer created successfully!" -ForegroundColor Green
        } else {
            Write-Host "  Failed to compile installer (exit code: $LASTEXITCODE)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  Note: Inno Setup not found. Install from https://jrsoftware.org/isinfo.php" -ForegroundColor Yellow
        Write-Host "  Then run: iscc $innoScriptPath" -ForegroundColor Gray
    }
}

# Summary
Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Release Build Complete!" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Version: $Version" -ForegroundColor White
Write-Host ""
Write-Host "Artifacts created in: $ArtifactsDir" -ForegroundColor Gray
Write-Host ""

Write-Host "Distribution files:" -ForegroundColor Yellow
Get-ChildItem $InstallerDir | ForEach-Object {
    $size = $_.Length / 1MB
    Write-Host "  - $($_.Name) ($([math]::Round($size, 2)) MB)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Create GitHub release: gh release create v$Version" -ForegroundColor Gray
Write-Host "  2. Upload artifacts: gh release upload v$Version $InstallerDir/*" -ForegroundColor Gray
Write-Host "  3. Update website download page" -ForegroundColor Gray
Write-Host ""
