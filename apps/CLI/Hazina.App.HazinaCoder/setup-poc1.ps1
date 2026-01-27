# POC 1 Setup Script - Automated Qdrant Setup for HazinaCoder Testing
# Usage: .\setup-poc1.ps1

$ErrorActionPreference = "Stop"

Write-Host "`n=== HazinaCoder POC 1 Setup Script ===`n" -ForegroundColor Cyan

# Step 1: Verify Docker is available
Write-Host "Step 1: Checking Docker..." -ForegroundColor Yellow
$dockerCmd = Get-Command docker -ErrorAction SilentlyContinue
if (-not $dockerCmd) {
    Write-Host "[ERROR] Docker not found in PATH" -ForegroundColor Red
    Write-Host "Docker Desktop was installed but requires:" -ForegroundColor Yellow
    Write-Host "  1. Close this terminal and open a new one (to refresh PATH)" -ForegroundColor White
    Write-Host "  2. OR restart your computer" -ForegroundColor White
    Write-Host "`nAfter restart, run this script again: .\setup-poc1.ps1`n" -ForegroundColor Cyan
    exit 1
}

Write-Host "[OK] Docker found: $($dockerCmd.Version)" -ForegroundColor Green

# Step 2: Check if Docker daemon is running
Write-Host "`nStep 2: Checking Docker daemon..." -ForegroundColor Yellow
$dockerRunning = $false
$retries = 0
$maxRetries = 3

while (-not $dockerRunning -and $retries -lt $maxRetries) {
    try {
        docker ps | Out-Null
        $dockerRunning = $true
        Write-Host "[OK] Docker daemon is running" -ForegroundColor Green
    }
    catch {
        $retries++
        if ($retries -eq 1) {
            Write-Host "[WAIT] Docker daemon not running. Starting Docker Desktop..." -ForegroundColor Yellow
            Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"
            Write-Host "Waiting 30 seconds for Docker to start..." -ForegroundColor White
            Start-Sleep -Seconds 30
        }
        elseif ($retries -lt $maxRetries) {
            Write-Host "Still starting... waiting 15 more seconds..." -ForegroundColor White
            Start-Sleep -Seconds 15
        }
        else {
            Write-Host "[ERROR] Docker daemon failed to start" -ForegroundColor Red
            Write-Host "Please start Docker Desktop manually and run this script again" -ForegroundColor Yellow
            exit 1
        }
    }
}

# Step 3: Create data directory
Write-Host "`nStep 3: Creating Qdrant data directory..." -ForegroundColor Yellow
$dataDir = "C:\Projects\hazina\apps\CLI\Hazina.App.HazinaCoder\data\qdrant"
if (-not (Test-Path $dataDir)) {
    New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
    Write-Host "[OK] Created: $dataDir" -ForegroundColor Green
}
else {
    Write-Host "[OK] Directory exists: $dataDir" -ForegroundColor Green
}

# Step 4: Check if Qdrant container exists
Write-Host "`nStep 4: Setting up Qdrant container..." -ForegroundColor Yellow
$qdrantExists = docker ps -a --format "{{.Names}}" | Select-String -Pattern "^qdrant$"

if ($qdrantExists) {
    Write-Host "Qdrant container exists. Checking status..." -ForegroundColor White
    $qdrantRunning = docker ps --format "{{.Names}}" | Select-String -Pattern "^qdrant$"

    if ($qdrantRunning) {
        Write-Host "[OK] Qdrant is already running" -ForegroundColor Green
    }
    else {
        Write-Host "Starting existing Qdrant container..." -ForegroundColor White
        docker start qdrant
        Start-Sleep -Seconds 3
        Write-Host "[OK] Qdrant started" -ForegroundColor Green
    }
}
else {
    Write-Host "Creating new Qdrant container..." -ForegroundColor White
    docker run -d `
        --name qdrant `
        -p 6333:6333 `
        -p 6334:6334 `
        -v C:/Projects/hazina/apps/CLI/Hazina.App.HazinaCoder/data/qdrant:/qdrant/storage `
        qdrant/qdrant

    Start-Sleep -Seconds 5
    Write-Host "[OK] Qdrant container created and running" -ForegroundColor Green
}

# Step 5: Test Qdrant connection
Write-Host "`nStep 5: Testing Qdrant connection..." -ForegroundColor Yellow
$maxConnectionRetries = 5
$connectionSuccess = $false

for ($i = 1; $i -le $maxConnectionRetries; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:6333/collections" -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            $connectionSuccess = $true
            Write-Host "[OK] Qdrant is responding on http://localhost:6333" -ForegroundColor Green
            break
        }
    }
    catch {
        if ($i -lt $maxConnectionRetries) {
            Write-Host "Attempt $i/$maxConnectionRetries failed. Waiting 3 seconds..." -ForegroundColor White
            Start-Sleep -Seconds 3
        }
    }
}

if (-not $connectionSuccess) {
    Write-Host "[ERROR] Could not connect to Qdrant after $maxConnectionRetries attempts" -ForegroundColor Red
    Write-Host "Check Docker logs: docker logs qdrant" -ForegroundColor Yellow
    exit 1
}

# Step 6: Check OpenAI API Key
Write-Host "`nStep 6: Checking OpenAI API Key..." -ForegroundColor Yellow
$openaiKey = $env:OPENAI_API_KEY

if (-not $openaiKey) {
    Write-Host "[WARN] OPENAI_API_KEY not set in environment" -ForegroundColor Yellow
    Write-Host "Checking secrets file..." -ForegroundColor White

    $secretsPath = "C:\Projects\client-manager\ClientManagerAPI\appsettings.Secrets.json"
    if (Test-Path $secretsPath) {
        $secrets = Get-Content $secretsPath | ConvertFrom-Json
        if ($secrets.OpenAI.ApiKey) {
            Write-Host "[OK] Found API key in secrets file (will be loaded by app)" -ForegroundColor Green
        }
        else {
            Write-Host "[ERROR] No API key in secrets file" -ForegroundColor Red
            Write-Host "Set it manually: `$env:OPENAI_API_KEY = 'sk-...'" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "[ERROR] Secrets file not found" -ForegroundColor Red
        Write-Host "Set API key: `$env:OPENAI_API_KEY = 'sk-...'" -ForegroundColor Yellow
    }
}
else {
    Write-Host "[OK] OPENAI_API_KEY is set (length: $($openaiKey.Length) chars)" -ForegroundColor Green
}

# Step 7: Build HazinaCoder
Write-Host "`nStep 7: Building HazinaCoder..." -ForegroundColor Yellow
$buildResult = dotnet build C:\Projects\hazina\apps\CLI\Hazina.App.HazinaCoder\Hazina.App.HazinaCoder.csproj --verbosity quiet 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] Build successful" -ForegroundColor Green
}
else {
    Write-Host "[ERROR] Build failed" -ForegroundColor Red
    Write-Host "Run manually: dotnet build" -ForegroundColor Yellow
    exit 1
}

# Summary
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "=== POC 1 Setup Complete! ===" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Cyan

Write-Host "`nSystem Status:" -ForegroundColor Cyan
Write-Host "  Docker: [OK] Running" -ForegroundColor White
Write-Host "  Qdrant: [OK] Running on http://localhost:6333" -ForegroundColor White
$apiStatus = if ($openaiKey -or (Test-Path $secretsPath)) { "[OK] Configured" } else { "[WARN] Needs configuration" }
Write-Host "  OpenAI: $apiStatus" -ForegroundColor White
Write-Host "  Build: [OK] Successful" -ForegroundColor White

Write-Host "`nReady to Test!" -ForegroundColor Cyan
Write-Host "  cd C:\Projects\hazina\apps\CLI\Hazina.App.HazinaCoder" -ForegroundColor White
Write-Host "  dotnet run" -ForegroundColor White

Write-Host "`nTest Commands:" -ForegroundColor Cyan
Write-Host "  > I prefer async/await over Task.Result" -ForegroundColor White
Write-Host "  > /exit" -ForegroundColor White
Write-Host "  (restart and run again)" -ForegroundColor Gray
Write-Host "  > What's my preference for async programming?" -ForegroundColor White

Write-Host "`nDocumentation:" -ForegroundColor Cyan
Write-Host "  - Testing Guide: docs/POC1-TESTING-GUIDE.md" -ForegroundColor White
Write-Host "  - Setup Guide: docs/QDRANT-SETUP-GUIDE.md" -ForegroundColor White
Write-Host "  - Architecture: docs/POC1-ARCHITECTURE.md" -ForegroundColor White
Write-Host ""
