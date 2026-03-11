# Hazina MSI Installer - Architecture

**Version:** 2.7.0
**Date:** 2026-03-02
**Status:** Implemented (v2.6.1 working, v2.7.0 in progress)

---

## 1. Design Decisions

### Single appsettings.json (no Production overlay)

The installer ships ONE `appsettings.json` that contains ALL settings. The old `appsettings.Production.json` is eliminated.

**Rationale:**
- Users expect one config file, not two
- ASP.NET layered config (base + Production) causes confusion
- The ConfigTool writes to one file; having two creates merge conflicts
- The Production file only added Kestrel config and relative paths - these now go in the base

**Merged config structure:**
```json
{
  "Logging": { ... },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Https": { "Url": "https://localhost:5123" }
    }
  },
  "Authentication": { "Enabled": true, "Username": "admin", "Password": "", ... },
  "AgenticOrchestration": {
    "DatabasePath": "data\\agent-activity.db",
    "LogsPath": "logs",
    "Terminal": { "DefaultCommand": "...", "DefaultWorkingDirectory": "..." },
    "SessionLogging": { "BasePath": "logs\\agent-sessions" },
    "Uploads": { "Path": "uploads", "MaxFileSizeMB": 50 },
    ...
  },
  "Swagger": { ... },
  "OpenAI": { "ApiKey": "", ... }
}
```

**Key:** All paths are RELATIVE (portable). Kestrel endpoint uses protocol/host/port from installer dialog.

### ConfigTool for all config writes (Option A - implemented)

All post-install configuration is done via `HazinaConfigTool.exe` custom actions:
- `set-auth` for username/password (via WixSilentExec - password not logged)
- `set-kestrel` for protocol/port
- `set-terminal` for shell command/working directory

All use `Return="ignore"` so config failures don't block installation.

---

## 2. Installer UI Flow

```
Welcome Dialog
    |
    v
License Agreement Dialog
    |
    v
Install Directory Dialog
    |  (default: C:\Program Files\Hazina Orchestration\)
    |
    v
+---------------------------------------------+
|   Configuration Dialog (CredentialsDlg)      |
|                                              |
|   -- Authentication --                       |
|   Username:         [admin________________]  |
|   Password:         [****_________________]  |
|   Confirm Password: [****_________________]  |
|                                              |
|   -- Web Server --                           |
|   Host:             [localhost_____________]  |
|   Port:             [5123_________________]  |
|   [x] Use SSL (HTTPS)                       |
|                                              |
|   -- Terminal --                             |
|   Shell Command:    [C:\scripts\claude...]   |
|   Working Directory:[C:\scripts___________]  |
|                                              |
|   -- Optional --                             |
|   [ ] Install and configure Tailscale        |
|                                              |
|   [Back]         [Next]          [Cancel]    |
+---------------------------------------------+
    |
    v
Verify Ready Dialog
    |  (summary of what will be installed)
    |
    v
[INSTALL] -- Progress Dialog -- file copy + custom actions
    |
    |  Custom actions (in order):
    |    1. KillRunningProcess (immediate)
    |    2. StopOldService / DeleteOldService (immediate)
    |    3. [MSI installs files - single appsettings.json]
    |    4. ConfigureAuth (deferred, WixSilentExec)
    |    5. ConfigureKestrel (deferred, WixQuietExec)
    |    6. ConfigureTerminal (deferred, WixQuietExec)
    |    7. InstallTailscale (deferred, conditional on checkbox)
    |
    v
Exit Dialog
    |  [x] Launch Hazina Orchestration
    |
    v
DONE (launches HazinaOrchestration.exe if checked)
```

### Navigation Graph

```
WelcomeDlg -[Next]-> LicenseAgreementDlg
LicenseAgreementDlg -[Back]-> WelcomeDlg
LicenseAgreementDlg -[Next]-> InstallDirDlg (if license accepted)
InstallDirDlg -[Back]-> LicenseAgreementDlg
InstallDirDlg -[Next]-> CredentialsDlg
CredentialsDlg -[Back]-> InstallDirDlg
CredentialsDlg -[Next]-> VerifyReadyDlg
VerifyReadyDlg -[Back]-> CredentialsDlg (fresh install)
VerifyReadyDlg -[Back]-> MaintenanceTypeDlg (upgrade/modify)
ExitDialog -[Finish]-> LaunchApp (optional) + EndDialog Return
```

---

## 3. WiX Properties

| Property | Dialog Control | Type | Default | Description |
|----------|---------------|------|---------|-------------|
| `AUTH_USERNAME` | TextEdit | Text | `admin` | Web UI login username |
| `AUTH_PASSWORD` | TextEdit | Password | (empty) | Web UI login password |
| `AUTH_PASSWORD_CONFIRM` | TextEdit | Password | (empty) | Password confirmation |
| `HOST_NAME` | TextEdit | Text | `localhost` | Hostname for Kestrel binding |
| `HOST_PORT` | TextEdit | Text | `5123` | Port for Kestrel binding |
| `USE_SSL` | CheckBox | Boolean | `1` (checked) | Use HTTPS instead of HTTP |
| `TERMINAL_COMMAND` | TextEdit | Text | `C:\scripts\claude_agent.bat` | Agent shell command |
| `TERMINAL_WORKDIR` | TextEdit | Text | `C:\scripts` | Agent working directory |
| `INSTALL_TAILSCALE` | CheckBox | Boolean | (empty/unchecked) | Install Tailscale after setup |
| `INSTALLFOLDER` | InstallDirDlg | Path | `C:\Program Files\Hazina Orchestration\` | Installation directory |

### Property XML

```xml
<Property Id="AUTH_USERNAME" Value="admin" Secure="yes" />
<Property Id="AUTH_PASSWORD" Secure="yes" Hidden="yes" />
<Property Id="AUTH_PASSWORD_CONFIRM" Secure="yes" Hidden="yes" />
<Property Id="HOST_NAME" Value="localhost" Secure="yes" />
<Property Id="HOST_PORT" Value="5123" Secure="yes" />
<Property Id="USE_SSL" Value="1" Secure="yes" />
<Property Id="TERMINAL_COMMAND" Value="C:\scripts\claude_agent.bat" Secure="yes" />
<Property Id="TERMINAL_WORKDIR" Value="C:\scripts" Secure="yes" />
<Property Id="INSTALL_TAILSCALE" Secure="yes" />
```

**Security notes:**
- `AUTH_PASSWORD` uses `Hidden="yes"` to prevent logging in MSI verbose logs
- `AUTH_PASSWORD` uses `WixSilentExec` (not `WixQuietExec`) for the custom action
- All properties marked `Secure="yes"` to pass through to deferred actions

---

## 4. Custom Actions

### Sequence

```
InstallExecuteSequence:
  1. KillRunningProcess        (Before InstallValidate, immediate, always)
  2. StopOldService            (After Kill, immediate, always)
  3. DeleteOldService          (After Stop, immediate, always)
  --- [MSI installs files] ---
  4. ConfigureAuthArgs         (After InstallFiles, immediate, NOT REMOVE="ALL")
  5. ConfigureAuth             (After #4, deferred WixSilentExec, NOT REMOVE="ALL")
  6. ConfigureKestrelArgs      (After #5, immediate, NOT REMOVE="ALL")
  7. ConfigureKestrel          (After #6, deferred WixQuietExec, NOT REMOVE="ALL")
  8. ConfigureTerminalArgs     (After #7, immediate, NOT REMOVE="ALL")
  9. ConfigureTerminal         (After #8, deferred WixQuietExec, NOT REMOVE="ALL")
  10. InstallTailscaleArgs     (After #9, immediate, INSTALL_TAILSCALE AND NOT REMOVE="ALL")
  11. InstallTailscale         (After #10, deferred WixQuietExec, INSTALL_TAILSCALE AND NOT REMOVE="ALL")
```

### ConfigureAuth (password-safe)

```xml
<CustomAction Id="ConfigureAuthArgs" Property="ConfigureAuth"
  Value="&quot;[INSTALLFOLDER]HazinaConfigTool.exe&quot; set-auth
    --username &quot;[AUTH_USERNAME]&quot;
    --password &quot;[AUTH_PASSWORD]&quot;
    --config &quot;[INSTALLFOLDER]appsettings.json&quot;
    --silent --no-backup" />
<CustomAction Id="ConfigureAuth" BinaryKey="WixCA" DllEntry="WixSilentExec"
  Execute="deferred" Impersonate="no" Return="ignore" />
```

### ConfigureKestrel

The protocol is determined by the USE_SSL checkbox. The immediate action builds the command line:
- If USE_SSL=1: `set-kestrel --protocol https --port [HOST_PORT]`
- If USE_SSL is empty: `set-kestrel --protocol http --port [HOST_PORT]`

Since WiX conditions can't do string concatenation in property values, we use a two-step approach:
1. Set a property `KESTREL_PROTOCOL` based on USE_SSL checkbox value
2. Build the ConfigureKestrel command using that property

**Alternative (simpler):** Always pass https/http based on a conditional CustomAction pair:

```xml
<!-- Set protocol based on checkbox -->
<CustomAction Id="SetProtocolHttps" Property="KESTREL_PROTOCOL" Value="https" />
<CustomAction Id="SetProtocolHttp" Property="KESTREL_PROTOCOL" Value="http" />

<!-- Build kestrel args -->
<CustomAction Id="ConfigureKestrelArgs" Property="ConfigureKestrel"
  Value="&quot;[INSTALLFOLDER]HazinaConfigTool.exe&quot; set-kestrel
    --protocol [KESTREL_PROTOCOL]
    --port [HOST_PORT]
    --config &quot;[INSTALLFOLDER]appsettings.json&quot;
    --silent --no-backup" />
<CustomAction Id="ConfigureKestrel" BinaryKey="WixCA" DllEntry="WixQuietExec"
  Execute="deferred" Impersonate="no" Return="ignore" />
```

Scheduling:
```xml
<Custom Action="SetProtocolHttps" After="InstallFiles">USE_SSL = "1" AND NOT REMOVE="ALL"</Custom>
<Custom Action="SetProtocolHttp" After="InstallFiles">USE_SSL &lt;&gt; "1" AND NOT REMOVE="ALL"</Custom>
<Custom Action="ConfigureKestrelArgs" After="SetProtocolHttp">NOT REMOVE="ALL"</Custom>
<Custom Action="ConfigureKestrel" After="ConfigureKestrelArgs">NOT REMOVE="ALL"</Custom>
```

### ConfigureTerminal

```xml
<CustomAction Id="ConfigureTerminalArgs" Property="ConfigureTerminal"
  Value="&quot;[INSTALLFOLDER]HazinaConfigTool.exe&quot; set-terminal
    --command &quot;[TERMINAL_COMMAND]&quot;
    --workdir &quot;[TERMINAL_WORKDIR]&quot;
    --config &quot;[INSTALLFOLDER]appsettings.json&quot;
    --silent --no-backup" />
<CustomAction Id="ConfigureTerminal" BinaryKey="WixCA" DllEntry="WixQuietExec"
  Execute="deferred" Impersonate="no" Return="ignore" />
```

### InstallTailscale (conditional)

Only runs if user checked the "Install and configure Tailscale" checkbox.

```xml
<CustomAction Id="InstallTailscaleArgs" Property="InstallTailscale"
  Value="winget install --id Tailscale.Tailscale --silent
    --accept-package-agreements --accept-source-agreements" />
<CustomAction Id="InstallTailscale" BinaryKey="WixCA" DllEntry="WixQuietExec"
  Execute="deferred" Impersonate="no" Return="ignore" />
```

**Condition:** `INSTALL_TAILSCALE AND NOT REMOVE="ALL"`
**Return="ignore":** If winget is not available or install fails, installation continues.
**Note:** Tailscale install requires internet access. If it fails, user can install manually later.

---

## 5. Configuration Dialog Design (CredentialsDialog.wxs)

**File:** `CredentialsDialog.wxs`
**Dialog size:** 370 x 420

```
+----------------------------------------------+ 0
| [Banner Bitmap]                              |
| Configuration Settings                       |
| Enter credentials, server, and terminal...   |
+----------------------------------------------+ 44
|                                              |
| Username:                                    | 55
| [admin___________________________________]   | 68
|                                              |
| Password:                                    | 91
| [*********_______________________________]   | 104
|                                              |
| Confirm Password:                            | 127
| [*********_______________________________]   | 140
|                                              |
| ========== Web Server ==================== | 162
|                                              |
| Host:              Port:                     | 172
| [localhost_______] [5123_]  [x] Use SSL      | 185
|                                              |
| ========== Terminal ====================== | 210
|                                              |
| Shell Command:                               | 222
| [C:\scripts\claude_agent.bat______________]  | 235
|                                              |
| Working Directory:                           | 258
| [C:\scripts______________________________]   | 271
|                                              |
| ========== Optional ====================== | 293
|                                              |
| [ ] Install and configure Tailscale         | 306
|     (requires internet, installs via winget) | 320
|                                              |
+----------------------------------------------+ 380
| [Back]          [Next]           [Cancel]    | 393
+----------------------------------------------+ 410
```

### Control Layout (approximate Y coordinates)

| Y | Control | Property | Notes |
|---|---------|----------|-------|
| 0-44 | Banner + title + description | | Standard WiX banner |
| 55 | Username label | | |
| 68 | Username edit | AUTH_USERNAME | |
| 91 | Password label | | |
| 104 | Password edit | AUTH_PASSWORD | Password="yes" |
| 127 | Confirm Password label | | |
| 140 | Confirm Password edit | AUTH_PASSWORD_CONFIRM | Password="yes" |
| 162 | Web Server separator line | | |
| 168 | "Web Server" header text | | Bold |
| 182 | Host label | | |
| 195 | Host edit (width 160) | HOST_NAME | |
| 182 | Port label (X=195) | | |
| 195 | Port edit (X=195, width 60) | HOST_PORT | |
| 197 | SSL checkbox (X=270) | USE_SSL | CheckBoxValue="1" |
| 218 | Terminal separator line | | |
| 224 | "Terminal" header text | | Bold |
| 240 | Shell Command label | | |
| 253 | Shell Command edit | TERMINAL_COMMAND | |
| 276 | Working Directory label | | |
| 289 | Working Directory edit | TERMINAL_WORKDIR | |
| 310 | Optional separator line | | |
| 322 | Tailscale checkbox | INSTALL_TAILSCALE | CheckBoxValue="1" |
| 336 | Tailscale info text | | Small gray text |
| 380 | Bottom line | | |
| 393 | Back / Next / Cancel buttons | | |

---

## 6. Files in MSI

### Included in package

| File | Role | Source |
|------|------|--------|
| `HazinaOrchestration.exe` | Main application | dotnet publish |
| `appsettings.json` | **Single** config file | Source (merged) |
| `HazinaConfigTool.exe` | CLI configuration tool | dotnet publish (ConfigTool project) |
| `Setup.ps1` | Post-install script (for Tailscale cert renewal, etc.) | Installer dir |
| `Setup.cmd` | Launcher for Setup.ps1 | Installer dir |
| `Setup-Config.example.json` | Example config for unattended setup | Installer dir |
| `SetCredentials.ps1` | Legacy script (kept for manual use) | Installer dir |
| `License.rtf` | License shown in installer | Installer dir |
| `entities.yaml` | Entity definitions | dotnet publish |
| `wwwroot/**` | React SPA (index.html + assets) | dotnet publish |

### NOT included (removed)

| File | Reason |
|------|--------|
| `appsettings.Production.json` | Merged into single appsettings.json |
| `*.pdb` | Debug symbols not needed at runtime |
| `*.Secrets.json` | Sensitive, never ship |
| `*.xml` (doc files) | Not needed at runtime |

---

## 7. Build Script (`Build-MSI-Complete.ps1`)

### Key sections to modify for v2.7.0

1. **Remove appsettings.Production.json from inventory/components** - no longer shipped
2. **Add new properties** (HOST_NAME, HOST_PORT, USE_SSL, INSTALL_TAILSCALE)
3. **Add new custom actions** (ConfigureKestrel, SetProtocol*, InstallTailscale)
4. **Update InstallExecuteSequence** with new actions and conditions
5. **Remove ProductionConfig component** from Feature element

### Source appsettings.json

The source `appsettings.json` in the project must be updated to include Kestrel config with defaults.
The ConfigTool custom actions will overwrite the relevant sections with user-provided values during install.

---

## 8. Testing Checklist

### Pre-install
- [ ] Old version uninstalled cleanly (or upgrade path tested)
- [ ] No ghost products in `Get-WmiObject Win32_Product | Where {$_.Name -like '*Hazina*'}`

### Install
- [ ] MSI builds without errors (candle + light)
- [ ] Welcome -> License -> InstallDir -> Config -> VerifyReady -> Install -> Exit flow works
- [ ] All fields show correct defaults (admin, localhost, 5123, SSL checked, etc.)
- [ ] Password fields are masked
- [ ] Changing host/port/SSL reflects in config
- [ ] Tailscale checkbox is OFF by default
- [ ] Back/Next navigation works through all dialogs
- [ ] Cancel dialog works at every step
- [ ] **Finish button closes the installer** (EndDialog Return event)

### Post-install verification
- [ ] Only ONE `appsettings.json` exists (no `appsettings.Production.json`)
- [ ] `appsettings.json` contains entered username and password
- [ ] `appsettings.json` has correct Kestrel URL (http/https + host + port)
- [ ] `appsettings.json` has correct terminal command and working directory
- [ ] Other config sections are preserved (OpenAI, Swagger, etc.)
- [ ] Start Menu shortcut created
- [ ] Application launches from tray
- [ ] Health endpoint responds at configured URL

### Uninstall
- [ ] Uninstall via Programs & Features works
- [ ] Files removed from install directory
- [ ] Start Menu shortcut removed
- [ ] No ghost entries in registry
- [ ] `Get-WmiObject Win32_Product` shows no Hazina products

### Silent install
```batch
msiexec /i HazinaOrchestrationSetup.msi ^
  AUTH_USERNAME=admin ^
  AUTH_PASSWORD=MySecurePass123 ^
  HOST_NAME=0.0.0.0 ^
  HOST_PORT=8443 ^
  USE_SSL=1 ^
  TERMINAL_COMMAND="claude" ^
  TERMINAL_WORKDIR="C:\workspace" ^
  INSTALL_TAILSCALE=1 ^
  INSTALLFOLDER="D:\Hazina\" ^
  /qn /l*v install.log
```

---

## 9. Version History

| Version | Date | Changes |
|---------|------|---------|
| 2.6.0 | 2026-03-02 | Initial CredentialsDialog with auth + terminal fields |
| 2.6.1 | 2026-03-02 | Fixed: ExitDialog Finish button (added EndDialog Return), Fixed: custom action conditions (NOT REMOVE="ALL" instead of NOT Installed) |
| 2.7.0 | 2026-03-02 | Single appsettings.json (removed Production), added host/port/SSL fields, added Tailscale checkbox, added ConfigureKestrel action |
