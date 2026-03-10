<#
.SYNOPSIS
    Hazina Orchestration - Universal Setup Script

.DESCRIPTION
    Primary installation entry point for Hazina Orchestration.
    Handles Tailscale detection, certificate generation, MSI installation,
    and HTTPS configuration across 8 phases:

    Phase 1: Pre-flight checks (admin, existing install)
    Phase 2: Tailscale detection & setup
    Phase 3: Tailscale connection verification
    Phase 4: Certificate generation
    Phase 5: MSI build & install
    Phase 6: Configuration (appsettings.json + Production.json)
    Phase 7: Funnel (optional, Tailscale only)
    Phase 8: Launch & verify

.PARAMETER InstallDir
    Override install path (default: auto-detect from registry or Program Files)

.PARAMETER IncludeBuild
    Build MSI from source before installing

.PARAMETER SkipMsiInstall
    Skip MSI installation (already installed)

.PARAMETER Mode
    Network mode: interactive (ask user), http (no TLS), tailscale (auto-cert), custom-cert (user-provided)

.PARAMETER Port
    HTTPS/HTTP port (default: 5123)

.PARAMETER EnableFunnel
    Enable Tailscale Funnel for internet access

.PARAMETER AuthUsername
    Basic auth username (default: admin)

.PARAMETER AuthPassword
    Basic auth password (default: changeme)

.PARAMETER TerminalCommand
    Command to launch in terminal sessions (default: claude)

.PARAMETER TerminalWorkDir
    Working directory for terminal sessions

.PARAMETER DatabasePath
    Path to SQLite database (default: data\agent-activity.db relative to install dir)

.PARAMETER LogsPath
    Path to logs directory (default: logs relative to install dir)

.PARAMETER CertPath
    Path to TLS certificate (custom-cert mode only)

.PARAMETER KeyPath
    Path to TLS private key (custom-cert mode only)

.PARAMETER ConfigFile
    JSON config file for unattended setup (overrides all other params)

.PARAMETER Force
    Skip confirmation prompts

.EXAMPLE
    .\Setup.ps1
    # Interactive mode - asks about Tailscale, configures automatically

.EXAMPLE
    .\Setup.ps1 -Mode tailscale -IncludeBuild
    # Build from source, auto-configure with Tailscale

.EXAMPLE
    .\Setup.ps1 -Mode http -Port 8080
    # HTTP-only on port 8080

.EXAMPLE
    .\Setup.ps1 -ConfigFile .\my-config.json
    # Unattended setup from config file
#>

param(
    [string]$InstallDir,
    [switch]$IncludeBuild,
    [switch]$SkipMsiInstall,
    [ValidateSet("interactive", "http", "tailscale", "custom-cert")]
    [string]$Mode = "interactive",
    [int]$Port = 5123,
    [switch]$EnableFunnel,
    [string]$AuthUsername,
    [string]$AuthPassword,
    [string]$TerminalCommand,
    [string]$TerminalWorkDir,
    [string]$DatabasePath,
    [string]$LogsPath,
    [string]$CertPath,
    [string]$KeyPath,
    [string]$ConfigFile,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# =====================================================
# HELPER FUNCTIONS
# =====================================================

function Test-Administrator {
    return ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator
    )
}

function Find-TailscaleExe {
    <#
    .SYNOPSIS
        Locate tailscale.exe by checking PATH, common install dirs, and registry.
    #>

    # 1. Check PATH
    $inPath = Get-Command tailscale.exe -ErrorAction SilentlyContinue
    if ($inPath) { return $inPath.Source }

    # 2. Check common install locations
    $commonPaths = @(
        "$env:ProgramFiles\Tailscale\tailscale.exe",
        "${env:ProgramFiles(x86)}\Tailscale\tailscale.exe",
        "$env:LOCALAPPDATA\Tailscale\tailscale.exe"
    )
    foreach ($p in $commonPaths) {
        if (Test-Path $p) { return $p }
    }

    # 3. Check registry
    $regPaths = @(
        "HKLM:\SOFTWARE\Tailscale IPN",
        "HKLM:\SOFTWARE\WOW6432Node\Tailscale IPN"
    )
    foreach ($reg in $regPaths) {
        try {
            $installDir = (Get-ItemProperty -Path $reg -ErrorAction SilentlyContinue).InstallDir
            if ($installDir) {
                $exe = Join-Path $installDir "tailscale.exe"
                if (Test-Path $exe) { return $exe }
            }
        } catch {}
    }

    return $null
}

function Get-TailscaleStatus {
    <#
    .SYNOPSIS
        Parse tailscale status --json and return hostname, FQDN, connected state.
    #>
    param([string]$TailscaleExe)

    try {
        $json = & $TailscaleExe status --json 2>&1
        $status = $json | ConvertFrom-Json

        $self = $status.Self
        return @{
            Connected = ($status.BackendState -eq "Running")
            Hostname  = $self.HostName
            DNSName   = $self.DNSName.TrimEnd('.')
            TailnetName = $status.CurrentTailnet.Name
            Online    = $self.Online
            OS        = $self.OS
        }
    } catch {
        return @{
            Connected = $false
            Hostname  = $null
            DNSName   = $null
            Error     = $_.Exception.Message
        }
    }
}

function Install-Tailscale {
    <#
    .SYNOPSIS
        Install Tailscale via winget or direct MSI download.
    #>

    # Try winget first
    $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
    if ($winget) {
        Write-Host "  Installing Tailscale via winget..." -ForegroundColor Gray
        $result = Start-Process winget -ArgumentList "install", "--id", "Tailscale.Tailscale", "--silent", "--accept-package-agreements", "--accept-source-agreements" -Wait -PassThru
        if ($result.ExitCode -eq 0) {
            Write-Host "  [OK] Tailscale installed via winget" -ForegroundColor Green
            # Refresh PATH
            $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path", "User")
            return $true
        }
        Write-Host "  [WARN] winget install returned exit code $($result.ExitCode)" -ForegroundColor Yellow
    }

    # Fallback: direct MSI download
    Write-Host "  Downloading Tailscale installer..." -ForegroundColor Gray
    $msiUrl = "https://pkgs.tailscale.com/stable/tailscale-setup-latest-amd64.msi"
    $msiPath = Join-Path $env:TEMP "tailscale-setup.msi"

    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $msiUrl -OutFile $msiPath -UseBasicParsing
    } catch {
        Write-Host "  [FAILED] Download failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }

    Write-Host "  Installing Tailscale MSI..." -ForegroundColor Gray
    $result = Start-Process msiexec -ArgumentList "/i", "`"$msiPath`"", "/qn", "TS_UNATTENDEDMODE=always" -Wait -PassThru
    if ($result.ExitCode -ne 0) {
        Write-Host "  [FAILED] MSI install failed (exit code: $($result.ExitCode))" -ForegroundColor Red
        return $false
    }

    # Refresh PATH
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path", "User")

    # Clean up
    Remove-Item $msiPath -ErrorAction SilentlyContinue

    Write-Host "  [OK] Tailscale installed via MSI" -ForegroundColor Green
    return $true
}

function New-TailscaleCertificate {
    <#
    .SYNOPSIS
        Generate TLS certificate using tailscale cert command.
    #>
    param(
        [string]$TailscaleExe,
        [string]$FQDN,
        [string]$CertFile,
        [string]$KeyFile
    )

    Write-Host "  Generating certificate for $FQDN..." -ForegroundColor Gray

    $certDir = Split-Path -Parent $CertFile
    if (-not (Test-Path $certDir)) {
        New-Item -ItemType Directory -Path $certDir -Force | Out-Null
    }

    try {
        $output = & $TailscaleExe cert --cert-file $CertFile --key-file $KeyFile $FQDN 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "  [FAILED] tailscale cert failed: $output" -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "  [FAILED] tailscale cert error: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }

    # Verify files exist and are non-empty
    if (-not (Test-Path $CertFile) -or (Get-Item $CertFile).Length -eq 0) {
        Write-Host "  [FAILED] Certificate file missing or empty: $CertFile" -ForegroundColor Red
        return $false
    }
    if (-not (Test-Path $KeyFile) -or (Get-Item $KeyFile).Length -eq 0) {
        Write-Host "  [FAILED] Key file missing or empty: $KeyFile" -ForegroundColor Red
        return $false
    }

    Write-Host "  [OK] Certificate generated" -ForegroundColor Green
    return $true
}

function Find-HazinaInstallDir {
    <#
    .SYNOPSIS
        Detect Hazina Orchestration install dir from registry or defaults.
    #>

    # Check registry (MSI install location)
    $regPaths = @(
        "HKLM:\SOFTWARE\Hazina\Orchestration",
        "HKLM:\SOFTWARE\WOW6432Node\Hazina\Orchestration"
    )
    foreach ($reg in $regPaths) {
        try {
            $dir = (Get-ItemProperty -Path $reg -ErrorAction SilentlyContinue).InstallDir
            if ($dir -and (Test-Path $dir)) { return $dir }
        } catch {}
    }

    # Check common MSI install paths via uninstall registry
    $uninstallPaths = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )
    foreach ($path in $uninstallPaths) {
        $entries = Get-ItemProperty -Path $path -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -like "*Hazina*Orchestration*" }
        if ($entries) {
            $loc = $entries | Select-Object -First 1 -ExpandProperty InstallLocation -ErrorAction SilentlyContinue
            if ($loc -and (Test-Path $loc)) { return $loc }
        }
    }

    # Default paths
    $defaults = @(
        "${env:ProgramFiles(x86)}\Hazina Orchestration",
        "$env:ProgramFiles\Hazina Orchestration"
    )
    foreach ($d in $defaults) {
        if (Test-Path $d) { return $d }
    }

    # Fallback
    return "${env:ProgramFiles(x86)}\Hazina Orchestration"
}

function Write-AppSettings {
    <#
    .SYNOPSIS
        Configure appsettings.json using HazinaConfigTool (non-destructive).
        Falls back to direct JSON write if ConfigTool not available.
    #>
    param(
        [string]$TargetDir,
        [string]$Protocol,
        [int]$Port,
        [string]$CertFile,
        [string]$KeyFile,
        [string]$AuthUser,
        [string]$AuthPass,
        [string]$TermCmd,
        [string]$TermWorkDir,
        [string]$DbPath,
        [string]$LogPath
    )

    # Resolve defaults
    if (-not $DbPath)    { $DbPath = "data\agent-activity.db" }
    if (-not $LogPath)    { $LogPath = "logs" }
    if (-not $TermCmd)    { $TermCmd = "claude" }
    if (-not $AuthUser)   { $AuthUser = "admin" }
    if (-not $AuthPass)   { $AuthPass = "changeme" }

    $configFile = Join-Path $TargetDir "appsettings.json"
    $configTool = Join-Path $TargetDir "HazinaConfigTool.exe"

    if (Test-Path $configTool) {
        Write-Host "  Using HazinaConfigTool for non-destructive configuration..." -ForegroundColor Gray

        # Configure authentication
        & $configTool set-auth --username $AuthUser --password $AuthPass --config $configFile --silent --no-backup
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  [OK] Authentication configured" -ForegroundColor Green
        } else {
            Write-Host "  [WARN] Authentication configuration failed (exit: $LASTEXITCODE)" -ForegroundColor Yellow
        }

        # Configure Kestrel
        $kestrelArgs = @("set-kestrel", "--protocol", $Protocol, "--port", $Port, "--config", $configFile, "--silent", "--no-backup")
        if ($Protocol -eq "https" -and $CertFile) {
            $kestrelArgs += @("--cert", $CertFile, "--key", $KeyFile)
        }
        & $configTool @kestrelArgs
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  [OK] Kestrel configured (${Protocol}://*:${Port})" -ForegroundColor Green
        } else {
            Write-Host "  [WARN] Kestrel configuration failed (exit: $LASTEXITCODE)" -ForegroundColor Yellow
        }

        # Configure paths
        & $configTool set-paths --database $DbPath --logs $LogPath --config $configFile --silent --no-backup
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  [OK] Paths configured" -ForegroundColor Green
        } else {
            Write-Host "  [WARN] Paths configuration failed (exit: $LASTEXITCODE)" -ForegroundColor Yellow
        }

        # Configure terminal
        $termArgs = @("set-terminal", "--command", $TermCmd, "--config", $configFile, "--silent", "--no-backup")
        if ($TermWorkDir) {
            $termArgs += @("--workdir", $TermWorkDir)
        }
        & $configTool @termArgs
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  [OK] Terminal configured" -ForegroundColor Green
        } else {
            Write-Host "  [WARN] Terminal configuration failed (exit: $LASTEXITCODE)" -ForegroundColor Yellow
        }

        # Validate final configuration
        & $configTool validate --config $configFile --silent
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  [OK] Configuration validated" -ForegroundColor Green
        } else {
            Write-Host "  [WARN] Configuration has validation warnings (exit: $LASTEXITCODE)" -ForegroundColor Yellow
        }

        Write-Host "  [OK] appsettings.json updated (all existing settings preserved)" -ForegroundColor Green
    } else {
        Write-Host "  [WARN] HazinaConfigTool.exe not found, using legacy JSON generation" -ForegroundColor Yellow

        # Legacy fallback: generate appsettings.json from scratch
        $endpointKey = if ($Protocol -eq "https") { "Https" } else { "Http" }
        $endpoint = @{ Url = "${Protocol}://*:${Port}" }
        if ($Protocol -eq "https" -and $CertFile) {
            $endpoint.Certificate = @{ Path = $CertFile; KeyPath = $KeyFile }
        }

        $appSettings = @{
            Logging = @{ LogLevel = @{ Default = "Information"; "Microsoft.AspNetCore" = "Warning" } }
            AllowedHosts = "*"
            Kestrel = @{ Endpoints = @{ $endpointKey = $endpoint } }
            Authentication = @{ Enabled = $true; Username = $AuthUser; Password = $AuthPass; Realm = "Hazina Agentic Orchestration" }
            AgenticOrchestration = @{
                DatabasePath = $DbPath; LogsPath = $LogPath; EntitiesYamlPath = "entities.yaml"
                SignalR = @{ Enabled = $true; HubPath = "/hubs/agentic" }
                Polling = @{ InstanceHeartbeatTimeoutSeconds = 60; InteractionExpiryMinutes = 60 }
                Features = @{ EnableTaskQueue = $true; EnableOutputStreaming = $true; EnableRealtimeNotifications = $true }
                Terminal = @{ DefaultCommand = $TermCmd; DefaultWorkingDirectory = $TermWorkDir; DefaultArguments = @(); DefaultColumns = 120; DefaultRows = 30; MaxConcurrentSessions = 10; SessionTimeoutMinutes = 60 }
                SessionLogging = @{ Enabled = $true; BasePath = if ($LogPath) { "$LogPath\agent-sessions" } else { "logs\agent-sessions" } }
            }
            Swagger = @{ Enabled = $true; Title = "Hazina Agentic Orchestration API"; Description = "Web API for managing Claude Code CLI instances via Hazina declarative framework"; Version = "v1" }
            OpenAI = @{ ApiKey = "YOUR_OPENAI_API_KEY_HERE"; Model = "gpt-4o-mini"; EmbeddingModel = "text-embedding-3-small"; ImageModel = "dall-e-3"; TtsModel = "gpt-4o-mini-tts" }
        } | ConvertTo-Json -Depth 10

        Set-Content -Path $configFile -Value $appSettings -Encoding UTF8
        Write-Host "  [OK] appsettings.json written (legacy mode)" -ForegroundColor Green
    }

    # Production config (baseline, rarely changes - always recreated)
    $productionSettings = @{
        Logging = @{ LogLevel = @{ Default = "Information"; "Microsoft.AspNetCore" = "Warning" }; EventLog = @{ LogLevel = @{ Default = "Information" } } }
        AllowedHosts = "*"
        Swagger = @{ Enabled = $true; Title = "Hazina Agentic Orchestration API"; Description = "Web API for managing Claude Code CLI instances via Hazina declarative framework"; Version = "v1" }
        OpenAI = @{ ApiKey = ""; Model = "gpt-4o-mini"; EmbeddingModel = "text-embedding-3-small"; ImageModel = "dall-e-3"; TtsModel = "gpt-4o-mini-tts" }
    } | ConvertTo-Json -Depth 10

    $productionPath = Join-Path $TargetDir "appsettings.Production.json"
    Set-Content -Path $productionPath -Value $productionSettings -Encoding UTF8
    Write-Host "  [OK] appsettings.Production.json written" -ForegroundColor Green
}

function Test-TailscaleFunnel {
    <#
    .SYNOPSIS
        Check if Tailscale Funnel is available on this tailnet.
    #>
    param([string]$TailscaleExe)

    try {
        $output = & $TailscaleExe funnel status 2>&1
        if ($LASTEXITCODE -eq 0) { return $true }
        # Funnel not enabled on tailnet
        return $false
    } catch {
        return $false
    }
}

function Enable-TailscaleFunnel {
    <#
    .SYNOPSIS
        Enable Tailscale Funnel on specified port.
    #>
    param(
        [string]$TailscaleExe,
        [int]$Port
    )

    Write-Host "  Enabling Tailscale Funnel on port $Port..." -ForegroundColor Gray
    try {
        & $TailscaleExe funnel $Port 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  [OK] Funnel enabled" -ForegroundColor Green
            return $true
        }
        Write-Host "  [FAILED] Funnel command returned exit code $LASTEXITCODE" -ForegroundColor Yellow
        return $false
    } catch {
        Write-Host "  [FAILED] $($_.Exception.Message)" -ForegroundColor Yellow
        return $false
    }
}

function Test-HealthEndpoint {
    <#
    .SYNOPSIS
        Health check with retry and exponential backoff.
    #>
    param(
        [string]$BaseUrl,
        [string]$Username,
        [string]$Password,
        [int]$MaxRetries = 3
    )

    $auth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${Username}:${Password}"))
    $headers = @{ Authorization = "Basic $auth" }
    $healthUrl = "$BaseUrl/health"

    for ($i = 1; $i -le $MaxRetries; $i++) {
        try {
            $response = Invoke-WebRequest -Uri $healthUrl -Headers $headers -SkipCertificateCheck -TimeoutSec 10 -UseBasicParsing
            if ($response.StatusCode -eq 200) {
                return $true
            }
        } catch {
            if ($i -lt $MaxRetries) {
                $wait = [math]::Pow(2, $i)
                Write-Host "  Retry $i/$MaxRetries in ${wait}s..." -ForegroundColor Gray
                Start-Sleep -Seconds $wait
            }
        }
    }
    return $false
}


# =====================================================
# PHASE 0: LOAD CONFIG FILE (if specified)
# =====================================================

if ($ConfigFile) {
    if (-not (Test-Path $ConfigFile)) {
        Write-Host "[FATAL] Config file not found: $ConfigFile" -ForegroundColor Red
        exit 1
    }
    Write-Host "Loading config from $ConfigFile..." -ForegroundColor Gray
    $cfg = Get-Content $ConfigFile -Raw | ConvertFrom-Json

    # Map config values to params (config values override defaults, explicit params override config)
    if ($cfg.mode -and -not $PSBoundParameters.ContainsKey('Mode'))            { $Mode = $cfg.mode }
    if ($cfg.port -and -not $PSBoundParameters.ContainsKey('Port'))            { $Port = $cfg.port }
    if ($cfg.enableFunnel -and -not $PSBoundParameters.ContainsKey('EnableFunnel')) { $EnableFunnel = [switch]$cfg.enableFunnel }
    if ($cfg.installDir -and -not $PSBoundParameters.ContainsKey('InstallDir'))    { $InstallDir = $cfg.installDir }
    if ($cfg.auth) {
        if ($cfg.auth.username -and -not $PSBoundParameters.ContainsKey('AuthUsername')) { $AuthUsername = $cfg.auth.username }
        if ($cfg.auth.password -and -not $PSBoundParameters.ContainsKey('AuthPassword')) { $AuthPassword = $cfg.auth.password }
    }
    if ($cfg.terminal) {
        if ($cfg.terminal.command -and -not $PSBoundParameters.ContainsKey('TerminalCommand'))          { $TerminalCommand = $cfg.terminal.command }
        if ($cfg.terminal.workingDirectory -and -not $PSBoundParameters.ContainsKey('TerminalWorkDir'))  { $TerminalWorkDir = $cfg.terminal.workingDirectory }
    }
    if ($cfg.paths) {
        if ($cfg.paths.database -and -not $PSBoundParameters.ContainsKey('DatabasePath')) { $DatabasePath = $cfg.paths.database }
        if ($cfg.paths.logs -and -not $PSBoundParameters.ContainsKey('LogsPath'))         { $LogsPath = $cfg.paths.logs }
    }
    if ($cfg.certificate) {
        if ($cfg.certificate.certPath -and -not $PSBoundParameters.ContainsKey('CertPath')) { $CertPath = $cfg.certificate.certPath }
        if ($cfg.certificate.keyPath -and -not $PSBoundParameters.ContainsKey('KeyPath'))   { $KeyPath = $cfg.certificate.keyPath }
    }
}


# =====================================================
# BANNER
# =====================================================
Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Hazina Orchestration - Setup" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""


# =====================================================
# PHASE 1: PRE-FLIGHT
# =====================================================
Write-Host "PHASE 1: Pre-flight checks" -ForegroundColor Cyan

if (-not (Test-Administrator)) {
    Write-Host "  [FATAL] Must run as Administrator!" -ForegroundColor Red
    Write-Host "  Right-click PowerShell -> Run as Administrator" -ForegroundColor Yellow
    exit 1
}
Write-Host "  [OK] Running as Administrator" -ForegroundColor Green

# Detect existing installation
$existingInstall = Find-HazinaInstallDir
if (-not $InstallDir) {
    $InstallDir = $existingInstall
}

$existingExe = Join-Path $InstallDir "HazinaOrchestration.exe"
if (Test-Path $existingExe) {
    Write-Host "  [OK] Existing installation found: $InstallDir" -ForegroundColor Green
} else {
    Write-Host "  [INFO] No existing installation at $InstallDir" -ForegroundColor Gray
}
Write-Host ""


# =====================================================
# PHASE 2: TAILSCALE DETECTION & SETUP
# =====================================================
Write-Host "PHASE 2: Tailscale detection" -ForegroundColor Cyan

$tailscaleExe = Find-TailscaleExe
$useTailscale = $false

if ($Mode -eq "http") {
    Write-Host "  [INFO] HTTP mode selected - skipping Tailscale" -ForegroundColor Gray
    $useTailscale = $false
}
elseif ($Mode -eq "custom-cert") {
    Write-Host "  [INFO] Custom certificate mode - skipping Tailscale" -ForegroundColor Gray
    if (-not $CertPath -or -not $KeyPath) {
        Write-Host "  [FATAL] -CertPath and -KeyPath required for custom-cert mode" -ForegroundColor Red
        exit 1
    }
    if (-not (Test-Path $CertPath)) {
        Write-Host "  [FATAL] Certificate not found: $CertPath" -ForegroundColor Red
        exit 1
    }
    if (-not (Test-Path $KeyPath)) {
        Write-Host "  [FATAL] Key not found: $KeyPath" -ForegroundColor Red
        exit 1
    }
    $useTailscale = $false
}
elseif ($tailscaleExe) {
    Write-Host "  [OK] Tailscale found: $tailscaleExe" -ForegroundColor Green
    $useTailscale = $true
}
elseif ($Mode -eq "tailscale") {
    Write-Host "  [INFO] Tailscale not found - installing..." -ForegroundColor Yellow
    $installed = Install-Tailscale
    if ($installed) {
        $tailscaleExe = Find-TailscaleExe
        if ($tailscaleExe) {
            $useTailscale = $true
        } else {
            Write-Host "  [FATAL] Tailscale installed but executable not found. Restart PowerShell and try again." -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "  [FATAL] Failed to install Tailscale" -ForegroundColor Red
        exit 1
    }
}
elseif ($Mode -eq "interactive") {
    Write-Host "  [INFO] Tailscale not found" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Tailscale provides automatic HTTPS certificates and secure remote access." -ForegroundColor White
    Write-Host "  Without it, the app will run on HTTP (local network only)." -ForegroundColor White
    Write-Host ""
    Write-Host "  Options:" -ForegroundColor White
    Write-Host "    [T] Install Tailscale and configure HTTPS (recommended)" -ForegroundColor White
    Write-Host "    [H] Continue with HTTP only (no TLS)" -ForegroundColor White
    Write-Host "    [C] Cancel setup" -ForegroundColor White
    Write-Host ""
    $choice = Read-Host "  Choose [T/H/C]"
    switch ($choice.ToUpper()) {
        "T" {
            Write-Host ""
            $installed = Install-Tailscale
            if ($installed) {
                $tailscaleExe = Find-TailscaleExe
                if ($tailscaleExe) {
                    $useTailscale = $true
                } else {
                    Write-Host "  [WARN] Tailscale installed but not in PATH. Restart PowerShell and re-run." -ForegroundColor Yellow
                    Write-Host "  Falling back to HTTP mode." -ForegroundColor Yellow
                    $useTailscale = $false
                }
            } else {
                Write-Host "  [WARN] Tailscale installation failed. Falling back to HTTP mode." -ForegroundColor Yellow
                $useTailscale = $false
            }
        }
        "H" {
            $useTailscale = $false
        }
        default {
            Write-Host "  Cancelled." -ForegroundColor Yellow
            exit 0
        }
    }
}

Write-Host ""


# =====================================================
# PHASE 3: TAILSCALE CONNECTION VERIFICATION
# =====================================================

$tailscaleFQDN = $null
$tailscaleHostname = $null

if ($useTailscale) {
    Write-Host "PHASE 3: Tailscale connection verification" -ForegroundColor Cyan

    $maxWaitMinutes = 5
    $checkInterval = 5
    $waited = 0

    while ($true) {
        $status = Get-TailscaleStatus -TailscaleExe $tailscaleExe

        if ($status.Connected -and $status.DNSName) {
            $tailscaleFQDN = $status.DNSName
            $tailscaleHostname = $status.Hostname
            Write-Host "  [OK] Connected to Tailscale" -ForegroundColor Green
            Write-Host "  Hostname: $tailscaleHostname" -ForegroundColor Gray
            Write-Host "  FQDN:     $tailscaleFQDN" -ForegroundColor Gray
            Write-Host "  Tailnet:  $($status.TailnetName)" -ForegroundColor Gray
            break
        }

        if ($waited -ge ($maxWaitMinutes * 60)) {
            Write-Host "  [WARN] Timed out waiting for Tailscale connection" -ForegroundColor Yellow
            Write-Host "  Falling back to HTTP mode" -ForegroundColor Yellow
            $useTailscale = $false
            break
        }

        if ($waited -eq 0) {
            Write-Host "  Tailscale is not connected. Please log in:" -ForegroundColor Yellow
            Write-Host ""
            Write-Host "    tailscale login" -ForegroundColor White
            Write-Host ""
            Write-Host "  Complete authentication in your browser, then this script will continue." -ForegroundColor Gray
            Write-Host "  Waiting up to $maxWaitMinutes minutes..." -ForegroundColor Gray
        }

        Start-Sleep -Seconds $checkInterval
        $waited += $checkInterval
        Write-Host "  Checking... ($([math]::Floor($waited/60))m $($waited%60)s)" -ForegroundColor Gray
    }

    Write-Host ""
} else {
    Write-Host "PHASE 3: Skipped (not using Tailscale)" -ForegroundColor DarkGray
    Write-Host ""
}


# =====================================================
# PHASE 4: CERTIFICATE GENERATION
# =====================================================

$protocol = "http"
$certDest = $null
$keyDest = $null

if ($useTailscale -and $tailscaleFQDN) {
    Write-Host "PHASE 4: Certificate generation" -ForegroundColor Cyan

    $certDest = Join-Path $InstallDir "tailscale.crt"
    $keyDest = Join-Path $InstallDir "tailscale.key"

    # Ensure install dir exists for cert generation
    if (-not (Test-Path $InstallDir)) {
        New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    }

    $certOk = New-TailscaleCertificate -TailscaleExe $tailscaleExe -FQDN $tailscaleFQDN -CertFile $certDest -KeyFile $keyDest

    if ($certOk) {
        $protocol = "https"
    } else {
        Write-Host "  [WARN] Certificate generation failed." -ForegroundColor Yellow
        Write-Host "  Make sure HTTPS certificates are enabled in your tailnet:" -ForegroundColor Yellow
        Write-Host "  https://login.tailscale.com/admin/dns" -ForegroundColor Cyan
        Write-Host "  Falling back to HTTP mode." -ForegroundColor Yellow
        $useTailscale = $false
        $certDest = $null
        $keyDest = $null
    }

    Write-Host ""
}
elseif ($Mode -eq "custom-cert") {
    Write-Host "PHASE 4: Custom certificate setup" -ForegroundColor Cyan

    # Copy certs to install dir
    if (-not (Test-Path $InstallDir)) {
        New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    }

    $certDest = Join-Path $InstallDir (Split-Path -Leaf $CertPath)
    $keyDest = Join-Path $InstallDir (Split-Path -Leaf $KeyPath)
    Copy-Item $CertPath -Destination $certDest -Force
    Copy-Item $KeyPath -Destination $keyDest -Force
    $protocol = "https"
    Write-Host "  [OK] Certificates copied to install dir" -ForegroundColor Green
    Write-Host ""
}
else {
    Write-Host "PHASE 4: Skipped (HTTP mode - no certificates needed)" -ForegroundColor DarkGray
    Write-Host ""
}


# =====================================================
# PHASE 5: MSI BUILD & INSTALL
# =====================================================
Write-Host "PHASE 5: MSI build & install" -ForegroundColor Cyan

$msiPath = Join-Path $scriptDir "bin\Release\HazinaOrchestrationSetup.msi"

if ($IncludeBuild) {
    Write-Host "  Building MSI from source..." -ForegroundColor Gray
    $buildScript = Join-Path $scriptDir "Build-MSI-Complete.ps1"
    if (-not (Test-Path $buildScript)) {
        Write-Host "  [FATAL] Build script not found: $buildScript" -ForegroundColor Red
        exit 1
    }
    & $buildScript
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  [FATAL] MSI build failed" -ForegroundColor Red
        exit 1
    }
}

if (-not $SkipMsiInstall) {
    if (-not (Test-Path $msiPath)) {
        Write-Host "  [FATAL] MSI not found: $msiPath" -ForegroundColor Red
        Write-Host "  Use -IncludeBuild to build from source, or place MSI at the expected path." -ForegroundColor Yellow
        exit 1
    }

    $msiSize = [math]::Round((Get-Item $msiPath).Length / 1MB, 2)
    Write-Host "  [OK] MSI ready: $msiSize MB" -ForegroundColor Green

    # Stop and remove old service + kill process
    $svc = Get-Service "HazinaOrchestration" -ErrorAction SilentlyContinue
    if ($svc) {
        if ($svc.Status -eq "Running") {
            Stop-Service "HazinaOrchestration" -Force
            Start-Sleep -Seconds 3
        }
        sc.exe delete "HazinaOrchestration" 2>&1 | Out-Null
        Write-Host "  [OK] Old Windows Service removed" -ForegroundColor Green
    }
    Get-Process HazinaOrchestration -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

    # Confirm before install
    if (-not $Force) {
        Write-Host ""
        Write-Host "  This will install/upgrade Hazina Orchestration to:" -ForegroundColor Yellow
        Write-Host "    $InstallDir" -ForegroundColor White
        Write-Host "    Mode: $( if ($useTailscale) { 'HTTPS (Tailscale)' } elseif ($Mode -eq 'custom-cert') { 'HTTPS (custom cert)' } else { 'HTTP' } )" -ForegroundColor White
        Write-Host "    Port: $Port" -ForegroundColor White
        Write-Host ""
        $confirm = Read-Host "  Continue? (Y/N)"
        if ($confirm -ne "Y" -and $confirm -ne "y") {
            Write-Host "  Cancelled." -ForegroundColor Yellow
            exit 0
        }
    }

    # Install MSI
    Write-Host "  Installing MSI..." -ForegroundColor Gray
    $msiLog = Join-Path $env:TEMP "hazina-install.log"
    $proc = Start-Process msiexec -ArgumentList "/i", "`"$msiPath`"", "/qn", "/l*v", "`"$msiLog`"" -Wait -PassThru
    if ($proc.ExitCode -ne 0) {
        Write-Host "  [FAILED] MSI install failed (exit code: $($proc.ExitCode))" -ForegroundColor Red
        Write-Host "  Log: $msiLog" -ForegroundColor Yellow
        exit 1
    }
    Write-Host "  [OK] MSI installed" -ForegroundColor Green

    # Re-detect install dir from registry
    $detectedDir = Find-HazinaInstallDir
    if ($detectedDir -and (Test-Path (Join-Path $detectedDir "HazinaOrchestration.exe"))) {
        $InstallDir = $detectedDir
    }
} else {
    Write-Host "  [INFO] Skipping MSI install (--SkipMsiInstall)" -ForegroundColor Gray
}

Write-Host ""


# =====================================================
# PHASE 6: CONFIGURATION
# =====================================================
Write-Host "PHASE 6: Configuration" -ForegroundColor Cyan

# For Tailscale certs generated in Phase 4, use relative paths in config
$configCertPath = $null
$configKeyPath = $null
if ($certDest) {
    $configCertPath = Split-Path -Leaf $certDest
    $configKeyPath = Split-Path -Leaf $keyDest
}

Write-AppSettings `
    -TargetDir $InstallDir `
    -Protocol $protocol `
    -Port $Port `
    -CertFile $configCertPath `
    -KeyFile $configKeyPath `
    -AuthUser $(if ($AuthUsername) { $AuthUsername } else { "admin" }) `
    -AuthPass $(if ($AuthPassword) { $AuthPassword } else { "changeme" }) `
    -TermCmd $(if ($TerminalCommand) { $TerminalCommand } else { "claude" }) `
    -TermWorkDir $(if ($TerminalWorkDir) { $TerminalWorkDir } else { $null }) `
    -DbPath $(if ($DatabasePath) { $DatabasePath } else { "data\agent-activity.db" }) `
    -LogPath $(if ($LogsPath) { $LogsPath } else { "logs" })

Write-Host ""


# =====================================================
# PHASE 7: FUNNEL (optional, Tailscale only)
# =====================================================

$funnelUrl = $null

if ($useTailscale -and $tailscaleFQDN) {
    if ($EnableFunnel) {
        Write-Host "PHASE 7: Tailscale Funnel" -ForegroundColor Cyan
        $funnelAvailable = Test-TailscaleFunnel -TailscaleExe $tailscaleExe
        if ($funnelAvailable) {
            $funnelOk = Enable-TailscaleFunnel -TailscaleExe $tailscaleExe -Port $Port
            if ($funnelOk) {
                $funnelUrl = "https://${tailscaleFQDN}:${Port}"
            }
        } else {
            Write-Host "  [WARN] Funnel not available on this tailnet" -ForegroundColor Yellow
            Write-Host "  Enable Funnel at: https://login.tailscale.com/admin/dns" -ForegroundColor Gray
        }
        Write-Host ""
    }
    elseif ($Mode -eq "interactive" -and -not $Force) {
        Write-Host "PHASE 7: Tailscale Funnel (optional)" -ForegroundColor Cyan
        Write-Host "  Funnel exposes this service to the public internet via Tailscale." -ForegroundColor Gray
        Write-Host "  This allows access from anywhere without VPN." -ForegroundColor Gray
        Write-Host ""
        $funnelChoice = Read-Host "  Enable Funnel? (Y/N)"
        if ($funnelChoice -eq "Y" -or $funnelChoice -eq "y") {
            $funnelAvailable = Test-TailscaleFunnel -TailscaleExe $tailscaleExe
            if ($funnelAvailable) {
                $funnelOk = Enable-TailscaleFunnel -TailscaleExe $tailscaleExe -Port $Port
                if ($funnelOk) {
                    $funnelUrl = "https://${tailscaleFQDN}:${Port}"
                }
            } else {
                Write-Host "  [WARN] Funnel not available on this tailnet" -ForegroundColor Yellow
            }
        } else {
            Write-Host "  [INFO] Funnel skipped" -ForegroundColor Gray
        }
        Write-Host ""
    }
    else {
        Write-Host "PHASE 7: Skipped (Funnel not requested)" -ForegroundColor DarkGray
        Write-Host ""
    }
} else {
    Write-Host "PHASE 7: Skipped (not using Tailscale)" -ForegroundColor DarkGray
    Write-Host ""
}


# =====================================================
# PHASE 8: LAUNCH & VERIFY
# =====================================================
Write-Host "PHASE 8: Launch & verify" -ForegroundColor Cyan

$exePath = Join-Path $InstallDir "HazinaOrchestration.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "  [WARN] Executable not found: $exePath" -ForegroundColor Yellow
    Write-Host "  Cannot launch - please start manually." -ForegroundColor Yellow
} else {
    Start-Process $exePath -WorkingDirectory $InstallDir
    Write-Host "  [OK] Application launched" -ForegroundColor Green

    # Wait for startup
    Start-Sleep -Seconds 5

    $proc = Get-Process HazinaOrchestration -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Host "  [OK] Process running (PID: $($proc.Id))" -ForegroundColor Green
    } else {
        Write-Host "  [WARN] Process not detected - check system tray or logs" -ForegroundColor Yellow
    }

    # Health check
    $baseUrl = "${protocol}://localhost:${Port}"
    $authUser = if ($AuthUsername) { $AuthUsername } else { "admin" }
    $authPass = if ($AuthPassword) { $AuthPassword } else { "changeme" }

    $healthy = Test-HealthEndpoint -BaseUrl $baseUrl -Username $authUser -Password $authPass -MaxRetries 3
    if ($healthy) {
        Write-Host "  [OK] Health check passed" -ForegroundColor Green
    } else {
        Write-Host "  [WARN] Health check failed - service may still be starting" -ForegroundColor Yellow
    }
}

Write-Host ""


# =====================================================
# SUMMARY
# =====================================================
Write-Host "========================================================" -ForegroundColor Green
Write-Host "  SETUP COMPLETE" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Install dir: $InstallDir" -ForegroundColor White
Write-Host "  Mode:        $( if ($useTailscale) { 'HTTPS (Tailscale)' } elseif ($Mode -eq 'custom-cert') { 'HTTPS (custom cert)' } else { 'HTTP' } )" -ForegroundColor White
Write-Host "  Local:       ${protocol}://localhost:${Port}" -ForegroundColor White

if ($tailscaleFQDN) {
    Write-Host "  Tailscale:   https://${tailscaleFQDN}:${Port}" -ForegroundColor White
}
if ($funnelUrl) {
    Write-Host "  Funnel:      $funnelUrl (public)" -ForegroundColor White
}

Write-Host "  Swagger:     ${protocol}://localhost:${Port}/swagger" -ForegroundColor White
Write-Host "  Auth:        $authUser / ****" -ForegroundColor White
Write-Host ""

if ($useTailscale) {
    Write-Host "  NOTE: Tailscale certificates expire after 90 days." -ForegroundColor Yellow
    Write-Host "  Re-run Setup.ps1 to regenerate certificates." -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "  Manage:" -ForegroundColor Cyan
Write-Host "    Right-click tray icon -> Open Dashboard" -ForegroundColor Gray
Write-Host "    Right-click tray icon -> Start with Windows" -ForegroundColor Gray
Write-Host "    Right-click tray icon -> Exit" -ForegroundColor Gray
Write-Host ""
