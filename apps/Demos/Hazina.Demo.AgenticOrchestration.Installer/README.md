# Hazina Agentic Orchestration - MSI Installer

This directory contains the WiX-based MSI installer for Hazina Agentic Orchestration.

## Prerequisites

Before building the MSI, you need:

1. **.NET 9 SDK** - Already installed if you can build Hazina
2. **WiX Toolset v3.11+** - Download from https://wixtoolset.org/releases/

### Installing WiX Toolset

```powershell
# Option 1: Download and install from website
# https://github.com/wixtoolset/wix3/releases/download/wix3112rtm/wix311.exe

# Option 2: Using winget
winget install WiXToolset.WiXToolset

# Option 3: Using Chocolatey
choco install wixtoolset
```

## Building the MSI

### Quick Build (Automated)

```powershell
.\Build-MSI.ps1
```

This script will:
1. Publish the ASP.NET Core app as a self-contained Windows executable
2. Build the WiX installer to create an MSI package
3. Output the MSI to `bin\Release\HazinaOrchestrationSetup.msi`

### Manual Build

```powershell
# Step 1: Publish the app
cd ..\Hazina.Demo.AgenticOrchestration
dotnet publish --configuration Release --runtime win-x64 --self-contained true

# Step 2: Build the MSI
cd ..\Hazina.Demo.AgenticOrchestration.Installer
msbuild Hazina.Demo.AgenticOrchestration.Installer.wixproj /p:Configuration=Release
```

## What the Installer Does

The MSI installer will:

### ✅ Installation Actions
- Install Hazina Orchestration to `C:\Program Files\Hazina Orchestration\`
- Register as a Windows Service named `HazinaOrchestration`
- Configure service to start automatically on boot
- Set up service recovery (automatic restart on failure)
- Create firewall exception for port 5000
- Create required directories:
  - `C:\scripts\_machine\` (with full permissions)
  - `C:\scripts\logs\agent-sessions\` (with full permissions)

### ⚙️ Service Configuration
- **Service Name:** HazinaOrchestration
- **Display Name:** Hazina Agentic Orchestration
- **Start Type:** Automatic
- **Account:** LocalSystem
- **Port:** 5000 (HTTP, localhost only)

### 🔒 No Privilege Errors
The service runs as LocalSystem and binds to `localhost:5000` - **no administrator privileges needed to start the service** after installation.

## Installing the MSI

1. **Run as Administrator** (required for service installation):
   ```powershell
   Start-Process "HazinaOrchestrationSetup.msi" -Verb RunAs
   ```

2. Follow the installation wizard

3. The service will start automatically after installation

## Using the Service

### Service Management

```powershell
# Check service status
sc query HazinaOrchestration

# Start service
sc start HazinaOrchestration

# Stop service
sc stop HazinaOrchestration

# Restart service
sc stop HazinaOrchestration && sc start HazinaOrchestration

# View service configuration
sc qc HazinaOrchestration
```

### Access the Web UI

Open your browser to:
- **Main UI:** http://localhost:5000
- **Swagger API:** http://localhost:5000/swagger
- **Health Check:** http://localhost:5000/health

### Configuration

The configuration file is located at:
```
C:\Program Files\Hazina Orchestration\appsettings.json
```

After modifying configuration, restart the service:
```powershell
sc stop HazinaOrchestration && sc start HazinaOrchestration
```

## Troubleshooting

### Service Won't Start

1. Check Event Viewer:
   - Windows Logs → Application
   - Look for errors from "HazinaOrchestration"

2. Check port 5000 is not in use:
   ```powershell
   netstat -ano | findstr :5000
   ```

3. Run manually to see errors:
   ```powershell
   cd "C:\Program Files\Hazina Orchestration"
   .\HazinaOrchestration.exe
   ```

### Port Already in Use

Edit `appsettings.json` and change the port:
```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5001"  // Change port here
      }
    }
  }
}
```

Then update the firewall rule:
```powershell
netsh advfirewall firewall add rule name="Hazina Orchestration HTTP" dir=in action=allow protocol=TCP localport=5001
```

### Permission Errors

The service should have full access to `C:\scripts\` directories. If you see permission errors:

```powershell
icacls "C:\scripts\_machine" /grant Everyone:(OI)(CI)F
icacls "C:\scripts\logs" /grant Everyone:(OI)(CI)F
```

## Uninstalling

1. **Using Control Panel:**
   - Control Panel → Programs and Features
   - Find "Hazina Agentic Orchestration"
   - Click Uninstall

2. **Using PowerShell:**
   ```powershell
   $app = Get-WmiObject -Class Win32_Product | Where-Object { $_.Name -eq "Hazina Agentic Orchestration" }
   $app.Uninstall()
   ```

The uninstaller will:
- Stop the service
- Remove the service registration
- Delete installed files
- Remove firewall exception
- **Note:** Configuration and data in `C:\scripts\` are preserved

## Upgrading

To upgrade to a newer version:

1. **MSI automatically handles upgrades**:
   - Just run the new MSI installer
   - It will detect the old version and upgrade in place
   - Service will be stopped during upgrade and restarted after

2. **Manual upgrade**:
   ```powershell
   sc stop HazinaOrchestration
   msiexec /x {OLD_PRODUCT_CODE} /qn
   msiexec /i HazinaOrchestrationSetup-v2.msi
   ```

## Development Notes

### WiX Project Structure

- `Product.wxs` - Main installer definition
- `License.rtf` - License agreement shown during installation
- `Build-MSI.ps1` - Automated build script

### Modifying the Installer

To add files to the installer:

1. Edit `Product.wxs`
2. Add `<File>` elements to `<Component>` groups
3. Rebuild with `.\Build-MSI.ps1`

### Changing Service Configuration

Edit the `<ServiceInstall>` element in `Product.wxs`:

```xml
<ServiceInstall Id="ServiceInstaller"
                Type="ownProcess"
                Name="HazinaOrchestration"
                DisplayName="Hazina Agentic Orchestration"
                Description="..."
                Start="auto"           <!-- auto | demand | disabled -->
                Account="LocalSystem"   <!-- LocalSystem | NetworkService | User -->
                ErrorControl="normal"
                Interactive="no">
```

## File Locations After Installation

| Item | Location |
|------|----------|
| Executable | `C:\Program Files\Hazina Orchestration\HazinaOrchestration.exe` |
| Configuration | `C:\Program Files\Hazina Orchestration\appsettings.json` |
| Web Files | `C:\Program Files\Hazina Orchestration\wwwroot\` |
| Database | `C:\scripts\_machine\agent-activity.db` |
| Logs | `C:\scripts\logs\` |
| Session Logs | `C:\scripts\logs\agent-sessions\` |

## Support

For issues with:
- **The installer:** Check this README and WiX Toolset documentation
- **The application:** See main Hazina documentation
- **Service errors:** Check Windows Event Viewer

## License

MIT License - See `License.rtf` for full text
