# Generate Documentation for Hazina Framework
# This script generates API documentation using DocFX

param(
    [switch]$Serve = $false,
    [switch]$Clean = $false,
    [int]$Port = 8080
)

$ErrorActionPreference = "Stop"

Write-Host "Hazina Documentation Generator" -ForegroundColor Cyan
Write-Host "==============================`n" -ForegroundColor Cyan

# Check if docfx is installed
$docfxInstalled = dotnet tool list | Select-String "docfx"
if (-not $docfxInstalled) {
    Write-Host "DocFX not found. Installing..." -ForegroundColor Yellow
    dotnet tool restore
}

# Clean previous build if requested
if ($Clean) {
    Write-Host "Cleaning previous documentation build..." -ForegroundColor Yellow
    if (Test-Path "docs/_site") {
        Remove-Item -Recurse -Force "docs/_site"
    }
    if (Test-Path "docs/api") {
        Remove-Item -Recurse -Force "docs/api"
    }
    Write-Host "[OK] Cleaned`n" -ForegroundColor Green
}

# Step 1: Generate metadata (API references from code)
Write-Host "Step 1: Generating API metadata from source code..." -ForegroundColor Cyan
try {
    dotnet docfx metadata docfx.json
    Write-Host "[OK] Metadata generated`n" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Metadata generation failed: $_" -ForegroundColor Red
    exit 1
}

# Step 2: Build documentation site
Write-Host "Step 2: Building documentation site..." -ForegroundColor Cyan
try {
    dotnet docfx build docfx.json
    Write-Host "[OK] Documentation site built`n" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Documentation build failed: $_" -ForegroundColor Red
    exit 1
}

# Display output location
Write-Host "Documentation generated successfully!" -ForegroundColor Green
Write-Host "Output location: docs/_site/index.html" -ForegroundColor White

# Serve documentation if requested
if ($Serve) {
    Write-Host "`nStarting documentation server on http://localhost:$Port..." -ForegroundColor Cyan
    Write-Host "Press Ctrl+C to stop the server`n" -ForegroundColor Yellow
    dotnet docfx serve docs/_site --port $Port
} else {
    Write-Host "`nTo preview the documentation, run:" -ForegroundColor Cyan
    Write-Host "  .\generate-docs.ps1 -Serve" -ForegroundColor White
    Write-Host "  or" -ForegroundColor White
    Write-Host "  dotnet docfx serve docs/_site --port $Port" -ForegroundColor White
}
