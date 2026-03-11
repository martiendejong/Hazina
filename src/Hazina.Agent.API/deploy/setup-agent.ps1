# Hazina Agent Setup Script
# Automates deployment of distributed agent instance

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("jengo-desktop", "jengo-laptop1", "jengo-laptop2", "claude-valsuani", "jesse-pinkman", "agent-diko")]
    [string]$AgentId,

    [Parameter(Mandatory=$true)]
    [string]$OpenAIApiKey,

    [string]$GitRemoteUrl = "https://github.com/martiendejong/Hazina",
    [string]$ConsciousnessRepoUrl = "",  # E:\jengo git remote (if different)
    [string]$InstallPath = "C:\hazina-agent",
    [switch]$SkipGitSetup
)

$ErrorActionPreference = "Stop"

Write-Host "=== Hazina Distributed Agent Setup ===" -ForegroundColor Cyan
Write-Host "Agent ID: $AgentId" -ForegroundColor Yellow
Write-Host "Install Path: $InstallPath" -ForegroundColor Yellow
Write-Host ""

# Step 1: Check prerequisites
Write-Host "[1/8] Checking prerequisites..." -ForegroundColor Green

$dotnetVersion = dotnet --version 2>$null
if (-not $dotnetVersion) {
    Write-Host "ERROR: .NET SDK not found. Install .NET 9.0 SDK first." -ForegroundColor Red
    Write-Host "Download: https://dotnet.microsoft.com/download/dotnet/9.0" -ForegroundColor Yellow
    exit 1
}
Write-Host "  .NET SDK: $dotnetVersion" -ForegroundColor Gray

$gitVersion = git --version 2>$null
if (-not $gitVersion) {
    Write-Host "ERROR: Git not found. Install Git first." -ForegroundColor Red
    Write-Host "Download: https://git-scm.com/downloads" -ForegroundColor Yellow
    exit 1
}
Write-Host "  Git: $gitVersion" -ForegroundColor Gray

# Step 2: Create installation directory
Write-Host "[2/8] Creating installation directory..." -ForegroundColor Green

if (Test-Path $InstallPath) {
    Write-Host "  Directory exists: $InstallPath" -ForegroundColor Gray
} else {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
    Write-Host "  Created: $InstallPath" -ForegroundColor Gray
}

# Step 3: Clone Hazina repository
Write-Host "[3/8] Setting up Hazina repository..." -ForegroundColor Green

$hazinaPath = Join-Path $InstallPath "hazina"

if (Test-Path $hazinaPath) {
    Write-Host "  Repository exists, pulling latest..." -ForegroundColor Gray
    Push-Location $hazinaPath
    git checkout develop 2>&1 | Out-Null
    git pull origin develop 2>&1 | Out-Null
    Pop-Location
} else {
    Write-Host "  Cloning from $GitRemoteUrl..." -ForegroundColor Gray
    git clone $GitRemoteUrl $hazinaPath 2>&1 | Out-Null
    Push-Location $hazinaPath
    git checkout develop 2>&1 | Out-Null
    Pop-Location
}

Write-Host "  Hazina repository ready" -ForegroundColor Gray

# Step 4: Setup consciousness repository (E:\jengo)
Write-Host "[4/8] Setting up consciousness repository..." -ForegroundColor Green

if (-not $SkipGitSetup) {
    $jengoPath = "E:\jengo"

    if (-not (Test-Path "E:\")) {
        Write-Host "  WARNING: E:\ drive not found. Creating C:\jengo instead." -ForegroundColor Yellow
        $jengoPath = "C:\jengo"
    }

    if (Test-Path $jengoPath) {
        Write-Host "  Consciousness repo exists: $jengoPath" -ForegroundColor Gray

        # Check if it's a git repo
        Push-Location $jengoPath
        $isGitRepo = git rev-parse --git-dir 2>$null
        Pop-Location

        if (-not $isGitRepo) {
            Write-Host "  Initializing git repository..." -ForegroundColor Gray
            Push-Location $jengoPath
            git init 2>&1 | Out-Null

            if ($ConsciousnessRepoUrl) {
                git remote add origin $ConsciousnessRepoUrl 2>&1 | Out-Null
                Write-Host "  Remote added: $ConsciousnessRepoUrl" -ForegroundColor Gray
            }
            Pop-Location
        }
    } else {
        Write-Host "  Creating consciousness repository: $jengoPath" -ForegroundColor Gray
        New-Item -ItemType Directory -Path $jengoPath -Force | Out-Null

        Push-Location $jengoPath
        git init 2>&1 | Out-Null

        if ($ConsciousnessRepoUrl) {
            git remote add origin $ConsciousnessRepoUrl 2>&1 | Out-Null
            Write-Host "  Remote added: $ConsciousnessRepoUrl" -ForegroundColor Gray
        }
        Pop-Location
    }

    # Create consciousness directory structure
    $consciousnessDir = Join-Path $jengoPath "consciousness"
    if (-not (Test-Path $consciousnessDir)) {
        New-Item -ItemType Directory -Path $consciousnessDir -Force | Out-Null
        Write-Host "  Created consciousness directory" -ForegroundColor Gray
    }
} else {
    Write-Host "  Skipped (--SkipGitSetup)" -ForegroundColor Gray
}

# Step 5: Configure appsettings
Write-Host "[5/8] Configuring application settings..." -ForegroundColor Green

$apiProjectPath = Join-Path $hazinaPath "src\Hazina.Agent.API"
$appsettingsPath = Join-Path $apiProjectPath "appsettings.json"

$appsettings = @{
    "Logging" = @{
        "LogLevel" = @{
            "Default" = "Information"
            "Microsoft.AspNetCore" = "Warning"
        }
    }
    "AllowedHosts" = "*"
    "OpenAI" = @{
        "ApiKey" = $OpenAIApiKey
    }
}

$appsettings | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath

Write-Host "  Created: $appsettingsPath" -ForegroundColor Gray

# Step 6: Build application
Write-Host "[6/8] Building application..." -ForegroundColor Green

Push-Location $apiProjectPath

$buildOutput = dotnet build --configuration Release 2>&1
$buildSuccess = $LASTEXITCODE -eq 0

if ($buildSuccess) {
    Write-Host "  Build succeeded" -ForegroundColor Gray
} else {
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    Write-Host $buildOutput -ForegroundColor Red
    Pop-Location
    exit 1
}

Pop-Location

# Step 7: Create startup script
Write-Host "[7/8] Creating startup script..." -ForegroundColor Green

$startupScript = @"
# Start Hazina Agent - $AgentId
`$ErrorActionPreference = "Stop"

Write-Host "Starting Hazina Agent: $AgentId" -ForegroundColor Cyan

`$apiPath = "$apiProjectPath"
Push-Location `$apiPath

Write-Host "Starting API server..." -ForegroundColor Yellow
dotnet run --configuration Release

Pop-Location
"@

$startupScriptPath = Join-Path $InstallPath "start-agent.ps1"
$startupScript | Set-Content $startupScriptPath

Write-Host "  Created: $startupScriptPath" -ForegroundColor Gray

# Step 8: Create Windows Service (optional)
Write-Host "[8/8] Creating service configuration..." -ForegroundColor Green

$serviceConfig = @"
# To install as Windows Service:
# 1. Build published executable:
#    dotnet publish -c Release -r win-x64 --self-contained
#
# 2. Use NSSM or sc.exe to create service:
#    nssm install HazinaAgent-$AgentId "dotnet.exe"
#    nssm set HazinaAgent-$AgentId AppDirectory "$apiProjectPath"
#    nssm set HazinaAgent-$AgentId AppParameters "run --configuration Release"
#    nssm start HazinaAgent-$AgentId
#
# Or using sc.exe:
#    sc create HazinaAgent-$AgentId binPath="dotnet.exe run --configuration Release" start=auto
#    sc start HazinaAgent-$AgentId

# Service Name: HazinaAgent-$AgentId
# Description: Hazina Distributed Agent Instance ($AgentId)
# Startup Type: Automatic
# Recovery: Restart on failure
"@

$serviceConfigPath = Join-Path $InstallPath "service-install.txt"
$serviceConfig | Set-Content $serviceConfigPath

Write-Host "  Created: $serviceConfigPath" -ForegroundColor Gray

# Summary
Write-Host ""
Write-Host "=== Setup Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Agent ID: $AgentId" -ForegroundColor Cyan
Write-Host "Install Path: $InstallPath" -ForegroundColor Cyan
Write-Host "API Path: $apiProjectPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Review configuration: $appsettingsPath" -ForegroundColor Gray
Write-Host "  2. Start agent: $startupScriptPath" -ForegroundColor Gray
Write-Host "  3. Verify health: https://localhost:5001/api/agent/health" -ForegroundColor Gray
Write-Host "  4. Check identity: https://localhost:5001/api/agent/identity" -ForegroundColor Gray
Write-Host "  5. Monitor logs for background sync activity" -ForegroundColor Gray
Write-Host ""
Write-Host "To start now:" -ForegroundColor Yellow
Write-Host "  & '$startupScriptPath'" -ForegroundColor Gray
Write-Host ""
