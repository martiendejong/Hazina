<#
.SYNOPSIS
    Install WiX Toolset v3.14 using winget
#>

Write-Host "Installing WiX Toolset v3.14..." -ForegroundColor Cyan

# Check if winget is available
if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    Write-Host "❌ winget not found. Please install winget or download WiX manually from:" -ForegroundColor Red
    Write-Host "   https://github.com/wixtoolset/wix3/releases" -ForegroundColor Yellow
    exit 1
}

# Install WiX Toolset
winget install --id WiXToolset.WiXToolset --version 3.14.0 --silent --accept-package-agreements --accept-source-agreements

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ WiX Toolset installed successfully" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Close and reopen this PowerShell window (to reload environment)" -ForegroundColor Gray
    Write-Host "  2. Run: .\Build-MSI.ps1" -ForegroundColor Gray
} else {
    Write-Host "❌ Failed to install WiX Toolset" -ForegroundColor Red
    Write-Host "   Try manual installation: https://github.com/wixtoolset/wix3/releases/download/wix314rtm/wix314.exe" -ForegroundColor Yellow
    exit 1
}
