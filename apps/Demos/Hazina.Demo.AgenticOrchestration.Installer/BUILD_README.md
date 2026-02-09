# Hazina Orchestration MSI - Build Instructions

## Quick Build

```powershell
.\Build-MSI-Complete.ps1
```

**Output:** `bin\Release\HazinaOrchestrationSetup.msi` (75.95 MB)

---

## What This Script Does

1. **Downloads WiX Toolset** (portable, ~3 MB) - no installation needed
2. **Publishes the app** with .NET 9 (self-contained, single file)
3. **Builds the MSI** using WiX 3.14

**Time:** ~2 minutes on first run, ~1 minute on subsequent builds

---

## Requirements

- **PowerShell** (built into Windows)
- **.NET 9 SDK** (for building the app)
- **Internet connection** (first time only, to download WiX)

**No admin rights needed!** (unless .NET Framework 3.5 is missing)

---

## Build Output

```
Hazina.Demo.AgenticOrchestration.Installer/
├── bin/Release/
│   └── HazinaOrchestrationSetup.msi  ← The installer (75.95 MB)
├── wix-tools/                         ← WiX binaries (downloaded once)
├── obj/Release/                       ← Build artifacts
└── build-output.log                   ← Build log (if using tee)
```

---

## Configuration

All configuration is in the source files:

### Port Configuration
**File:** `apps/Demos/Hazina.Demo.AgenticOrchestration/appsettings.json`
```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5123"  ← Change port here
      }
    }
  }
}
```

### Service Configuration
**File:** `Product-Simple.wxs` (lines 59-67)
```xml
<ServiceInstall Id="ServiceInstaller"
                Type="ownProcess"
                Name="HazinaOrchestration"        ← Service name
                DisplayName="Hazina Agentic Orchestration"
                Description="..."
                Start="auto"                      ← auto | demand | disabled
                Account="LocalSystem"             ← LocalSystem | NetworkService
                ErrorControl="normal" />
```

After making changes, rebuild the MSI.

---

## Troubleshooting Build Errors

### Error: ".NET Framework 3.5 not enabled"

**Why:** WiX 3.14 requires .NET Framework 3.5

**Solution (requires admin):**
```powershell
dism /online /enable-feature /featurename:NetFx3 /all
```

Or enable via Windows Settings:
- Control Panel → Programs → Turn Windows features on or off
- Check ".NET Framework 3.5"

The build script will skip this check if you don't have admin rights and try anyway.

### Error: "Failed to publish application"

**Cause:** .NET 9 SDK not installed

**Solution:** Install from https://dotnet.microsoft.com/download/dotnet/9.0

### Error: "WiX compilation failed"

**Debug:**
```powershell
# Check WiX was downloaded
ls wix-tools\candle.exe
ls wix-tools\light.exe

# Try manual build
cd wix-tools
.\candle.exe ..\Product-Simple.wxs -out ..\obj\Release\ -dPublishDir="C:\Projects\hazina\apps\Demos\Hazina.Demo.AgenticOrchestration\bin\Release\net9.0\win-x64\publish"
.\light.exe ..\obj\Release\Product-Simple.wixobj -out ..\bin\Release\HazinaOrchestrationSetup.msi -sval
```

---

## Manual Build Steps

If the script fails, build manually:

### Step 1: Publish the app
```powershell
cd ..\Hazina.Demo.AgenticOrchestration
dotnet publish --configuration Release --runtime win-x64 --self-contained true
```

### Step 2: Download WiX (if not already present)
```powershell
cd ..\Hazina.Demo.AgenticOrchestration.Installer
Invoke-WebRequest -Uri "https://github.com/wixtoolset/wix3/releases/download/wix314rtm/wix314-binaries.zip" -OutFile "wix-tools.zip"
Expand-Archive wix-tools.zip -DestinationPath wix-tools
```

### Step 3: Build MSI
```powershell
.\wix-tools\candle.exe Product-Simple.wxs -out obj\Release\ -dPublishDir="..\Hazina.Demo.AgenticOrchestration\bin\Release\net9.0\win-x64\publish"
.\wix-tools\light.exe obj\Release\Product-Simple.wixobj -out bin\Release\HazinaOrchestrationSetup.msi -sval
```

---

## Versioning

To change the version number:

**File:** `Product-Simple.wxs` (line 7)
```xml
<?define ProductVersion = "1.0.0" ?>  ← Change version here
```

**Important:** WiX uses the `UpgradeCode` to detect existing installations.
- Same UpgradeCode = MSI will upgrade existing installation
- Different UpgradeCode = MSI will install side-by-side

Current UpgradeCode: `12345678-1234-1234-1234-123456789012`

---

## Distribution

The MSI is **fully self-contained**:
- No .NET Runtime required on target PC
- No external dependencies
- Single file installation

Share the MSI with others:
1. Copy `bin\Release\HazinaOrchestrationSetup.msi` (75.95 MB)
2. Include `INSTALLATION_INSTRUCTIONS.md` for end users

---

## Advanced: Add Files to MSI

To include additional files in the installer:

Edit `Product-Simple.wxs`, add to the `ProductComponents` group:

```xml
<Component Id="MyExtraFile" Guid="*">
  <File Id="ExtraFile"
        Source="$(var.PublishDir)\MyFile.txt"
        KeyPath="yes" />
</Component>
```

Then rebuild with `.\Build-MSI-Complete.ps1`.

---

## Scripts Overview

| Script | Purpose |
|--------|---------|
| `Build-MSI-Complete.ps1` | **Main build script** (all-in-one) |
| `Build-MSI-Fixed.ps1` | Alternative (no emoji characters) |
| `Deploy-Manual.ps1` | Deploy without MSI (direct service install) |
| `install-wix.ps1` | Install WiX via winget (requires admin) |

**Recommended:** Use `Build-MSI-Complete.ps1` - it handles everything automatically.

---

## Performance Tips

### Speed up builds

```powershell
# Skip npm rebuild on every publish
# Edit: Hazina.Demo.AgenticOrchestration.csproj
# Comment out the PublishRunWebpack target (lines 54-66)

# This saves ~30 seconds if React UI hasn't changed
```

### Reduce MSI size

The MSI is 75.95 MB because it's self-contained (includes .NET Runtime).

To create a framework-dependent build (requires .NET on target PC):
```powershell
# Edit Build-MSI-Complete.ps1, change line 114:
--self-contained false  # Instead of true
```

This reduces size to ~10 MB but requires .NET 9 Runtime on target PCs.

---

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Build MSI

on:
  push:
    branches: [main]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'

      - name: Build MSI
        run: |
          cd apps/Demos/Hazina.Demo.AgenticOrchestration.Installer
          .\Build-MSI-Complete.ps1

      - name: Upload MSI
        uses: actions/upload-artifact@v3
        with:
          name: HazinaOrchestrationSetup
          path: apps/Demos/Hazina.Demo.AgenticOrchestration.Installer/bin/Release/HazinaOrchestrationSetup.msi
```

---

## FAQ

**Q: Do I need Visual Studio?**
A: No, just the .NET 9 SDK.

**Q: Can I build on Linux/Mac?**
A: No, WiX and Windows Service installation require Windows.

**Q: How do I sign the MSI?**
A: Use `signtool.exe` from Windows SDK:
```powershell
signtool sign /f MyCertificate.pfx /p Password /t http://timestamp.digicert.com HazinaOrchestrationSetup.msi
```

**Q: Can I customize the installer UI?**
A: Yes, but you'll need the full WiX Toolset (not portable version) and WixUIExtension. Current version has minimal UI for simplicity.

---

**Last Updated:** 2026-02-09
**Build System Version:** 1.0
