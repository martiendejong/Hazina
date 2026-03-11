# Hazina MSI Installer - Problem Analysis

**Date:** 2026-03-01
**Analyzed by:** Jengo (Autonomous Agent)
**Issue:** MSI installer gets stuck at the end and becomes un-exitable

---

## Executive Summary

The MSI installer hangs at the end because it launches `Setup.cmd` (which runs a 1052-line interactive PowerShell script) as part of the installation process. This script can prompt for user input, install software, wait for network connections, and run health checks - all of which block the MSI installer from exiting.

**Root Cause:** Mixing file installation (MSI's job) with complex configuration workflows (should be separate).

---

## Current Architecture

### 1. MSI Installer Components

**File:** `Product-Generated.wxs`

The MSI installer does the following:
1. Kills any running HazinaOrchestration.exe processes
2. Stops and removes old Windows Service (if exists)
3. Installs application files to Program Files
4. Copies setup scripts (Setup.ps1, Setup.cmd, SetCredentials.ps1)
5. Creates data and logs directories
6. **PROBLEM:** Launches `Setup.cmd` at the end via custom action

### 2. The Problematic Custom Action

**Location:** Product-Generated.wxs, lines 88-91, 130-132

```xml
<!-- Launch Setup.cmd via ShellExecute after install -->
<Property Id="WixShellExecTarget" Value="[#SetupCmdFile]" />
<CustomAction Id="LaunchSetup" BinaryKey="WixCA" DllEntry="WixShellExec" Impersonate="yes" />

<!-- Exit dialog: launch setup script -->
<Publish Dialog="ExitDialog" Control="Finish" Event="DoAction" Value="LaunchSetup">
  WIXUI_EXITDIALOGOPTIONALCHECKBOX = 1 and NOT Installed
</Publish>
```

**What this does:**
- When user clicks "Finish" on the exit dialog
- If the checkbox "Configure network access and security (recommended)" is checked (default: YES)
- The MSI launches `Setup.cmd` using WiX's built-in ShellExec custom action

### 3. Setup.cmd

**File:** `Setup.cmd` (5 lines)

```batch
@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup.ps1" -Mode interactive -SkipMsiInstall
pause
```

**What this does:**
- Changes to installation directory
- Launches PowerShell with `Setup.ps1`
- Passes `-Mode interactive` (ALLOWS USER PROMPTS!)
- Passes `-SkipMsiInstall` (doesn't re-run MSI install)
- **PAUSES at the end** (waits for user to press a key)

### 4. Setup.ps1

**File:** `Setup.ps1` (1052 lines, 8 phases)

**Phase 1:** Pre-flight checks
**Phase 2:** Tailscale detection & setup
- Can install Tailscale via winget or MSI download
- This can take several minutes

**Phase 3:** Tailscale connection verification
- Waits up to 5 minutes for Tailscale connection
- Polls every 5 seconds

**Phase 4:** Certificate generation
- Runs `tailscale cert` command
- Network operation, can fail/timeout

**Phase 5:** MSI build & install
- Skipped when called with `-SkipMsiInstall`

**Phase 6:** Configuration
- Writes `appsettings.json` and `appsettings.Production.json`
- This is the actual configuration work

**Phase 7:** Funnel (optional)
- Can prompt user: "Enable Funnel? (Y/N)"
- **INTERACTIVE - blocks waiting for input**

**Phase 8:** Launch & verify
- Starts HazinaOrchestration.exe
- Runs health checks with 3 retries and exponential backoff
- Each retry waits 2^n seconds

**Total potential wait time:**
- Tailscale install: 1-5 minutes
- Tailscale connection: up to 5 minutes
- Certificate generation: 30-60 seconds
- Health checks: 3 retries × exponential backoff = up to 14 seconds
- User prompts: **INDEFINITE** (waits for user input)

### 5. SetCredentials.ps1

**File:** `SetCredentials.ps1` (148 lines)

**Status:** Referenced in MSI but **NOT CALLED** during installation.

**What it does:**
- Reads `appsettings.Production.json`
- Updates credentials and terminal settings
- Has proper error handling (always exits 0)
- Logs to `C:\Windows\Temp\SetCredentials-*.log`

**Note:** This script is well-designed with proper error handling. It's included in the installer but not actually executed during MSI installation. It appears to be intended for a different workflow.

---

## The "Un-exitable Installer" Problem

### Timeline of Events

1. User runs MSI installer
2. MSI installs files successfully
3. User sees "Completion" dialog with checkbox "Configure network access and security (recommended)" ✓
4. User clicks "Finish"
5. MSI launches `Setup.cmd` via WiX ShellExec
6. `Setup.cmd` launches PowerShell with `Setup.ps1 -Mode interactive`
7. `Setup.ps1` starts running its 8 phases:
   - **IF** Tailscale not found: Prompts "Install Tailscale? [T/H/C]" → **BLOCKS waiting for user input**
   - **IF** Tailscale not connected: Waits up to 5 minutes for connection
   - **IF** in interactive mode: Prompts "Enable Funnel? (Y/N)" → **BLOCKS waiting for user input**
8. MSI installer window is still open, waiting for Setup.ps1 to finish
9. User **CANNOT** close the MSI installer because it's waiting for Setup.ps1
10. Setup.ps1 may be:
    - Waiting for user input (indefinitely)
    - Installing Tailscale (minutes)
    - Waiting for network connection (minutes)
    - Stuck on a failed operation with retry logic

### Why This Is An Anti-Pattern

1. **Separation of Concerns:** MSI should install files, not run complex configuration workflows
2. **User Experience:** User expects "Finish" button to finish the installation, not start a 5-minute interactive wizard
3. **Error Handling:** If Setup.ps1 fails or hangs, the MSI installer can't complete
4. **Rollback:** If configuration fails, you can't roll back the MSI installation
5. **Silent Installs:** This pattern breaks unattended/silent installation scenarios
6. **Testability:** Can't test MSI installation separately from configuration

---

## Scripts Currently Run During MSI Install

### During MSI Execution

1. **KillRunningProcess** custom action:
   - Command: `taskkill.exe /F /IM HazinaOrchestration.exe`
   - Execution: Immediate
   - Return: Ignore errors
   - **Status:** ✅ Fine - simple, non-blocking

2. **StopOldService** custom action:
   - Command: `sc.exe stop HazinaOrchestration`
   - Execution: Immediate
   - Return: Ignore errors
   - **Status:** ✅ Fine - simple, non-blocking

3. **DeleteOldService** custom action:
   - Command: `sc.exe delete HazinaOrchestration`
   - Execution: Immediate
   - Return: Ignore errors
   - **Status:** ✅ Fine - simple, non-blocking

### After MSI Execution (Exit Dialog)

4. **LaunchSetup** custom action:
   - Command: Launches `Setup.cmd` via WiX ShellExec
   - Execution: User-triggered (exit dialog checkbox)
   - Return: **BLOCKING** - MSI waits for completion
   - **Status:** ❌ **PROBLEM** - Runs 1052-line interactive script

---

## What Causes The Hang

The `LaunchSetup` custom action is the culprit. Here's what makes it problematic:

1. **Interactive Mode:**
   `Setup.ps1 -Mode interactive` allows `Read-Host` calls, which block waiting for user input.

2. **Long-Running Operations:**
   - Installing Tailscale (winget or MSI download)
   - Waiting for Tailscale connection (up to 5 minutes)
   - Certificate generation (network operation)
   - Health checks with retries

3. **No Timeout:**
   WiX ShellExec has no timeout mechanism. It waits indefinitely.

4. **Pause Command:**
   `Setup.cmd` ends with `pause`, which waits for user to press a key.

5. **Error Scenarios:**
   - Tailscale fails to install → prompts user for choice
   - Network connection fails → waits 5 minutes
   - Certificate generation fails → user must read error and press key
   - Health check fails → retries with backoff

---

## Configuration Files

### Current Configuration Approach

**Setup.ps1** generates two configuration files:

1. **appsettings.json** (full configuration)
   - Kestrel endpoints (HTTP/HTTPS + certificate paths)
   - Authentication (username, password)
   - Terminal settings (command, working directory)
   - Database and logs paths
   - All application settings

2. **appsettings.Production.json** (stripped version)
   - Logging configuration
   - Swagger settings
   - OpenAI settings
   - **Does NOT include:** Kestrel, Authentication, Terminal (these are in main appsettings.json)

**Function:** `Write-AppSettings` (lines 324-464 of Setup.ps1)

### Configuration Values Needed

| Setting | Source | Example |
|---------|--------|---------|
| Protocol | Tailscale detection | http or https |
| Port | Parameter/default | 5123 |
| Certificate path | Generated by Tailscale | tailscale.crt |
| Certificate key | Generated by Tailscale | tailscale.key |
| Auth username | Parameter/default | admin |
| Auth password | Parameter/default | changeme |
| Terminal command | Parameter/default | claude |
| Terminal working dir | Parameter/default | (empty) |
| Database path | Parameter/default | data\agent-activity.db |
| Logs path | Parameter/default | logs |

**All of these can be configured AFTER installation.**

---

## Existing Alternative Scripts

The installer directory contains several other scripts that are NOT causing problems:

1. **SetCredentials.ps1** - Updates credentials in appsettings.Production.json (not called during MSI)
2. **apply-config.ps1** - Applies configuration (not used during MSI)
3. **post-install-config.ps1** - Post-install configuration (not used during MSI)
4. **Build-MSI-Complete.ps1** - Builds the MSI (build-time only)
5. **Deploy-Manual.ps1** - Manual deployment script (not used during MSI)
6. **install-msi.ps1** - Installs the MSI (wrapper script, not used during MSI)
7. **reinstall-clean.ps1** - Clean reinstall (manual script, not used during MSI)

**Note:** Many configuration scripts exist but are not integrated into the MSI workflow. This suggests the team has been trying different approaches to solve the configuration problem.

---

## Recommendations

### Short-Term Fix (Immediate)

1. **Remove the LaunchSetup custom action from MSI**
   - Delete or comment out lines 88-91, 130-132 in Product-Generated.wxs
   - MSI completes and exits cleanly
   - Users can run Setup.ps1 manually if they want Tailscale/HTTPS

2. **Update Exit Dialog Text**
   - Change checkbox text to: "Open Setup Wizard after installation"
   - Make it unchecked by default
   - Users who want advanced configuration can check it

### Long-Term Solution (Recommended)

1. **Create HazinaConfigTool.exe** (dedicated configuration utility)
   - Standalone C# console application
   - Commands:
     * `set-auth --username <user> --password <pass>`
     * `set-paths --database <path> --logs <path>`
     * `set-terminal --command <cmd> --workdir <dir>`
     * `set-kestrel --protocol https --port 5123 --cert <path> --key <path>`
     * `validate` - Check configuration
     * `show` - Display current settings
   - Can be called from MSI (non-blocking)
   - Can be called manually after installation
   - Can be scripted for automation
   - Proper exit codes for error handling

2. **Simplify MSI Installer**
   - ONLY install files and create directories
   - ONLY kill processes and clean up old service
   - NO configuration during install
   - Optional: Call HazinaConfigTool.exe with default values (non-blocking)

3. **Refactor Setup.ps1**
   - Keep Tailscale integration (useful for advanced users)
   - Replace `Write-AppSettings` function with calls to HazinaConfigTool.exe
   - Make it a POST-INSTALL tool, not part of MSI
   - Add `-NonInteractive` mode for scripting

4. **Update Documentation**
   - Installation guide: MSI installs files only
   - Configuration guide: Run Setup.ps1 OR use HazinaConfigTool.exe
   - Quick start: Default configuration works on localhost:5123
   - Advanced setup: Tailscale + HTTPS via Setup.ps1

---

## Files Involved

### WiX Installer Files

- `Product.wxs` - Simple version (not currently used)
- `Product-Generated.wxs` - **ACTIVE** - Generated by Build-MSI-Complete.ps1
- `Product-Simple.wxs` - Simplified version (not currently used)
- `CredentialsDialog.wxs` - Custom credentials dialog (referenced but not shown in UI)

### PowerShell Scripts (Included in MSI)

- `Setup.ps1` - **PROBLEM** - 1052-line interactive configuration wizard
- `Setup.cmd` - **PROBLEM** - Launches Setup.ps1 in interactive mode
- `SetCredentials.ps1` - Credentials update script (not called during MSI)
- `Setup-Config.example.json` - Example configuration file

### Build Scripts (Not included in MSI)

- `Build-MSI-Complete.ps1` - Generates Product-Generated.wxs and builds MSI
- `Build-MSI.ps1` - Older build script
- `Build-MSI-Fixed.ps1` - Older build script
- Multiple deployment and installation helper scripts

### Configuration Files

- `appsettings.json` - Main application configuration (in Hazina.Demo.AgenticOrchestration project)
- `appsettings.Production.json` - Production-specific settings
- `appsettings.Development.json` - Development settings
- `appsettings.Secrets.json` - Secrets (not in MSI)

---

## Conclusion

The MSI installer hangs because it runs a complex, interactive configuration wizard (`Setup.ps1`) as part of the installation completion. This is an anti-pattern that violates separation of concerns and creates a poor user experience.

**The fix is simple:**
Remove the LaunchSetup custom action from the MSI. Let the MSI just install files. Configuration can happen afterward via:
1. HazinaConfigTool.exe (to be created)
2. Setup.ps1 run manually
3. Direct editing of appsettings.json

**The proper solution is:**
Create a dedicated configuration tool (HazinaConfigTool.exe) that can be called programmatically or interactively. This separates installation (MSI's job) from configuration (config tool's job).

---

## Completed Changes (2026-03-01)

1. ✅ **Task #1** - Analysis documented (this file)
2. ✅ **Task #2** - Designed HazinaConfigTool CLI architecture (CONFIGTOOL_DESIGN.md)
3. ✅ **Task #3** - Created HazinaConfigTool.exe (System.CommandLine, JsonNode-based, 1.07 MB)
4. ✅ **Task #4** - Simplified MSI: replaced LaunchSetup (Setup.cmd) with LaunchApp (HazinaOrchestration.exe)
5. ✅ **Task #5** - Updated Setup.ps1 to use HazinaConfigTool for non-destructive config
6. ⏭️ **Task #6** - Test end-to-end workflow (requires MSI rebuild)
7. ✅ **Task #7** - Documentation updated
8. ✅ **Task #8** - Fixed ConfigurationService to use JsonNode (prevents data loss)

### Key Changes Made

- **Build-MSI-Complete.ps1**: Builds HazinaConfigTool, includes it in MSI, replaced `LaunchSetup` with `LaunchApp`
- **Setup.cmd**: Removed `pause` command that blocked indefinitely
- **Setup.ps1**: Phase 6 now uses HazinaConfigTool.exe for non-destructive config (with legacy fallback)
- **ConfigTool**: Created at `Hazina.Demo.AgenticOrchestration.ConfigTool/` with 7 commands
- **MSI exit dialog**: Now says "Launch Hazina Orchestration" instead of "Configure network access"
