<#
.SYNOPSIS
    Validates Hazina Orchestration MSI installer for required features.

.DESCRIPTION
    Automated validation script that checks:
    - Installation scope (perUser vs perMachine)
    - Installation directory (LocalAppDataFolder)
    - Launch after install functionality
    - Absence of Windows Service components
    - File inclusion completeness

.PARAMETER MsiPath
    Path to the MSI file to validate

.PARAMETER Verbose
    Show detailed validation output

.EXAMPLE
    .\Validate-Installer.ps1 -MsiPath ".\bin\Release\HazinaOrchestrationSetup-20260405.msi"

.EXAMPLE
    .\Validate-Installer.ps1 -MsiPath ".\bin\Release\HazinaOrchestrationSetup-20260405.msi" -Verbose
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$MsiPath,

    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# ============================================================================
# Validation Functions
# ============================================================================

function Test-MsiExists {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        Write-Host "❌ FAIL: MSI file not found: $Path" -ForegroundColor Red
        return $false
    }

    Write-Host "✅ PASS: MSI file exists" -ForegroundColor Green
    return $true
}

function Test-MsiSize {
    param([string]$Path)

    $sizeMB = [math]::Round((Get-Item $Path).Length / 1MB, 2)

    if ($sizeMB -lt 150) {
        Write-Host "⚠️  WARN: MSI size unusually small: $sizeMB MB (expected ~180-190 MB)" -ForegroundColor Yellow
        Write-Host "        This may indicate missing .NET runtime or files" -ForegroundColor Gray
        return $false
    }
    elseif ($sizeMB -gt 250) {
        Write-Host "⚠️  WARN: MSI size unusually large: $sizeMB MB (expected ~180-190 MB)" -ForegroundColor Yellow
        return $false
    }

    Write-Host "✅ PASS: MSI size acceptable: $sizeMB MB" -ForegroundColor Green
    return $true
}

function Test-ProductWxs {
    param([string]$InstallerDir)

    $wxsPath = Join-Path $InstallerDir "Product.wxs"

    if (-not (Test-Path $wxsPath)) {
        Write-Host "❌ FAIL: Product.wxs not found" -ForegroundColor Red
        return $false
    }

    $wxsContent = Get-Content $wxsPath -Raw
    $passed = $true

    # Test 1: InstallScope = perUser
    if ($wxsContent -match 'InstallScope\s*=\s*"perUser"') {
        Write-Host "✅ PASS: InstallScope is 'perUser'" -ForegroundColor Green
    }
    else {
        Write-Host "❌ FAIL: InstallScope is NOT 'perUser' (found perMachine or missing)" -ForegroundColor Red
        $passed = $false
    }

    # Test 2: LocalAppDataFolder used
    if ($wxsContent -match 'LocalAppDataFolder') {
        Write-Host "✅ PASS: Uses LocalAppDataFolder for installation" -ForegroundColor Green
    }
    else {
        Write-Host "❌ FAIL: Does NOT use LocalAppDataFolder (may install to Program Files)" -ForegroundColor Red
        $passed = $false
    }

    # Test 3: Launch after install
    if ($wxsContent -match 'LaunchApplication') {
        Write-Host "✅ PASS: Launch after install action exists" -ForegroundColor Green
    }
    else {
        Write-Host "❌ FAIL: NO launch after install action" -ForegroundColor Red
        $passed = $false
    }

    # Test 4: NO Windows Service components
    if ($wxsContent -match 'ServiceInstall|ServiceControl') {
        Write-Host "❌ FAIL: CRITICAL - Contains ServiceInstall/ServiceControl (causes hang)" -ForegroundColor Red
        Write-Host "        This will cause installer to hang! Remove all service components." -ForegroundColor Yellow
        $passed = $false
    }
    else {
        Write-Host "✅ PASS: NO ServiceInstall components (runs as tray app)" -ForegroundColor Green
    }

    # Test 5: Required files
    $requiredFiles = @(
        "HazinaOrchestration.exe",
        "appsettings.json",
        "appsettings.Production.json"
    )

    $missingFiles = @()
    foreach ($file in $requiredFiles) {
        if ($wxsContent -notmatch [regex]::Escape($file)) {
            $missingFiles += $file
        }
    }

    if ($missingFiles.Count -eq 0) {
        Write-Host "✅ PASS: All required files referenced in WiX" -ForegroundColor Green
    }
    else {
        Write-Host "❌ FAIL: Missing file references: $($missingFiles -join ', ')" -ForegroundColor Red
        $passed = $false
    }

    return $passed
}

function Test-BuildScript {
    param([string]$InstallerDir)

    $buildScript = Join-Path $InstallerDir "Build-Installer.ps1"

    if (-not (Test-Path $buildScript)) {
        Write-Host "❌ FAIL: Build-Installer.ps1 not found" -ForegroundColor Red
        return $false
    }

    Write-Host "✅ PASS: Build-Installer.ps1 exists" -ForegroundColor Green

    # Check if it references the wrong build script
    $content = Get-Content $buildScript -Raw
    if ($content -match 'Build-MSI-Complete') {
        Write-Host "⚠️  WARN: References archived Build-MSI-Complete.ps1 (may be outdated)" -ForegroundColor Yellow
        return $false
    }

    return $true
}

function Test-Documentation {
    param([string]$InstallerDir)

    $requiredDocs = @(
        "README-INSTALLER.md",
        "INSTALLER_ARCHITECTURE.md"
    )

    $missingDocs = @()
    foreach ($doc in $requiredDocs) {
        if (-not (Test-Path (Join-Path $InstallerDir $doc))) {
            $missingDocs += $doc
        }
    }

    if ($missingDocs.Count -eq 0) {
        Write-Host "✅ PASS: Required documentation exists" -ForegroundColor Green
        return $true
    }
    else {
        Write-Host "⚠️  WARN: Missing documentation: $($missingDocs -join ', ')" -ForegroundColor Yellow
        return $false
    }
}

# ============================================================================
# Main Validation
# ============================================================================

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     Hazina Orchestration MSI - Validation Report            ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$msiFullPath = Resolve-Path $MsiPath -ErrorAction SilentlyContinue
if (-not $msiFullPath) {
    $msiFullPath = $MsiPath
}

Write-Host "MSI Path: $msiFullPath" -ForegroundColor Gray
Write-Host ""

# Get installer directory
$installerDir = Split-Path -Parent (Resolve-Path $PSCommandPath)

# Run validations
$results = @{
    "MSI Exists" = Test-MsiExists -Path $msiFullPath
    "MSI Size" = Test-MsiSize -Path $msiFullPath
    "Product.wxs Configuration" = Test-ProductWxs -InstallerDir $installerDir
    "Build Script" = Test-BuildScript -InstallerDir $installerDir
    "Documentation" = Test-Documentation -InstallerDir $installerDir
}

# Summary
Write-Host ""
Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  VALIDATION SUMMARY" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$passed = 0
$failed = 0
$warnings = 0

foreach ($test in $results.GetEnumerator()) {
    $status = if ($test.Value) { "✅ PASS" } else { "❌ FAIL" }
    $color = if ($test.Value) { "Green" } else { "Red" }

    Write-Host "  $status - $($test.Key)" -ForegroundColor $color

    if ($test.Value) { $passed++ } else { $failed++ }
}

Write-Host ""
Write-Host "Tests: $passed passed, $failed failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host ""

# Recommendations
if ($failed -gt 0) {
    Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Yellow
    Write-Host "  RECOMMENDATIONS" -ForegroundColor Yellow
    Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "❌ Validation FAILED - Do NOT release this MSI!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Actions required:" -ForegroundColor Yellow
    Write-Host "  1. Review Product.wxs configuration" -ForegroundColor Gray
    Write-Host "  2. Ensure InstallScope='perUser'" -ForegroundColor Gray
    Write-Host "  3. Remove any ServiceInstall components" -ForegroundColor Gray
    Write-Host "  4. Rebuild using Build-Installer.ps1" -ForegroundColor Gray
    Write-Host "  5. Re-run validation" -ForegroundColor Gray
    Write-Host ""
    exit 1
}
else {
    Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "  ✅ ALL VALIDATIONS PASSED" -ForegroundColor Green
    Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host ""
    Write-Host "This MSI is ready for:" -ForegroundColor Green
    Write-Host "  ✅ Manual testing" -ForegroundColor Gray
    Write-Host "  ✅ Release to GitHub" -ForegroundColor Gray
    Write-Host "  ✅ Production deployment" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Install MSI and verify functionality" -ForegroundColor Gray
    Write-Host "  2. Test system tray settings" -ForegroundColor Gray
    Write-Host "  3. Run Setup.ps1 for configuration" -ForegroundColor Gray
    Write-Host "  4. Create GitHub release" -ForegroundColor Gray
    Write-Host ""
    exit 0
}
