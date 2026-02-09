<#
.SYNOPSIS
    One-shot deployment for Hazina Orchestration on DESKTOP-ECBAUNU (Martien's PC)

.DESCRIPTION
    Complete deployment in one command:
    1. Builds MSI from source
    2. Stops existing service (if present) and kills running process
    3. Removes old Windows Service (migration from v1)
    4. Installs MSI silently
    5. Configures HTTPS with Tailscale certificates
    6. Writes machine-specific appsettings.json
    7. Strips appsettings.Production.json (prevents config conflicts)
    8. Launches the tray application
    9. Verifies everything works

    MUST RUN AS ADMINISTRATOR (for MSI install + old service removal).

.PARAMETER SkipBuild
    Skip MSI build (use existing MSI)

.PARAMETER Force
    Skip confirmation prompt

.EXAMPLE
    .\Deploy-ThisPC.ps1
    .\Deploy-ThisPC.ps1 -SkipBuild   # Use existing MSI
#>

param(
    [switch]$SkipBuild,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# =====================================================
# MACHINE-SPECIFIC CONFIGURATION (DESKTOP-ECBAUNU)
# =====================================================
$Config = @{
    # Service
    ServiceName     = "HazinaOrchestration"
    InstallDir      = "C:\Program Files (x86)\Hazina Orchestration"

    # Network
    Port            = 5123
    Protocol        = "https"

    # Certificates (Tailscale)
    CertSource      = "C:\stores\orchestration\tailscale.crt"
    KeySource       = "C:\stores\orchestration\tailscale.key"

    # Authentication
    AuthUsername    = "bosi"
    AuthPassword    = "Th1s1sSp4rt4!"

    # Terminal / Claude Agent
    TerminalCommand = "C:\\scripts\\claude_agent.bat"
    TerminalWorkDir = "C:\\scripts"

    # Paths
    DatabasePath    = "C:\\scripts\\_machine\\agent-activity.db"
    LogsPath        = "C:\\scripts\\logs"
    SessionLogsPath = "C:\\scripts\\logs\\agent-sessions"
}

# =====================================================
# PRE-FLIGHT CHECKS
# =====================================================

Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Hazina Orchestration - Deploy to DESKTOP-ECBAUNU" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

# Check admin
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[FATAL] Must run as Administrator!" -ForegroundColor Red
    Write-Host "  Right-click PowerShell -> Run as Administrator" -ForegroundColor Yellow
    exit 1
}
Write-Host "[OK] Running as Administrator" -ForegroundColor Green

# Check certificates exist
if (-not (Test-Path $Config.CertSource)) {
    Write-Host "[FATAL] Tailscale certificate not found: $($Config.CertSource)" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $Config.KeySource)) {
    Write-Host "[FATAL] Tailscale key not found: $($Config.KeySource)" -ForegroundColor Red
    exit 1
}
Write-Host "[OK] Tailscale certificates found" -ForegroundColor Green

# Confirm
if (-not $Force) {
    Write-Host ""
    Write-Host "This will:" -ForegroundColor Yellow
    Write-Host "  - Stop and REMOVE old Windows Service (if exists)" -ForegroundColor Gray
    Write-Host "  - Kill any running HazinaOrchestration.exe" -ForegroundColor Gray
    Write-Host "  - Install/upgrade MSI to $($Config.InstallDir)" -ForegroundColor Gray
    Write-Host "  - Configure HTTPS on port $($Config.Port) with Tailscale certs" -ForegroundColor Gray
    Write-Host "  - Set auth: $($Config.AuthUsername) / ****" -ForegroundColor Gray
    Write-Host "  - Launch as desktop tray app (no longer a service)" -ForegroundColor Gray
    Write-Host ""
    $confirm = Read-Host "Continue? (Y/N)"
    if ($confirm -ne "Y" -and $confirm -ne "y") {
        Write-Host "Cancelled." -ForegroundColor Yellow
        exit 0
    }
}

Write-Host ""

# =====================================================
# STEP 1: Build MSI
# =====================================================
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$msiPath = Join-Path $scriptDir "bin\Release\HazinaOrchestrationSetup.msi"

if ($SkipBuild -and (Test-Path $msiPath)) {
    Write-Host "STEP 1: Skipping build (using existing MSI)" -ForegroundColor Yellow
} else {
    Write-Host "STEP 1: Building MSI..." -ForegroundColor Cyan
    $buildScript = Join-Path $scriptDir "Build-MSI-Complete.ps1"
    & $buildScript
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[FATAL] MSI build failed" -ForegroundColor Red
        exit 1
    }
}

if (-not (Test-Path $msiPath)) {
    Write-Host "[FATAL] MSI not found: $msiPath" -ForegroundColor Red
    exit 1
}
$msiSize = [math]::Round((Get-Item $msiPath).Length / 1MB, 2)
Write-Host "[OK] MSI ready: $msiSize MB" -ForegroundColor Green
Write-Host ""

# =====================================================
# STEP 2: Stop and remove old Windows Service + kill process
# =====================================================
Write-Host "STEP 2: Removing old Windows Service (if exists)..." -ForegroundColor Cyan

$svc = Get-Service $Config.ServiceName -ErrorAction SilentlyContinue
if ($svc) {
    if ($svc.Status -eq "Running") {
        Stop-Service $Config.ServiceName -Force
        Start-Sleep -Seconds 3
        Write-Host "[OK] Service stopped" -ForegroundColor Green
    }
    # Remove the service entirely - we're migrating to tray app
    sc.exe delete $Config.ServiceName 2>&1 | Out-Null
    Write-Host "[OK] Old Windows Service removed (migrating to tray app)" -ForegroundColor Green
} else {
    Write-Host "[OK] No old Windows Service found" -ForegroundColor Green
}

# Kill any stray processes
Get-Process HazinaOrchestration -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Write-Host ""

# =====================================================
# STEP 3: Install MSI
# =====================================================
Write-Host "STEP 3: Installing MSI..." -ForegroundColor Cyan

$msiLog = Join-Path $env:TEMP "hazina-install.log"
$proc = Start-Process msiexec -ArgumentList "/i", "`"$msiPath`"", "/qn", "/l*v", "`"$msiLog`"" -Wait -PassThru
if ($proc.ExitCode -ne 0) {
    Write-Host "[FAILED] MSI install failed (exit code: $($proc.ExitCode))" -ForegroundColor Red
    Write-Host "  Log: $msiLog" -ForegroundColor Yellow
    exit 1
}
Write-Host "[OK] MSI installed" -ForegroundColor Green
Write-Host ""

# =====================================================
# STEP 4: Copy Tailscale certificates
# =====================================================
Write-Host "STEP 4: Copying Tailscale certificates..." -ForegroundColor Cyan

Copy-Item $Config.CertSource -Destination (Join-Path $Config.InstallDir "tailscale.crt") -Force
Copy-Item $Config.KeySource -Destination (Join-Path $Config.InstallDir "tailscale.key") -Force
Write-Host "[OK] Certificates copied" -ForegroundColor Green
Write-Host ""

# =====================================================
# STEP 5: Write machine-specific appsettings.json
# =====================================================
Write-Host "STEP 5: Writing machine-specific appsettings.json..." -ForegroundColor Cyan

$appSettings = @{
    Logging = @{
        LogLevel = @{
            Default = "Information"
            "Microsoft.AspNetCore" = "Warning"
        }
    }
    AllowedHosts = "*"
    Kestrel = @{
        Endpoints = @{
            Https = @{
                Url = "https://*:$($Config.Port)"
                Certificate = @{
                    Path = "tailscale.crt"
                    KeyPath = "tailscale.key"
                }
            }
        }
    }
    Authentication = @{
        Enabled = $true
        Username = $Config.AuthUsername
        Password = $Config.AuthPassword
        Realm = "Hazina Agentic Orchestration"
    }
    AgenticOrchestration = @{
        DatabasePath = $Config.DatabasePath
        LogsPath = $Config.LogsPath
        EntitiesYamlPath = "entities.yaml"
        SignalR = @{
            Enabled = $true
            HubPath = "/hubs/agentic"
        }
        Polling = @{
            InstanceHeartbeatTimeoutSeconds = 60
            InteractionExpiryMinutes = 60
        }
        Features = @{
            EnableTaskQueue = $true
            EnableOutputStreaming = $true
            EnableRealtimeNotifications = $true
        }
        Terminal = @{
            DefaultCommand = $Config.TerminalCommand
            DefaultWorkingDirectory = $Config.TerminalWorkDir
            DefaultArguments = @()
            DefaultColumns = 120
            DefaultRows = 30
            MaxConcurrentSessions = 10
            SessionTimeoutMinutes = 60
        }
        SessionLogging = @{
            Enabled = $true
            BasePath = $Config.SessionLogsPath
        }
    }
    Swagger = @{
        Enabled = $true
        Title = "Hazina Agentic Orchestration API"
        Description = "Web API for managing Claude Code CLI instances via Hazina declarative framework"
        Version = "v1"
    }
    OpenAI = @{
        ApiKey = "YOUR_OPENAI_API_KEY_HERE"
        Model = "gpt-4o-mini"
        EmbeddingModel = "text-embedding-3-small"
        ImageModel = "dall-e-3"
        TtsModel = "gpt-4o-mini-tts"
    }
} | ConvertTo-Json -Depth 10

$appSettingsPath = Join-Path $Config.InstallDir "appsettings.json"
Set-Content -Path $appSettingsPath -Value $appSettings -Encoding UTF8
Write-Host "[OK] appsettings.json written (HTTPS, auth, full paths)" -ForegroundColor Green

# =====================================================
# STEP 6: Strip appsettings.Production.json
# =====================================================
Write-Host "STEP 6: Writing stripped appsettings.Production.json..." -ForegroundColor Cyan

# Production config must NOT override Kestrel, Auth, or Terminal settings
# Only safe overrides (logging, swagger, openai)
$productionSettings = @{
    Logging = @{
        LogLevel = @{
            Default = "Information"
            "Microsoft.AspNetCore" = "Warning"
        }
        EventLog = @{
            LogLevel = @{
                Default = "Information"
            }
        }
    }
    AllowedHosts = "*"
    Swagger = @{
        Enabled = $true
        Title = "Hazina Agentic Orchestration API"
        Description = "Web API for managing Claude Code CLI instances via Hazina declarative framework"
        Version = "v1"
    }
    OpenAI = @{
        ApiKey = ""
        Model = "gpt-4o-mini"
        EmbeddingModel = "text-embedding-3-small"
        ImageModel = "dall-e-3"
        TtsModel = "gpt-4o-mini-tts"
    }
} | ConvertTo-Json -Depth 10

$productionPath = Join-Path $Config.InstallDir "appsettings.Production.json"
Set-Content -Path $productionPath -Value $productionSettings -Encoding UTF8
Write-Host "[OK] appsettings.Production.json stripped (no Kestrel/Auth/Terminal overrides)" -ForegroundColor Green
Write-Host ""

# =====================================================
# STEP 7: (Skipped - no longer needed)
# Previously configured git safe.directory for SYSTEM user,
# but tray app runs as current user so this is not required.
# =====================================================
Write-Host "STEP 7: Skipped (git safe.directory not needed - tray app runs as user)" -ForegroundColor DarkGray
Write-Host ""

# =====================================================
# STEP 8: Launch tray application
# =====================================================
Write-Host "STEP 8: Launching tray application..." -ForegroundColor Cyan

$exePath = Join-Path $Config.InstallDir "HazinaOrchestration.exe"
if (Test-Path $exePath) {
    Start-Process $exePath -WorkingDirectory $Config.InstallDir
    Start-Sleep -Seconds 5
    $proc = Get-Process HazinaOrchestration -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Host "[OK] Tray application running (PID: $($proc.Id))" -ForegroundColor Green
    } else {
        Write-Host "[WARN] Process not detected - check system tray" -ForegroundColor Yellow
    }
} else {
    Write-Host "[WARN] Executable not found: $exePath" -ForegroundColor Yellow
}
Write-Host ""

# =====================================================
# STEP 9: Verify deployment
# =====================================================
Write-Host "STEP 9: Verifying deployment..." -ForegroundColor Cyan

# Health check
try {
    # Use -SkipCertificateCheck for self-signed Tailscale certs
    $healthUrl = "https://localhost:$($Config.Port)/health"
    $auth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("$($Config.AuthUsername):$($Config.AuthPassword)"))
    $headers = @{ Authorization = "Basic $auth" }

    $response = Invoke-WebRequest -Uri $healthUrl -Headers $headers -SkipCertificateCheck -TimeoutSec 10 -UseBasicParsing
    Write-Host "[OK] Health endpoint: $($response.StatusCode)" -ForegroundColor Green
} catch {
    Write-Host "[WARN] Health check failed: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "  Service may still be starting up..." -ForegroundColor Yellow
}

# Swagger check
try {
    $swaggerUrl = "https://localhost:$($Config.Port)/swagger/index.html"
    $response = Invoke-WebRequest -Uri $swaggerUrl -Headers $headers -SkipCertificateCheck -TimeoutSec 10 -UseBasicParsing
    Write-Host "[OK] Swagger UI accessible" -ForegroundColor Green
} catch {
    Write-Host "[WARN] Swagger check failed" -ForegroundColor Yellow
}

# Terminal session test
try {
    $sessionUrl = "https://localhost:$($Config.Port)/api/terminal/sessions"
    $body = '{"name":"deploy-test"}'
    $response = Invoke-RestMethod -Uri $sessionUrl -Method POST -Headers $headers -Body $body -ContentType "application/json" -SkipCertificateCheck -TimeoutSec 15

    if ($response.isRunning) {
        Write-Host "[OK] Terminal sessions working (test session created)" -ForegroundColor Green
        # Clean up test session
        Start-Sleep -Seconds 2
        try {
            Invoke-RestMethod -Uri "$sessionUrl/$($response.sessionId)" -Method DELETE -Headers $headers -SkipCertificateCheck -TimeoutSec 5 | Out-Null
        } catch {}
    }
} catch {
    Write-Host "[WARN] Terminal test failed: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "  The app may still be starting up - give it a few more seconds" -ForegroundColor Yellow
}

Write-Host ""

# =====================================================
# DONE
# =====================================================
Write-Host "========================================================" -ForegroundColor Green
Write-Host "  DEPLOYMENT COMPLETE - Desktop Tray App v2" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Mode:     Desktop Tray App (system tray icon)" -ForegroundColor White
Write-Host "  Local:    https://localhost:$($Config.Port)" -ForegroundColor White
Write-Host "  Remote:   https://desktop-ecbaunu.tailca9ff1.ts.net:$($Config.Port)" -ForegroundColor White
Write-Host "  Swagger:  https://localhost:$($Config.Port)/swagger" -ForegroundColor White
Write-Host "  Auth:     $($Config.AuthUsername) / ****" -ForegroundColor White
Write-Host ""
Write-Host "  Manage:" -ForegroundColor Cyan
Write-Host "    Right-click tray icon -> Open Dashboard" -ForegroundColor Gray
Write-Host "    Right-click tray icon -> Start with Windows (toggle)" -ForegroundColor Gray
Write-Host "    Right-click tray icon -> Exit" -ForegroundColor Gray
Write-Host "    Start Menu -> Hazina Orchestration" -ForegroundColor Gray
Write-Host ""
