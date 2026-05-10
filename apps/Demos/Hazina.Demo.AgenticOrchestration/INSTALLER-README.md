# MSI Installer Location

⚠️ **IMPORTANT: Do NOT create installers in this directory!**

The production MSI installer is located at:
```
../Hazina.Demo.AgenticOrchestration.Installer/
```

## Building the MSI

1. Navigate to the installer project:
   ```powershell
   cd ..\Hazina.Demo.AgenticOrchestration.Installer
   ```

2. Run the build script:
   ```powershell
   .\Build-Installer.ps1
   ```

3. The MSI will be created in:
   ```
   bin\Release\HazinaOrchestrationSetup-YYYYMMDD-HHMMSS.msi
   ```

## Validation

Before releasing ANY MSI installer, run the validation script:
```powershell
.\Validate-Installer.ps1 -MsiPath ".\bin\Release\HazinaOrchestrationSetup-YYYYMMDD-HHMMSS.msi"
```

This ensures:
- ✅ InstallScope = perUser (user folder, not Program Files)
- ✅ Installation directory = LocalAppDataFolder
- ✅ Launch after install functionality exists
- ✅ NO ServiceInstall components (prevents installer hangs)
- ✅ All required files included

## Documentation

See the installer project for complete documentation:
- `README-INSTALLER.md` - Complete installation guide
- `INSTALLER_ARCHITECTURE.md` - Technical architecture
- `INSTALLER-VALIDATION-CHECKLIST.md` - Manual testing checklist
- `Validate-Installer.ps1` - Automated validation script

## Why This Exists

This README prevents accidentally creating installers in the wrong location. The app project and installer project are separate for good reason - keep them that way.
