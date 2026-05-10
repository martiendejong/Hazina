# Hazina Orchestration MSI - Validation Checklist

## Before Building MSI

- [ ] Version updated in `Hazina.Demo.AgenticOrchestration.csproj` → `<Version>X.Y.Z</Version>`
- [ ] Version updated in `Product.wxs` → `<?define ProductVersion = "X.Y.Z" ?>`
- [ ] App published to standard location: `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
- [ ] Published files exist at: `../Hazina.Demo.AgenticOrchestration/bin/Release/net9.0-windows/win-x64/publish/`

## Building MSI

- [ ] Use **Build-Installer.ps1** (NOT Build-MSI-Complete.ps1 or manual WiX commands)
- [ ] Build completes without errors
- [ ] MSI created in `bin/Release/HazinaOrchestrationSetup-YYYYMMDD-HHMMSS.msi`
- [ ] MSI size ~180-190 MB (includes runtime)

## After Building - Automated Validation

Run the validation script:
```powershell
.\Validate-Installer.ps1 -MsiPath ".\bin\Release\HazinaOrchestrationSetup-YYYYMMDD-HHMMSS.msi"
```

This checks:
- [ ] InstallScope = perUser (user folder)
- [ ] Installation directory = LocalAppDataFolder
- [ ] Launch after install action exists
- [ ] NO ServiceInstall components
- [ ] All required files included

## Manual Testing (Required Before Release)

### Installation Test
1. **Install the MSI**
   - [ ] NO admin prompt appears
   - [ ] Installation completes without hanging
   - [ ] Final screen shows "Launch Hazina Orchestration" checkbox

2. **Verify Installation Location**
   - [ ] Files installed to: `%LOCALAPPDATA%\Hazina\Orchestration\`
   - [ ] Start Menu shortcut created: "Hazina Orchestration"
   - [ ] Desktop shortcut (if enabled)

3. **First Run**
   - [ ] App launches from finish screen checkbox
   - [ ] System tray icon appears
   - [ ] Browser opens to https://localhost:5123 (or configured port)
   - [ ] Login screen appears (if auth enabled)

4. **System Tray Functionality**
   - [ ] Right-click tray icon shows menu
   - [ ] "Settings" menu item exists
   - [ ] Settings dialog opens
   - [ ] Can edit configuration:
     - [ ] Authentication (username/password)
     - [ ] Terminal settings
     - [ ] Paths (database, logs)
     - [ ] OpenAI settings
   - [ ] "Save" persists changes to appsettings.json
   - [ ] "Exit" closes app cleanly

5. **Configuration via Setup.ps1** (Optional but Recommended)
   - [ ] Run Setup.ps1 from installer directory
   - [ ] Prompts for username/password
   - [ ] Detects Tailscale (if installed)
   - [ ] Offers to generate certificates
   - [ ] Updates appsettings.json correctly

### Uninstallation Test
1. **Uninstall via Control Panel**
   - [ ] App appears in "Apps & Features"
   - [ ] Uninstall completes cleanly
   - [ ] NO files remain in `%LOCALAPPDATA%\Hazina\Orchestration\`
   - [ ] Start Menu shortcut removed
   - [ ] System tray icon removed (if app was running)

2. **Reinstall Test**
   - [ ] Can reinstall without errors
   - [ ] Settings preserved OR reset cleanly

## Critical Requirements (NEVER CHANGE)

These settings in Product.wxs are **PERMANENT** and must never be changed:

```xml
<!-- REQUIRED: perUser installation -->
<Package InstallScope="perUser" />

<!-- REQUIRED: User folder path -->
<Directory Id="LocalAppDataFolder">
  <Directory Id="ManufacturerFolder" Name="Hazina">
    <Directory Id="INSTALLFOLDER" Name="Orchestration" />
  </Directory>
</Directory>

<!-- REQUIRED: Launch after install -->
<Property Id="WIXUI_EXITDIALOGOPTIONALCHECKBOXTEXT" Value="Launch Hazina Orchestration" />
<CustomAction Id="LaunchApplication" ... />

<!-- CRITICAL: NO ServiceInstall or ServiceControl components -->
<!-- App runs as tray application, NOT Windows Service -->
```

## Known Issues to Avoid

### ❌ Issue #9: Installer Hangs During Installation
- **Cause**: ServiceInstall component in perUser scope
- **Fix**: Remove all ServiceInstall/ServiceControl components
- **Archived**: `archive/Product-Simple.wxs` (DO NOT USE)

### ❌ Issue: Requires Admin Privileges
- **Cause**: InstallScope="perMachine"
- **Fix**: Always use InstallScope="perUser"

### ❌ Issue: App Doesn't Start After Install
- **Cause**: Missing LaunchApplication CustomAction
- **Fix**: Verify Product.wxs has complete launch configuration

### ❌ Issue: Settings Not Editable
- **Cause**: App missing SettingsForm.cs or not initialized
- **Fix**: Verify TrayApplicationContext.cs initializes settings menu

## Release Checklist

Before creating GitHub release:
- [ ] All validation tests pass
- [ ] Manual testing complete
- [ ] Version numbers consistent across files
- [ ] CHANGELOG.md updated
- [ ] MSI tested on clean machine
- [ ] Setup.ps1 tested for configuration

## GitHub Release Process

1. Tag release: `git tag v2.5.0 -a -m "Version 2.5.0"`
2. Push tag: `git push origin v2.5.0`
3. Create release on GitHub
4. Upload MSI from `bin/Release/`
5. Include release notes with:
   - Installation instructions
   - System requirements (.NET 9.0 included)
   - Configuration steps
   - Known issues

## Emergency Rollback

If installer is broken after release:
1. Delete GitHub release
2. Revert to last known working commit
3. Rebuild MSI
4. Re-release with patch version (e.g., 2.5.1)

## Contact

For installer issues:
- Check: `INSTALLER_ARCHITECTURE.md`
- Check: `MSI_INSTALLER_ANALYSIS.md`
- Check: `README-INSTALLER.md`
