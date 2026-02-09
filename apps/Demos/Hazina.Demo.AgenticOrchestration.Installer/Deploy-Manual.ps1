<#
.SYNOPSIS
    Manual deployment script for Hazina Orchestration (alternative to MSI)

.DESCRIPTION
    This script:
    1. Copies the published app to C:\Program Files\Hazina Orchestration
    2. Installs it as a Windows Service
    3. Creates required directories
    4. Configures firewall

.EXAMPLE
    .\Deploy-Manual.ps1
#>

#Requires -RunAsAdministrator

param(
    [string]$InstallPath = "C:\Program Files\Hazina Orchestration",
    [string]$ServiceName = "HazinaOrchestration",
    [string]$ServiceDisplayName = "Hazina Agentic Orchestration"
)

$ErrorActionPreference = "Stop"

Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     Hazina Agentic Orchestration - Manual Deployment            ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
$publishDir = Join-Path $projectDir "bin\Release\net9.0\win-x64\publish"
$exePath = Join-Path $publishDir "HazinaOrchestration.exe"

# Verify publish directory exists
if (-not (Test-Path $exePath)) {
    Write-Host "❌ Published app not found. Please run first:" -ForegroundColor Red
    Write-Host "   dotnet publish --configuration Release --runtime win-x64 --self-contained true" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Found published app: $exePath" -ForegroundColor Green

# Step 1: Stop existing service if running
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "STEP 1: Stopping existing service (if any)..." -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Found existing service, stopping..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    Write-Host "Deleting existing service..." -ForegroundColor Yellow
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2

    Write-Host "✅ Existing service removed" -ForegroundColor Green
} else {
    Write-Host "No existing service found" -ForegroundColor Gray
}

# Step 2: Create installation directory
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "STEP 2: Creating installation directory..." -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

if (Test-Path $InstallPath) {
    Write-Host "Removing old installation..." -ForegroundColor Yellow
    Remove-Item $InstallPath -Recurse -Force
}

New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
Write-Host "✅ Created: $InstallPath" -ForegroundColor Green

# Step 3: Copy files
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "STEP 3: Copying application files..." -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

Copy-Item -Path "$publishDir\*" -Destination $InstallPath -Recurse -Force
Write-Host "✅ Application files copied" -ForegroundColor Green

# Step 4: Create required directories
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "STEP 4: Creating required directories..." -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

$dirs = @(
    "C:\scripts\_machine",
    "C:\scripts\logs",
    "C:\scripts\logs\agent-sessions"
)

foreach ($dir in $dirs) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "✅ Created: $dir" -ForegroundColor Green
    } else {
        Write-Host "✅ Already exists: $dir" -ForegroundColor Gray
    }
}

# Step 5: Register Windows Service
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "STEP 5: Registering Windows Service..." -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

$serviceExe = Join-Path $InstallPath "HazinaOrchestration.exe"
$serviceDescription = "Web-based orchestration interface for managing Claude Code CLI instances"

# Create service
sc.exe create $ServiceName `
    binPath= "`"$serviceExe`"" `
    DisplayName= "$ServiceDisplayName" `
    start= auto

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to create service" -ForegroundColor Red
    exit 1
}

# Set description
sc.exe description $ServiceName "$serviceDescription"

# Configure failure recovery (restart on failure)
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000

Write-Host "✅ Service registered: $ServiceName" -ForegroundColor Green

# Step 6: Configure firewall
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "STEP 6: Configuring firewall..." -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

# Remove old rule if exists
netsh advfirewall firewall delete rule name="Hazina Orchestration HTTP" 2>$null | Out-Null

# Add new rule
netsh advfirewall firewall add rule `
    name="Hazina Orchestration HTTP" `
    dir=in `
    action=allow `
    protocol=TCP `
    localport=5000

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Firewall rule created (port 5000)" -ForegroundColor Green
} else {
    Write-Host "⚠️  Failed to create firewall rule (non-critical)" -ForegroundColor Yellow
}

# Step 7: Start service
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "STEP 7: Starting service..." -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

Start-Service -Name $ServiceName

Start-Sleep -Seconds 3

$service = Get-Service -Name $ServiceName
if ($service.Status -eq 'Running') {
    Write-Host "✅ Service started successfully!" -ForegroundColor Green
} else {
    Write-Host "❌ Service failed to start. Status: $($service.Status)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Check Event Viewer for error details:" -ForegroundColor Yellow
    Write-Host "  eventvwr.msc → Windows Logs → Application" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Or try running manually to see errors:" -ForegroundColor Yellow
    Write-Host "  cd `"$InstallPath`"" -ForegroundColor Gray
    Write-Host "  .\HazinaOrchestration.exe" -ForegroundColor Gray
    exit 1
}

# Final success message
Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                  DEPLOYMENT COMPLETE                             ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "Service Information:" -ForegroundColor Cyan
Write-Host "  Name:         $ServiceName" -ForegroundColor White
Write-Host "  Display Name: $ServiceDisplayName" -ForegroundColor White
Write-Host "  Status:       Running" -ForegroundColor Green
Write-Host "  Install Path: $InstallPath" -ForegroundColor White
Write-Host ""
Write-Host "Access the application:" -ForegroundColor Cyan
Write-Host "  Web UI:    http://localhost:5000" -ForegroundColor White
Write-Host "  Swagger:   http://localhost:5000/swagger" -ForegroundColor White
Write-Host "  Health:    http://localhost:5000/health" -ForegroundColor White
Write-Host ""
Write-Host "Service Management:" -ForegroundColor Cyan
Write-Host "  Start:     sc start $ServiceName" -ForegroundColor Gray
Write-Host "  Stop:      sc stop $ServiceName" -ForegroundColor Gray
Write-Host "  Restart:   sc stop $ServiceName && sc start $ServiceName" -ForegroundColor Gray
Write-Host "  Status:    sc query $ServiceName" -ForegroundColor Gray
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Cyan
Write-Host "  File: $InstallPath\appsettings.json" -ForegroundColor White
Write-Host "  Auth: Username=bosi, Password=Th1s1sSp4rt4!" -ForegroundColor White
Write-Host ""
