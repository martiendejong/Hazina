#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Start Hazina Orchestration Demo with Tailscale HTTPS
.DESCRIPTION
    Quick launcher for running the orchestration demo with Tailscale configuration
#>

param(
    [switch]$Build
)

$ErrorActionPreference = "Stop"

Write-Host "🚀 Starting Hazina Orchestration with Tailscale HTTPS" -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

# Check if certificates exist
$certsDir = Join-Path $PSScriptRoot "certs"
$certFile = Join-Path $certsDir "desktop-ecbaunu.tailca9ff1.ts.net.crt"
$keyFile = Join-Path $certsDir "desktop-ecbaunu.tailca9ff1.ts.net.key"

if (-not (Test-Path $certFile) -or -not (Test-Path $keyFile)) {
    Write-Host "❌ Certificates not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please run the setup script first:" -ForegroundColor Yellow
    Write-Host "   .\setup-tailscale-https.ps1" -ForegroundColor White
    Write-Host ""
    exit 1
}

Write-Host "✅ Certificates found" -ForegroundColor Green
Write-Host ""

# Set environment
$env:ASPNETCORE_ENVIRONMENT = "Tailscale"

if ($Build) {
    Write-Host "🔨 Building application..." -ForegroundColor Yellow
    dotnet build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host ""
}

Write-Host "🌐 Starting server..." -ForegroundColor Yellow
Write-Host "   Environment: Tailscale" -ForegroundColor Gray
Write-Host "   URL: https://desktop-ecbaunu.tailca9ff1.ts.net:5123/" -ForegroundColor Gray
Write-Host "   Local: https://localhost:5123/" -ForegroundColor Gray
Write-Host ""
Write-Host "Press Ctrl+C to stop" -ForegroundColor Yellow
Write-Host ""

dotnet run
