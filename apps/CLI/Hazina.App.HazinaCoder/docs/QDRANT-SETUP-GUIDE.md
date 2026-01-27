# Qdrant Setup Guide for POC 1 Testing

## Status: Docker Desktop Installed ✅

**Date**: 2026-01-26
**Next Steps**: Start Docker and launch Qdrant

---

## Step 1: Complete Docker Setup

### Option A: Restart Terminal (Quick)
Close this terminal and open a new one to refresh PATH.

### Option B: Restart Computer (Recommended)
Docker Desktop may require a system restart to fully initialize.

**Verify Docker is ready:**
```powershell
docker --version
```

Expected output: `Docker version 4.57.0, build ...`

---

## Step 2: Start Qdrant Vector Database

Once Docker is available:

```powershell
# Create data directory for Qdrant persistence
New-Item -ItemType Directory -Force -Path "C:\Projects\hazina\apps\CLI\Hazina.App.HazinaCoder\data\qdrant"

# Start Qdrant container
docker run -d `
  --name qdrant `
  -p 6333:6333 `
  -p 6334:6334 `
  -v C:/Projects/hazina/apps/CLI/Hazina.App.HazinaCoder/data/qdrant:/qdrant/storage `
  qdrant/qdrant
```

**Verify Qdrant is running:**
```powershell
docker ps | findstr qdrant
```

**Test connection:**
```powershell
curl http://localhost:6333/collections
```

Expected: `{"result":{"collections":[]},"status":"ok","time":0.000123}`

---

## Step 3: Set OpenAI API Key

### Option A: Environment Variable (Current Session)
```powershell
$env:OPENAI_API_KEY = "sk-..."
```

### Option B: Load from Secrets File
The application will automatically try to load from:
```
C:\Projects\client-manager\ClientManagerAPI\appsettings.Secrets.json
```

**Verify key is set:**
```powershell
echo $env:OPENAI_API_KEY
```

---

## Step 4: Run POC 1 Tests

```powershell
cd C:\Projects\hazina\apps\CLI\Hazina.App.HazinaCoder
dotnet run
```

### Test Sequence:

**Test 1: Store Preference**
```
> I prefer async/await over Task.Result
> /exit
```

**Test 2: Retrieve Preference**
```
dotnet run
> What's my preference for async programming?
```

Expected: `💡 Based on your preferences: User prefers: async/await over Task.Result (learned X seconds ago)`

---

## Troubleshooting

### Docker daemon not running
```powershell
# Start Docker Desktop manually
Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"

# Wait 30 seconds for Docker to start
Start-Sleep -Seconds 30

# Verify
docker ps
```

### Qdrant connection refused
```powershell
# Check if container is running
docker ps -a | findstr qdrant

# If stopped, restart it
docker start qdrant

# If not exists, create it (see Step 2)
```

### Qdrant container already exists
```powershell
# Remove old container
docker rm -f qdrant

# Create new one (see Step 2)
```

### OpenAI API errors
```powershell
# Verify key is set
echo $env:OPENAI_API_KEY

# Check if key is valid (test with curl)
curl https://api.openai.com/v1/models `
  -H "Authorization: Bearer $env:OPENAI_API_KEY" | ConvertFrom-Json | Select-Object -First 1
```

---

## Quick Reference Commands

| Command | Purpose |
|---------|---------|
| `docker ps` | List running containers |
| `docker logs qdrant` | View Qdrant logs |
| `docker stop qdrant` | Stop Qdrant |
| `docker start qdrant` | Start Qdrant |
| `docker rm qdrant` | Remove Qdrant container |
| `curl http://localhost:6333/collections` | Test Qdrant connection |

---

## Architecture Reminder

```
HazinaCoder POC 1
    ↓
ExperienceStorage (C#)
    ↓
OpenAI API (text-embedding-3-small)
    ↓ (1536-dim vectors)
Qdrant Vector DB (localhost:6333)
    ↓ (cosine similarity search)
ExperienceRetrieval (C#)
    ↓
User receives personalized response
```

---

## Success Criteria Checklist

- [ ] Docker Desktop running
- [ ] Qdrant container running on port 6333
- [ ] OpenAI API key configured
- [ ] HazinaCoder builds successfully (`dotnet build`)
- [ ] Store preference test passes
- [ ] Retrieve preference test passes
- [ ] Cross-session persistence test passes (restart, preferences still available)

---

## Next Steps After POC 1 Success

Once all tests pass:
1. **POC 2**: Automatic code pattern capture
2. **POC 3**: Error resolution memory
3. **POC 4**: Project context awareness
4. **POC 5**: User insight detection

---

**Installation Status**: ✅ Docker Desktop installed (requires restart)
**Qdrant Status**: ⏳ Awaiting Docker availability
**OpenAI Status**: ⏳ Awaiting API key configuration
**Build Status**: ✅ Compiles successfully
**Testing Status**: ⏳ Ready to test once dependencies available

---

**Last Updated**: 2026-01-26 09:00
**Author**: Claude Sonnet 4.5 (Autonomous Agent)
