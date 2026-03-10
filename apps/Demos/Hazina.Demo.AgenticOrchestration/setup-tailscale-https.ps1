#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Setup Tailscale HTTPS certificates for Hazina Orchestration Demo
.DESCRIPTION
    Downloads Tailscale certificates and configures the app for HTTPS access over Tailscale network
.PARAMETER Hostname
    The Tailscale hostname (default: desktop-ecbaunu.tailca9ff1.ts.net)
#>

param(
    [string]$Hostname = "desktop-ecbaunu.tailca9ff1.ts.net"
)

$ErrorActionPreference = "Stop"

Write-Host "🔐 Hazina Orchestration - Tailscale HTTPS Setup" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Create certs directory
$certsDir = Join-Path $PSScriptRoot "certs"
if (-not (Test-Path $certsDir)) {
    Write-Host "📁 Creating certs directory..." -ForegroundColor Yellow
    New-Item -Path $certsDir -ItemType Directory | Out-Null
    Write-Host "   ✅ Created: $certsDir" -ForegroundColor Green
}

# Step 2: Request Tailscale certificate
Write-Host ""
Write-Host "🌐 Requesting Tailscale certificate for: $Hostname" -ForegroundColor Yellow
Write-Host "   Running: tailscale cert $Hostname" -ForegroundColor Gray

try {
    $certOutput = tailscale cert $Hostname 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ Certificate obtained successfully!" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Failed to obtain certificate" -ForegroundColor Red
        Write-Host "   Output: $certOutput" -ForegroundColor Gray
        exit 1
    }
} catch {
    Write-Host "   ❌ Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "⚠️  Make sure Tailscale is installed and you're logged in:" -ForegroundColor Yellow
    Write-Host "   - Install: https://tailscale.com/download" -ForegroundColor Gray
    Write-Host "   - Login: tailscale login" -ForegroundColor Gray
    exit 1
}

# Step 3: Find certificate files
Write-Host ""
Write-Host "📋 Locating certificate files..." -ForegroundColor Yellow

# Tailscale usually saves certs in current directory or temp location
$certFile = Get-ChildItem -Path . -Filter "$Hostname.crt" -ErrorAction SilentlyContinue | Select-Object -First 1
$keyFile = Get-ChildItem -Path . -Filter "$Hostname.key" -ErrorAction SilentlyContinue | Select-Object -First 1

if (-not $certFile) {
    # Try common Tailscale cert locations
    $possiblePaths = @(
        "$env:USERPROFILE\.tailscale\certs",
        "$env:TEMP",
        $PWD
    )

    foreach ($path in $possiblePaths) {
        $certFile = Get-ChildItem -Path $path -Filter "$Hostname.crt" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($certFile) { break }
    }
}

if (-not $keyFile) {
    $possiblePaths = @(
        "$env:USERPROFILE\.tailscale\certs",
        "$env:TEMP",
        $PWD
    )

    foreach ($path in $possiblePaths) {
        $keyFile = Get-ChildItem -Path $path -Filter "$Hostname.key" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($keyFile) { break }
    }
}

if (-not $certFile -or -not $keyFile) {
    Write-Host "   ❌ Certificate files not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please locate the following files manually:" -ForegroundColor Yellow
    Write-Host "   - $Hostname.crt" -ForegroundColor Gray
    Write-Host "   - $Hostname.key" -ForegroundColor Gray
    Write-Host ""
    Write-Host "And copy them to: $certsDir" -ForegroundColor Yellow
    exit 1
}

# Step 4: Copy certificates
Write-Host "   ✅ Found certificate: $($certFile.FullName)" -ForegroundColor Green
Write-Host "   ✅ Found key: $($keyFile.FullName)" -ForegroundColor Green
Write-Host ""
Write-Host "📦 Copying certificates to certs directory..." -ForegroundColor Yellow

Copy-Item -Path $certFile.FullName -Destination (Join-Path $certsDir "$Hostname.crt") -Force
Copy-Item -Path $keyFile.FullName -Destination (Join-Path $certsDir "$Hostname.key") -Force

Write-Host "   ✅ Certificates copied successfully!" -ForegroundColor Green

# Step 5: Update password
Write-Host ""
Write-Host "🔑 Security Setup" -ForegroundColor Yellow
Write-Host "   Current password in appsettings.Tailscale.json: 'changeme'" -ForegroundColor Gray
Write-Host ""
$updatePassword = Read-Host "   Do you want to update the admin password? (y/n)"

if ($updatePassword -eq 'y' -or $updatePassword -eq 'Y') {
    $newPassword = Read-Host "   Enter new password" -AsSecureString
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($newPassword)
    )

    $configPath = Join-Path $PSScriptRoot "appsettings.Tailscale.json"
    $config = Get-Content $configPath -Raw | ConvertFrom-Json
    $config.Authentication.Password = $plainPassword
    $config | ConvertTo-Json -Depth 10 | Set-Content $configPath

    Write-Host "   ✅ Password updated!" -ForegroundColor Green
}

# Step 6: Instructions
Write-Host ""
Write-Host "✨ Setup Complete!" -ForegroundColor Green
Write-Host "=================" -ForegroundColor Green
Write-Host ""
Write-Host "To start the app with Tailscale HTTPS:" -ForegroundColor Cyan
Write-Host ""
Write-Host "   cd C:\Projects\hazina\apps\Demos\Hazina.Demo.AgenticOrchestration" -ForegroundColor White
Write-Host "   `$env:ASPNETCORE_ENVIRONMENT='Tailscale'" -ForegroundColor White
Write-Host "   dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "Then access via:" -ForegroundColor Cyan
Write-Host "   https://$Hostname:5123/" -ForegroundColor White
Write-Host ""
Write-Host "Login credentials:" -ForegroundColor Cyan
Write-Host "   Username: admin" -ForegroundColor White
Write-Host "   Password: $(if ($updatePassword -eq 'y') { '<your new password>' } else { 'changeme' })" -ForegroundColor White
Write-Host ""
