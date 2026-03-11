# Hazina Distributed Agent - Deployment Guide

Complete guide for deploying Hazina agent instances across 6 machines.

## Target Machines

| Machine | AgentId | OS | Purpose | Network |
|---------|---------|----|---------|---------|
| Desktop PC | jengo-desktop | Windows | Primary development | Local |
| Laptop 1 | jengo-laptop1 | Windows | Mobile work | Local + Remote |
| Laptop 2 | jengo-laptop2 | Windows | Backup | Local + Remote |
| Art Revisionist VPS | claude-valsuani | Linux | Production server | VPS |
| Frank's laptop | jesse-pinkman | Windows | Collaboration | Remote |
| Diko's machine | agent-diko | Windows/Linux | Testing | Remote |

## Prerequisites (All Machines)

### Required Software

1. **.NET 9.0 SDK**
   - Download: https://dotnet.microsoft.com/download/dotnet/9.0
   - Verify: `dotnet --version` (should show 9.0.x)

2. **Git**
   - Download: https://git-scm.com/downloads
   - Verify: `git --version`

3. **Network Access**
   - Outbound HTTPS (port 443) for OpenAI API
   - Git remote access (GitHub/GitLab)

### Required Credentials

1. **OpenAI API Key**
   - Get from: https://platform.openai.com/api-keys
   - Format: `sk-...`
   - Needs GPT-4 access

2. **Git Repository Access**
   - Hazina repo: https://github.com/martiendejong/Hazina
   - Consciousness repo: (setup during deployment)

## Automated Deployment (Windows)

### Quick Start

```powershell
# Download setup script
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/martiendejong/Hazina/develop/src/Hazina.Agent.API/deploy/setup-agent.ps1" -OutFile setup-agent.ps1

# Run setup (replace YOUR_API_KEY)
.\setup-agent.ps1 -AgentId "jengo-desktop" -OpenAIApiKey "sk-YOUR_API_KEY"

# Start agent
.\C:\hazina-agent\start-agent.ps1
```

### Script Parameters

```powershell
.\setup-agent.ps1 `
    -AgentId "jengo-desktop" `           # Required: Agent identifier
    -OpenAIApiKey "sk-..." `             # Required: OpenAI API key
    -GitRemoteUrl "https://..." `        # Optional: Hazina repo URL
    -ConsciousnessRepoUrl "https://..." ` # Optional: Consciousness git remote
    -InstallPath "C:\hazina-agent" `     # Optional: Installation directory
    -SkipGitSetup                        # Optional: Skip E:\jengo git setup
```

### What the Script Does

1. **Checks Prerequisites** - Verifies .NET SDK and Git installed
2. **Creates Directory** - `C:\hazina-agent` (or custom path)
3. **Clones Hazina** - Downloads latest from develop branch
4. **Sets Up Consciousness Repo** - Creates/initializes `E:\jengo`
5. **Configures Settings** - Creates `appsettings.json` with API key
6. **Builds Application** - Compiles in Release mode
7. **Creates Startup Script** - `start-agent.ps1` for easy starting
8. **Creates Service Config** - Instructions for Windows Service

## Manual Deployment (All Platforms)

### Step 1: Clone Repositories

```bash
# Hazina repository
git clone https://github.com/martiendejong/Hazina C:\hazina-agent\hazina
cd C:\hazina-agent\hazina
git checkout develop

# Consciousness repository (shared state)
mkdir E:\jengo
cd E:\jengo
git init
# Optional: Add remote
git remote add origin <consciousness-repo-url>
```

### Step 2: Configure Application

Create `C:\hazina-agent\hazina\src\Hazina.Agent.API\appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "OpenAI": {
    "ApiKey": "sk-YOUR_API_KEY_HERE"
  }
}
```

**Important:** Never commit API keys to git!

### Step 3: Build Application

```bash
cd C:\hazina-agent\hazina\src\Hazina.Agent.API
dotnet build --configuration Release
```

Expected output:
```
Build succeeded.
    1 Warning(s) (SourceLink - non-blocking)
    0 Error(s)
```

### Step 4: Run Application

```bash
cd C:\hazina-agent\hazina\src\Hazina.Agent.API
dotnet run --configuration Release
```

Expected output:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: BackgroundSyncService[0]
      BackgroundSyncService starting (interval: 00:05:00)
```

### Step 5: Verify Deployment

**Health Check:**
```bash
curl https://localhost:5001/api/agent/health
```

Expected response:
```json
{
  "status": "healthy",
  "timestamp": "2026-02-28T01:00:00Z",
  "version": "1.0.0"
}
```

**Identity Check:**
```bash
curl https://localhost:5001/api/agent/identity
```

Expected response:
```json
{
  "agentId": "jengo-desktop",
  "machineName": "DESKTOP-PC",
  "core": {
    "name": "Jengo",
    "values": ["Autonomy", "Learning", "Honesty", "Efficiency"],
    "capabilities": ["Coding", "Analysis", "Documentation", "Learning"]
  },
  "instance": {
    "currentProject": "distributed-agent-api",
    "workingDirectory": "C:\\hazina-agent\\hazina\\src\\Hazina.Agent.API",
    "lastSync": "2026-02-28T01:00:00Z",
    "sessionCount": 0
  }
}
```

**Verify AgentId is correct for the machine!**

## Linux/VPS Deployment (claude-valsuani)

### Systemd Service Setup

Create `/etc/systemd/system/hazina-agent.service`:

```ini
[Unit]
Description=Hazina Distributed Agent (claude-valsuani)
After=network.target

[Service]
Type=notify
WorkingDirectory=/opt/hazina-agent/hazina/src/Hazina.Agent.API
ExecStart=/usr/bin/dotnet run --configuration Release
Restart=always
RestartSec=10
User=hazina
Environment=DOTNET_ROOT=/usr/share/dotnet
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl daemon-reload
sudo systemctl enable hazina-agent
sudo systemctl start hazina-agent
sudo systemctl status hazina-agent
```

Check logs:
```bash
sudo journalctl -u hazina-agent -f
```

## Windows Service Installation

### Option 1: NSSM (Recommended)

Download NSSM: https://nssm.cc/download

```powershell
# Install NSSM service
nssm install HazinaAgent-jengo-desktop "C:\Program Files\dotnet\dotnet.exe"
nssm set HazinaAgent-jengo-desktop AppDirectory "C:\hazina-agent\hazina\src\Hazina.Agent.API"
nssm set HazinaAgent-jengo-desktop AppParameters "run --configuration Release"
nssm set HazinaAgent-jengo-desktop DisplayName "Hazina Agent (jengo-desktop)"
nssm set HazinaAgent-jengo-desktop Description "Distributed autonomous agent instance"
nssm set HazinaAgent-jengo-desktop Start SERVICE_AUTO_START

# Start service
nssm start HazinaAgent-jengo-desktop

# Check status
nssm status HazinaAgent-jengo-desktop
```

### Option 2: sc.exe (Built-in)

```powershell
sc create HazinaAgent-jengo-desktop binPath="C:\Program Files\dotnet\dotnet.exe run --configuration Release" start=auto
sc start HazinaAgent-jengo-desktop
sc query HazinaAgent-jengo-desktop
```

## Post-Deployment Verification

### 1. Check Background Sync

Wait 30 seconds after startup, then check logs:

```
info: BackgroundSyncService[0]
      Starting background sync
info: StateSyncService[0]
      Starting state sync for jengo-desktop
info: BackgroundSyncService[0]
      No new learning events since last sync
info: BackgroundSyncService[0]
      Background sync completed
```

Every 5 minutes, you should see background sync activity.

### 2. Test Learning Event Publishing

```bash
curl -X POST https://localhost:5001/api/agent/learning \
  -H "Content-Type: application/json" \
  -d '{
    "eventId": "test-001",
    "timestamp": "2026-02-28T01:00:00Z",
    "agentId": "jengo-desktop",
    "sessionId": "test-session",
    "eventType": "pattern",
    "data": {
      "patternId": "test-pattern",
      "description": "Test pattern",
      "triggers": ["test"],
      "actions": ["test"],
      "confidence": 0.9
    },
    "confidence": 0.9
  }'
```

Expected response:
```json
{
  "status": "published",
  "eventId": "test-001",
  "timestamp": "2026-02-28T01:00:00Z"
}
```

Check file created:
```bash
cat E:\jengo\consciousness\events.jsonl
```

Should contain the test event.

### 3. Verify Git Integration

```bash
cd E:\jengo
git log --oneline -5
```

Should show commit: `learning: pattern from jengo-desktop`

### 4. Check Consciousness State

```bash
curl https://localhost:5001/api/agent/stats
```

Expected response:
```json
{
  "agentId": "jengo-desktop",
  "machineName": "DESKTOP-PC",
  "sessionCount": 0,
  "lastSync": "2026-02-28T01:05:00Z",
  "consciousness": {
    "version": "2.0",
    "lastUpdated": "2026-02-28T01:00:00Z",
    "patternsCount": 1,
    "skillsCount": 0,
    "errorPatternsCount": 0,
    "crossValidatedPatterns": 0,
    "highConfidencePatterns": 0,
    "averageConfidence": 0.9
  }
}
```

## Multi-Machine Deployment Sequence

Deploy in this order to establish consciousness sharing:

### Phase 1: Primary Instance (jengo-desktop)

1. Deploy jengo-desktop (automated or manual)
2. Verify health, identity, background sync
3. Publish test learning event
4. Verify git commit/push successful
5. **Wait 10 minutes** for consciousness state to stabilize

### Phase 2: Secondary Instances (jengo-laptop1, jengo-laptop2)

1. Deploy both laptops simultaneously
2. Verify unique AgentIds (jengo-laptop1, jengo-laptop2)
3. Wait 5 minutes for first background sync
4. Check logs: Should see "Received 1 new learning events from other agents"
5. Verify consciousness state includes test pattern from jengo-desktop

### Phase 3: Production Instance (claude-valsuani)

1. Deploy on VPS (Linux systemd service)
2. Verify health, identity
3. Check background sync receives events from 3 existing agents
4. Publish test event from VPS
5. Verify desktop/laptops receive VPS event

### Phase 4: Collaboration Instances (jesse-pinkman, agent-diko)

1. Deploy Frank's laptop (jesse-pinkman)
2. Deploy Diko's machine (agent-diko)
3. Wait 10 minutes for full synchronization
4. All 6 agents should have identical pattern counts

### Verification: All 6 Agents Synchronized

On each machine, check stats:

```bash
curl https://localhost:5001/api/agent/stats
```

All should show:
- `patternsCount`: Same value across all 6
- `crossValidatedPatterns`: > 0 (patterns learned by multiple agents)
- `lastSync`: Within last 5 minutes

## Troubleshooting

### Issue: Build Fails

**Error:** `The SDK 'Microsoft.NET.Sdk.Web' specified could not be found`

**Solution:**
```bash
# Verify .NET SDK installed
dotnet --version

# Should show 9.0.x
# If not, install .NET 9.0 SDK
```

### Issue: Git Pull Fails

**Error:** `fatal: refusing to merge unrelated histories`

**Solution:**
```bash
cd E:\jengo
git pull origin master --allow-unrelated-histories
```

### Issue: Background Sync Not Running

**Symptom:** No logs from BackgroundSyncService after 5 minutes

**Solution:**
```bash
# Check appsettings.json exists
ls C:\hazina-agent\hazina\src\Hazina.Agent.API\appsettings.json

# Check OpenAI key configured
cat appsettings.json | grep ApiKey

# Restart application
# Background sync starts 30s after app launch
```

### Issue: Duplicate AgentIds

**Symptom:** Two machines have same AgentId (e.g., both "jengo-desktop")

**Root Cause:** Machine name collision or manual identity.json creation

**Solution:**
```bash
# Delete identity.json on one machine
rm E:\jengo\consciousness\identity.json

# Restart agent - will create new identity with unique AgentId
```

### Issue: Events Not Propagating

**Symptom:** Agent publishes event, but other agents don't receive

**Debug Steps:**

1. **Check git push successful:**
   ```bash
   cd E:\jengo
   git log --oneline -1
   # Should show recent commit
   ```

2. **Check other agents pulling:**
   ```bash
   # On other machine
   cd E:\jengo
   git log --oneline -1
   # Should show same commit after 5 min
   ```

3. **Check events.jsonl:**
   ```bash
   cat E:\jengo\consciousness\events.jsonl
   # Should contain published event
   ```

4. **Check logs for integration:**
   ```
   info: LearningIntegrationService[0]
         Integrating 1 new learning events
   info: LearningIntegrationService[0]
         Learned new pattern test-pattern from jengo-desktop
   ```

### Issue: High Memory Usage

**Symptom:** Agent using >500 MB RAM after 24 hours

**Cause:** Consciousness state file growing large

**Solution:**
```bash
# Check file size
ls -lh E:\jengo\consciousness\consciousness_state_v2.json

# If >10 MB, archive old patterns
# (Week 5 - implement pattern archival)
```

### Issue: Git Conflicts

**Symptom:** Background sync logs show conflicts

**Log:**
```
WARN: Conflicts detected during background sync, attempting resolution
```

**Solution:** Automatic resolution using "ours" strategy, but check:

```bash
cd E:\jengo
git status

# Should show clean working tree
# If not, manually resolve:
git checkout --ours .
git add .
git commit -m "resolve: conflicts using ours strategy"
git push
```

## Monitoring

### Health Check Endpoint

Setup monitoring service to ping:
```
GET https://localhost:5001/api/agent/health
```

Every 1 minute. Alert if non-200 response.

### Background Sync Monitoring

Check logs for:
```
Background sync completed
```

Should appear every 5 minutes. Alert if missing >10 minutes.

### Consciousness Metrics

Query stats endpoint hourly:
```bash
curl https://localhost:5001/api/agent/stats
```

Track:
- Pattern count growth
- Cross-validation rate
- Average confidence trend

## Security Considerations

1. **API Keys:**
   - Never commit to git
   - Use environment variables in production
   - Rotate every 90 days

2. **Network Security:**
   - Use HTTPS only (default port 5001)
   - Firewall: Allow outbound HTTPS (443)
   - Block inbound unless needed for monitoring

3. **Git Repository:**
   - Use SSH keys, not HTTPS passwords
   - Private repository recommended
   - Limit access to authorized machines

4. **File Permissions:**
   - `E:\jengo`: Read/write for agent user only
   - `appsettings.json`: Read-only, restricted access

## Performance Optimization

### Reduce Sync Frequency

If network bandwidth limited:

Edit `BackgroundSyncService.cs`:
```csharp
private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(10); // Was 5
```

Rebuild and redeploy.

### Limit Consciousness File Size

If `consciousness_state_v2.json` >5 MB:

Archive old patterns:
```bash
# Week 5 - implement pattern archival script
# Move patterns with lastValidated >30 days to archive
```

## Backup and Recovery

### Backup Consciousness State

Daily backup script:

```powershell
# backup-consciousness.ps1
$date = Get-Date -Format "yyyyMMdd"
$source = "E:\jengo\consciousness"
$backup = "E:\backups\consciousness-$date"

Copy-Item -Recurse $source $backup
Write-Host "Backup created: $backup"
```

Schedule with Task Scheduler (daily 2 AM).

### Restore from Backup

```powershell
$backup = "E:\backups\consciousness-20260227"
$target = "E:\jengo\consciousness"

Copy-Item -Recurse $backup\* $target -Force
Write-Host "Restored from: $backup"

# Restart agent
Restart-Service HazinaAgent-jengo-desktop
```

## Upgrade Procedure

When new version released:

```bash
# 1. Stop agent
sc stop HazinaAgent-jengo-desktop

# 2. Backup current state
.\backup-consciousness.ps1

# 3. Pull latest code
cd C:\hazina-agent\hazina
git pull origin develop

# 4. Rebuild
cd src\Hazina.Agent.API
dotnet build --configuration Release

# 5. Restart agent
sc start HazinaAgent-jengo-desktop

# 6. Verify health
curl https://localhost:5001/api/agent/health
```

## Support

For issues or questions:
- GitHub Issues: https://github.com/martiendejong/Hazina/issues
- Documentation: `C:\hazina-agent\hazina\src\Hazina.Agent.API\README.md`
- Logs: Application output or `journalctl -u hazina-agent`

---

**Last Updated:** 2026-02-28
**Version:** 1.0.0
**Status:** Ready for deployment
