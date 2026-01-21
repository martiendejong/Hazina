# Hazina Pre-Commit Hook Setup Script
# Run this script to enable local quality gates before commits

param(
    [switch]$Uninstall
)

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

if ($Uninstall) {
    Write-Host "Removing pre-commit hook configuration..." -ForegroundColor Yellow
    git config --unset core.hooksPath
    Write-Host "✅ Pre-commit hooks disabled" -ForegroundColor Green
    exit 0
}

Write-Host "Installing Hazina pre-commit hooks..." -ForegroundColor Cyan
Write-Host "Repository: $RepoRoot" -ForegroundColor Gray

# Configure git to use the .hooks directory
git config core.hooksPath .hooks

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Pre-commit hooks installed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "The following checks will run before each commit:" -ForegroundColor White
    Write-Host "  • Build verification (AgentFactory)" -ForegroundColor Gray
    Write-Host "  • Unit tests (AgentFactory.Tests)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "To disable: .hooks/setup.ps1 -Uninstall" -ForegroundColor Yellow
} else {
    Write-Host "❌ Failed to install pre-commit hooks" -ForegroundColor Red
    exit 1
}
