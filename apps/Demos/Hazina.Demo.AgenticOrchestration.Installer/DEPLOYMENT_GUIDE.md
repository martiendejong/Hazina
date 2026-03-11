# Hazina Orchestration Deployment Guide

## Quick Deploy (No MSI Needed)

**Fastest way to get running:**

```powershell
cd C:\Projects\hazina\apps\Demos\Hazina.Demo.AgenticOrchestration.Installer

# Run as Administrator
.\Deploy-Manual.ps1
```

This will:
- ✅ Install to `C:\Program Files\Hazina Orchestration`
- ✅ Register Windows Service
- ✅ Configure firewall (port 5000)
- ✅ Start service automatically

**Access:** http://localhost:5000

---

## MSI Installer (For Distribution)

If you need an MSI for clean installation/uninstallation:

### Step 1: Install WiX Toolset

```powershell
# Option A: Automated install
.\install-wix.ps1

# Option B: Manual download
# https://github.com/wixtoolset/wix3/releases/download/wix314rtm/wix314.exe
```

### Step 2: Build MSI

```powershell
# Close and reopen PowerShell after installing WiX
.\Build-MSI.ps1
```

**Output:** `bin\Release\HazinaOrchestrationSetup.msi`

### Step 3: Install MSI

```powershell
Start-Process "bin\Release\HazinaOrchestrationSetup.msi" -Verb RunAs
```

---

## Troubleshooting Service Startup

### Error: Service won't start

1. **Check Event Viewer:**
   ```powershell
   Get-EventLog -LogName Application -Source "Hazina*" -Newest 10
   ```

2. **Run manually to see errors:**
   ```powershell
   cd "C:\Program Files\Hazina Orchestration"
   .\HazinaOrchestration.exe
   ```

3. **Common issues:**

   **Port 5000 already in use:**
   ```powershell
   netstat -ano | findstr :5000
   ```
   Edit `appsettings.json` to change port to 5001 or 8080.

   **Missing directories:**
   ```powershell
   mkdir C:\scripts\_machine
   mkdir C:\scripts\logs\agent-sessions
   ```

   **Permission errors:**
   ```powershell
   icacls "C:\scripts" /grant Everyone:(OI)(CI)F
   ```

   **OpenAI API key:**
   Edit `C:\Program Files\Hazina Orchestration\appsettings.json`:
   ```json
   {
     "OpenAI": {
       "ApiKey": "YOUR_KEY_HERE"
     }
   }
   ```
   Then restart service:
   ```powershell
   sc stop HazinaOrchestration
   sc start HazinaOrchestration
   ```

### Error: "cannot convert code page to unicode"

This is a PowerShell encoding issue. Fix:

```powershell
# Run as UTF-8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
.\Deploy-Manual.ps1
```

---

## Service Management

```powershell
# Status
sc query HazinaOrchestration

# Start
sc start HazinaOrchestration

# Stop
sc stop HazinaOrchestration

# Restart
sc stop HazinaOrchestration; sc start HazinaOrchestration

# View logs
Get-EventLog -LogName Application -Source "*HazinaOrchestration*" -Newest 20

# Configuration
notepad "C:\Program Files\Hazina Orchestration\appsettings.json"
```

---

## Uninstall

### If installed with MSI:
- Control Panel → Programs and Features → Hazina Agentic Orchestration → Uninstall

### If installed manually:
```powershell
# Stop and remove service
sc stop HazinaOrchestration
sc delete HazinaOrchestration

# Remove files
Remove-Item "C:\Program Files\Hazina Orchestration" -Recurse -Force

# Remove firewall rule
netsh advfirewall firewall delete rule name="Hazina Orchestration HTTP"
```

---

## Configuration

### Default Settings

- **Port:** 5000
- **Auth:** Username=`bosi`, Password=`Th1s1sSp4rt4!`
- **Database:** `C:\scripts\_machine\agent-activity.db`
- **Logs:** `C:\scripts\logs\agent-sessions`

### Change Port

Edit `appsettings.json`:
```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:8080"  // Change here
      }
    }
  }
}
```

Restart service after changes.

### Disable Authentication

Edit `appsettings.json`:
```json
{
  "Authentication": {
    "Enabled": false
  }
}
```

---

## URLs

- **Web UI:** http://localhost:5000
- **Swagger API:** http://localhost:5000/swagger
- **Health Check:** http://localhost:5000/health
- **SignalR Terminal:** ws://localhost:5000/hubs/terminal
- **SignalR Agentic:** ws://localhost:5000/hubs/agentic

---

## System Requirements

- Windows 10/11 or Windows Server 2019+
- .NET 9.0 Runtime (included in self-contained build)
- 100MB disk space
- Administrator privileges (for installation only)

---

## What Gets Installed

```
C:\Program Files\Hazina Orchestration\
├── HazinaOrchestration.exe      (82MB self-contained)
├── appsettings.json
├── entities.yaml
└── wwwroot\                      (React SPA)

C:\scripts\
├── _machine\
│   └── agent-activity.db        (SQLite database)
└── logs\
    └── agent-sessions\           (Session logs)
```

---

## Support

**Service won't start?**
1. Run manually: `"C:\Program Files\Hazina Orchestration\HazinaOrchestration.exe"`
2. Check Event Viewer: Windows Logs → Application
3. Review this guide's Troubleshooting section

**MSI build fails?**
- Ensure WiX Toolset is installed
- Check `$env:WIX` environment variable exists
- Close and reopen PowerShell after installing WiX

**Port conflicts?**
- Change port in `appsettings.json`
- Update firewall rule: `netsh advfirewall firewall add rule name="Hazina" dir=in action=allow protocol=TCP localport=8080`
