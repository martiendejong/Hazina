# Deployment Verification Script
# Tests all endpoints and validates agent configuration

param(
    [string]$BaseUrl = "https://localhost:5001",
    [int]$TimeoutSeconds = 10
)

$ErrorActionPreference = "Continue"

Write-Host "=== Hazina Agent Deployment Verification ===" -ForegroundColor Cyan
Write-Host "Target: $BaseUrl" -ForegroundColor Yellow
Write-Host ""

$allPassed = $true

# Test 1: Health Check
Write-Host "[1/6] Testing health endpoint..." -ForegroundColor Green

try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/agent/health" -TimeoutSec $TimeoutSeconds

    if ($response.status -eq "healthy") {
        Write-Host "  PASS: Health check returned healthy status" -ForegroundColor Gray
    } else {
        Write-Host "  FAIL: Health check returned unexpected status: $($response.status)" -ForegroundColor Red
        $allPassed = $false
    }
} catch {
    Write-Host "  FAIL: Health endpoint not responding" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    $allPassed = $false
}

# Test 2: Identity Check
Write-Host "[2/6] Testing identity endpoint..." -ForegroundColor Green

try {
    $identity = Invoke-RestMethod -Uri "$BaseUrl/api/agent/identity" -TimeoutSec $TimeoutSeconds

    if ($identity.agentId) {
        Write-Host "  PASS: Identity retrieved" -ForegroundColor Gray
        Write-Host "    AgentId: $($identity.agentId)" -ForegroundColor DarkGray
        Write-Host "    Machine: $($identity.machineName)" -ForegroundColor DarkGray
        Write-Host "    Core Name: $($identity.core.name)" -ForegroundColor DarkGray
    } else {
        Write-Host "  FAIL: Identity missing agentId" -ForegroundColor Red
        $allPassed = $false
    }
} catch {
    Write-Host "  FAIL: Identity endpoint not responding" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    $allPassed = $false
}

# Test 3: Consciousness State
Write-Host "[3/6] Testing consciousness endpoint..." -ForegroundColor Green

try {
    $consciousness = Invoke-RestMethod -Uri "$BaseUrl/api/agent/consciousness" -TimeoutSec $TimeoutSeconds

    if ($consciousness.version) {
        Write-Host "  PASS: Consciousness state retrieved" -ForegroundColor Gray
        Write-Host "    Version: $($consciousness.version)" -ForegroundColor DarkGray
        Write-Host "    Patterns: $($consciousness.patterns.Count)" -ForegroundColor DarkGray
        Write-Host "    Skills: $($consciousness.skills.Count)" -ForegroundColor DarkGray
        Write-Host "    Errors: $($consciousness.errorPatterns.Count)" -ForegroundColor DarkGray
    } else {
        Write-Host "  FAIL: Consciousness state invalid" -ForegroundColor Red
        $allPassed = $false
    }
} catch {
    Write-Host "  FAIL: Consciousness endpoint not responding" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    $allPassed = $false
}

# Test 4: Stats Endpoint
Write-Host "[4/6] Testing stats endpoint..." -ForegroundColor Green

try {
    $stats = Invoke-RestMethod -Uri "$BaseUrl/api/agent/stats" -TimeoutSec $TimeoutSeconds

    if ($stats.agentId) {
        Write-Host "  PASS: Stats retrieved" -ForegroundColor Gray
        Write-Host "    Sessions: $($stats.sessionCount)" -ForegroundColor DarkGray
        Write-Host "    Last Sync: $($stats.lastSync)" -ForegroundColor DarkGray
        Write-Host "    Avg Confidence: $($stats.consciousness.averageConfidence)" -ForegroundColor DarkGray
    } else {
        Write-Host "  FAIL: Stats missing agentId" -ForegroundColor Red
        $allPassed = $false
    }
} catch {
    Write-Host "  FAIL: Stats endpoint not responding" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    $allPassed = $false
}

# Test 5: Consciousness Repository
Write-Host "[5/6] Checking consciousness repository..." -ForegroundColor Green

$jengoPath = "E:\jengo"
if (-not (Test-Path "E:\")) {
    $jengoPath = "C:\jengo"
}

if (Test-Path $jengoPath) {
    Write-Host "  PASS: Consciousness repo exists at $jengoPath" -ForegroundColor Gray

    # Check if it's a git repo
    Push-Location $jengoPath
    $isGit = git rev-parse --git-dir 2>$null
    Pop-Location

    if ($isGit) {
        Write-Host "    Git repository initialized" -ForegroundColor DarkGray
    } else {
        Write-Host "  WARN: Not a git repository" -ForegroundColor Yellow
    }

    # Check consciousness directory
    $consciousnessDir = Join-Path $jengoPath "consciousness"
    if (Test-Path $consciousnessDir) {
        Write-Host "    Consciousness directory exists" -ForegroundColor DarkGray

        # Check for key files
        $eventsFile = Join-Path $consciousnessDir "events.jsonl"
        $stateFile = Join-Path $consciousnessDir "consciousness_state_v2.json"

        if (Test-Path $eventsFile) {
            $eventCount = (Get-Content $eventsFile | Measure-Object -Line).Lines
            Write-Host "    Events file exists ($eventCount events)" -ForegroundColor DarkGray
        } else {
            Write-Host "    WARN: events.jsonl not found (will be created on first event)" -ForegroundColor Yellow
        }

        if (Test-Path $stateFile) {
            Write-Host "    State file exists" -ForegroundColor DarkGray
        } else {
            Write-Host "    WARN: consciousness_state_v2.json not found (will be created on first sync)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  WARN: Consciousness directory not found" -ForegroundColor Yellow
    }
} else {
    Write-Host "  FAIL: Consciousness repository not found at $jengoPath" -ForegroundColor Red
    $allPassed = $false
}

# Test 6: Background Sync (check logs)
Write-Host "[6/6] Checking background sync activity..." -ForegroundColor Green

Write-Host "  INFO: Background sync runs every 5 minutes" -ForegroundColor Gray
Write-Host "  INFO: Check application logs for:" -ForegroundColor Gray
Write-Host "    - 'BackgroundSyncService starting'" -ForegroundColor DarkGray
Write-Host "    - 'Background sync completed' (every 5 min)" -ForegroundColor DarkGray
Write-Host "  INFO: First sync occurs 30s after app startup" -ForegroundColor Gray

# Summary
Write-Host ""
if ($allPassed) {
    Write-Host "=== All Tests Passed ===" -ForegroundColor Green
    Write-Host ""
    Write-Host "Deployment verified successfully!" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Yellow
    Write-Host "  1. Wait 5 minutes for first background sync" -ForegroundColor Gray
    Write-Host "  2. Check logs for 'Background sync completed'" -ForegroundColor Gray
    Write-Host "  3. Verify consciousness state updates: GET $BaseUrl/api/agent/consciousness" -ForegroundColor Gray
    Write-Host "  4. If multiple agents deployed, verify cross-agent learning" -ForegroundColor Gray
    Write-Host ""
    exit 0
} else {
    Write-Host "=== Some Tests Failed ===" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please review failures above and:" -ForegroundColor Yellow
    Write-Host "  1. Check application logs for errors" -ForegroundColor Gray
    Write-Host "  2. Verify appsettings.json configuration" -ForegroundColor Gray
    Write-Host "  3. Ensure all prerequisites installed (.NET 9.0, Git)" -ForegroundColor Gray
    Write-Host "  4. Check network connectivity" -ForegroundColor Gray
    Write-Host ""
    Write-Host "See DEPLOYMENT_GUIDE.md for troubleshooting" -ForegroundColor Gray
    Write-Host ""
    exit 1
}
