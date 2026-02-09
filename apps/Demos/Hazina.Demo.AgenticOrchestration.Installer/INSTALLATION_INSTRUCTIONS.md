# Hazina Agentic Orchestration - Installation Guide

## Quick Installation

1. **Double-click** `HazinaOrchestrationSetup.msi`
2. Follow the wizard (Next → Next → Install)
3. Service starts automatically after installation

**Done!** Open your browser to: http://localhost:5123

---

## What Gets Installed

- **Location:** `C:\Program Files\Hazina Orchestration\`
- **Service:** HazinaOrchestration (automatic startup)
- **Port:** 5123 (HTTP)
- **Authentication:** Username=`bosi`, Password=`Th1s1sSp4rt4!`

---

## Access the Application

| Feature | URL |
|---------|-----|
| **Web UI** | http://localhost:5123 |
| **API Docs** | http://localhost:5123/swagger |
| **Health Check** | http://localhost:5123/health |

---

## Service Management

```powershell
# Check status
sc query HazinaOrchestration

# Start service
sc start HazinaOrchestration

# Stop service
sc stop HazinaOrchestration

# Restart service
sc stop HazinaOrchestration && sc start HazinaOrchestration
```

---

## Troubleshooting

### Service Won't Start

1. **Check Event Viewer:**
   - Open: `eventvwr.msc`
   - Go to: Windows Logs → Application
   - Look for errors from "HazinaOrchestration"

2. **Port 5123 already in use?**
   ```powershell
   # Check what's using port 5123
   netstat -ano | findstr :5123
   ```

3. **Run manually to see errors:**
   ```powershell
   cd "C:\Program Files\Hazina Orchestration"
   .\HazinaOrchestration.exe
   ```
   Press Ctrl+C to stop, then restart the service.

### Permission Errors

If the service can't access `C:\scripts\` directories:

```powershell
# Create directories if missing
mkdir C:\scripts\_machine
mkdir C:\scripts\logs\agent-sessions

# Grant permissions
icacls "C:\scripts" /grant "NT AUTHORITY\SYSTEM:(OI)(CI)F" /T
```

### Change Port

If port 5123 is occupied:

1. Edit configuration:
   ```powershell
   notepad "C:\Program Files\Hazina Orchestration\appsettings.json"
   ```

2. Change port:
   ```json
   {
     "Kestrel": {
       "Endpoints": {
         "Http": {
           "Url": "http://localhost:8080"  // Change to different port
         }
       }
     }
   }
   ```

3. Restart service:
   ```powershell
   sc stop HazinaOrchestration
   sc start HazinaOrchestration
   ```

---

## System Requirements

- **OS:** Windows 10/11 or Windows Server 2019+
- **Disk Space:** 150 MB
- **RAM:** 256 MB minimum
- **Privileges:** Administrator (for installation only)
- **.NET:** Not required (self-contained build)

---

## Uninstall

### Option 1: Control Panel
1. Open: Control Panel → Programs and Features
2. Find: "Hazina Agentic Orchestration"
3. Click: Uninstall

### Option 2: PowerShell
```powershell
# Find and uninstall
$app = Get-WmiObject -Class Win32_Product | Where-Object { $_.Name -eq "Hazina Agentic Orchestration" }
$app.Uninstall()
```

**Note:** Configuration and data in `C:\scripts\` is preserved after uninstall.

---

## Configuration

### Default Settings

```
Port:     5123
Username: bosi
Password: Th1s1sSp4rt4!
Database: C:\scripts\_machine\agent-activity.db
Logs:     C:\scripts\logs\agent-sessions\
```

### Disable Authentication

Edit `C:\Program Files\Hazina Orchestration\appsettings.json`:

```json
{
  "Authentication": {
    "Enabled": false
  }
}
```

Restart the service after changes.

---

## Features

- **Terminal Management:** Create and manage Claude Code CLI sessions
- **SignalR Real-Time:** Live output streaming
- **REST API:** Full API for automation
- **Session Logging:** Automatic session recording
- **Health Monitoring:** Built-in health checks

---

## Support

**Service won't start?**
- Run manually: `"C:\Program Files\Hazina Orchestration\HazinaOrchestration.exe"`
- Check Event Viewer: Windows Logs → Application
- Verify port 5123 is available: `netstat -ano | findstr :5123`

**Port conflicts?**
- Edit `appsettings.json` and change port to 8080 or 8888
- Restart service after changing configuration

**Permission errors?**
- Ensure `C:\scripts\_machine` and `C:\scripts\logs` exist
- Grant SYSTEM account full permissions to these directories

---

**Built with Hazina Framework**
https://github.com/martiendejong/Hazina
