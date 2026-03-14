# Hazina Orchestration MSI Installer

## Building the MSI

**Default/Recommended build script:**
```powershell
.\Build-Installer.ps1
```

This builds the **user-folder installer** that:
- ✅ Installs to `%LOCALAPPDATA%\Hazina\Orchestration\`
- ✅ Requires NO admin privileges
- ✅ Runs as tray application (NOT Windows Service)
- ✅ Completes installation properly (does NOT hang)

## Configuration

**Product.wxs** - Main WiX installer definition
- `InstallScope="perUser"` - User folder installation (NO CHANGE NEEDED)
- `LocalAppDataFolder` - Installs to %LOCALAPPDATA% (NO CHANGE NEEDED)
- NO ServiceInstall components - Runs as tray app (NO CHANGE NEEDED)

## Critical Settings (DO NOT CHANGE)

```xml
<!-- KEEP THIS: perUser = user folder, no admin needed -->
<Package InstallScope="perUser" />

<!-- KEEP THIS: User folder path -->
<Directory Id="LocalAppDataFolder">
  <Directory Id="ManufacturerFolder" Name="Hazina">
    <Directory Id="INSTALLFOLDER" Name="Orchestration" />
  </Directory>
</Directory>

<!-- NO ServiceInstall or ServiceControl - keeps it as tray app -->
```

## Version Updates

When releasing a new version:
1. Update `Hazina.Demo.AgenticOrchestration.csproj` - `<Version>X.Y.Z</Version>`
2. Update `Product.wxs` - `<?define ProductVersion = "X.Y.Z" ?>`
3. Run `.\Build-Installer.ps1`
4. Test the MSI installs correctly to user folder
5. Create GitHub release with the MSI

## Archived Files

`archive/` folder contains old/broken configurations:
- **Product-Simple.wxs** - Old service-based installer (CAUSES HANG)
- **Product-Generated.wxs** - Auto-generated version (CAUSES HANG)
- **Build-MSI-Complete.ps1** - Old build with service install (DO NOT USE)

These are kept for reference only. **DO NOT use them for production builds.**

## Troubleshooting

### MSI hangs during installation
- ❌ You're using the old service-based installer
- ✅ Use Build-Installer.ps1 which creates user-folder installer

### Requires admin privileges
- ❌ InstallScope is set to "perMachine"
- ✅ Should be "perUser" in Product.wxs

### App doesn't start
- Check if it's running in system tray (right-click tray icons)
- Launch from: Start Menu → Hazina Orchestration
- Check installation folder: `%LOCALAPPDATA%\Hazina\Orchestration\`

## Build Output

MSI is created at:
```
bin\Release\HazinaOrchestrationSetup-YYYYMMDD-HHMMSS.msi
```

Size: ~183 MB (includes .NET 9.0 runtime, 95MB exe)
