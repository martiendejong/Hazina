# Build-All-Platforms.ps1
# Build release artifacts for all platforms (Windows, macOS, Linux)

param(
    [string]$Version = "1.0.0",
    [switch]$SkipWindows,
    [switch]$SkipMacOS,
    [switch]$SkipLinux,
    [switch]$ComputeHashes
)

$ErrorActionPreference = "Stop"

# Paths
$RepoRoot = (Get-Item $PSScriptRoot).Parent.Parent.Parent.FullName
$ProjectPath = Join-Path $RepoRoot "src\Hazina.AgenticOrchestration\Hazina.AgenticOrchestration.csproj"
$InstallerPath = $PSScriptRoot
$OutputDir = Join-Path $InstallerPath "release-artifacts"

Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   Hazina Orchestration - Multi-Platform Build                 ║" -ForegroundColor Cyan
Write-Host "║   Version: $Version                                              ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Create output directory
if (Test-Path $OutputDir) {
    Write-Host "🗑️  Cleaning previous build artifacts..." -ForegroundColor Yellow
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir | Out-Null

# ============================================================================
# Windows MSI
# ============================================================================

if (-not $SkipWindows) {
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "  Building Windows MSI Installer" -ForegroundColor Green
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host ""

    try {
        # Run existing MSI build script
        $MsiBuildScript = Join-Path $InstallerPath "Build-MSI-Complete.ps1"

        if (-not (Test-Path $MsiBuildScript)) {
            Write-Host "❌ Build-MSI-Complete.ps1 not found!" -ForegroundColor Red
            exit 1
        }

        & $MsiBuildScript

        # Copy MSI to output directory
        $MsiPath = Join-Path $InstallerPath "bin\Release\HazinaOrchestration.msi"
        if (Test-Path $MsiPath) {
            Copy-Item $MsiPath -Destination $OutputDir
            Write-Host "✅ Windows MSI: $OutputDir\HazinaOrchestration.msi" -ForegroundColor Green
        } else {
            Write-Host "❌ MSI build failed - file not found at $MsiPath" -ForegroundColor Red
            exit 1
        }
    }
    catch {
        Write-Host "❌ Windows MSI build failed: $_" -ForegroundColor Red
        exit 1
    }
}

# ============================================================================
# macOS Archive
# ============================================================================

if (-not $SkipMacOS) {
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "  Building macOS Archive" -ForegroundColor Green
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host ""

    try {
        $MacOSOutput = Join-Path $OutputDir "osx-x64"

        Write-Host "🔨 Publishing for macOS (osx-x64)..." -ForegroundColor Cyan
        dotnet publish $ProjectPath `
            -c Release `
            -r osx-x64 `
            --self-contained `
            -o $MacOSOutput `
            /p:PublishSingleFile=true `
            /p:PublishTrimmed=false

        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed with exit code $LASTEXITCODE"
        }

        # Create tar.gz
        Write-Host "📦 Creating tar.gz archive..." -ForegroundColor Cyan
        $TarFile = Join-Path $OutputDir "hazina-orchestration-osx-x64.tar.gz"

        Push-Location $MacOSOutput
        try {
            # Use tar if available (Git Bash, WSL, native Windows tar)
            if (Get-Command tar -ErrorAction SilentlyContinue) {
                tar -czf $TarFile *
            } else {
                # Fallback: use 7-Zip or other archiver
                Write-Host "⚠️  'tar' command not found. Please install Git Bash or use WSL." -ForegroundColor Yellow
                Write-Host "   Archive must be created manually from: $MacOSOutput" -ForegroundColor Yellow
            }
        }
        finally {
            Pop-Location
        }

        if (Test-Path $TarFile) {
            Write-Host "✅ macOS Archive: $TarFile" -ForegroundColor Green

            # Clean up temp directory
            Remove-Item $MacOSOutput -Recurse -Force
        } else {
            Write-Host "⚠️  macOS archive not created (tar command unavailable)" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "❌ macOS build failed: $_" -ForegroundColor Red
        exit 1
    }
}

# ============================================================================
# Linux Archive
# ============================================================================

if (-not $SkipLinux) {
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "  Building Linux Archive" -ForegroundColor Green
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host ""

    try {
        $LinuxOutput = Join-Path $OutputDir "linux-x64"

        Write-Host "🔨 Publishing for Linux (linux-x64)..." -ForegroundColor Cyan
        dotnet publish $ProjectPath `
            -c Release `
            -r linux-x64 `
            --self-contained `
            -o $LinuxOutput `
            /p:PublishSingleFile=true `
            /p:PublishTrimmed=false

        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed with exit code $LASTEXITCODE"
        }

        # Create tar.gz
        Write-Host "📦 Creating tar.gz archive..." -ForegroundColor Cyan
        $TarFile = Join-Path $OutputDir "hazina-orchestration-linux-x64.tar.gz"

        Push-Location $LinuxOutput
        try {
            if (Get-Command tar -ErrorAction SilentlyContinue) {
                tar -czf $TarFile *
            } else {
                Write-Host "⚠️  'tar' command not found. Archive must be created manually." -ForegroundColor Yellow
                Write-Host "   From: $LinuxOutput" -ForegroundColor Yellow
            }
        }
        finally {
            Pop-Location
        }

        if (Test-Path $TarFile) {
            Write-Host "✅ Linux Archive: $TarFile" -ForegroundColor Green

            # Clean up temp directory
            Remove-Item $LinuxOutput -Recurse -Force
        } else {
            Write-Host "⚠️  Linux archive not created (tar command unavailable)" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "❌ Linux build failed: $_" -ForegroundColor Red
        exit 1
    }
}

# ============================================================================
# Compute Hashes
# ============================================================================

if ($ComputeHashes) {
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "  Computing SHA256 Hashes" -ForegroundColor Green
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host ""

    $HashFile = Join-Path $OutputDir "SHA256SUMS.txt"

    Get-ChildItem $OutputDir -File | ForEach-Object {
        $Hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
        $FileName = $_.Name

        Write-Host "$FileName" -ForegroundColor Cyan
        Write-Host "  $Hash" -ForegroundColor Gray

        # Append to hash file
        "$Hash  $FileName" | Add-Content $HashFile
    }

    Write-Host ""
    Write-Host "✅ Hashes written to: $HashFile" -ForegroundColor Green
}

# ============================================================================
# Summary
# ============================================================================

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                    Build Complete!                             ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "Release artifacts:" -ForegroundColor White
Get-ChildItem $OutputDir -File | ForEach-Object {
    $SizeMB = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  📦 $($_.Name) ($SizeMB MB)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Test installers on each platform" -ForegroundColor Gray
Write-Host "  2. Update Homebrew formula SHA256 hash" -ForegroundColor Gray
Write-Host "  3. Create GitHub release:" -ForegroundColor Gray
Write-Host "     git tag -a v$Version -m 'Release v$Version'" -ForegroundColor Gray
Write-Host "     git push origin v$Version" -ForegroundColor Gray
Write-Host "     gh release create v$Version release-artifacts/* --title 'v$Version' --notes-file RELEASE_NOTES.md" -ForegroundColor Gray
Write-Host ""
