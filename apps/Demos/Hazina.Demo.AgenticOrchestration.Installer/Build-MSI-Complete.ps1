<#
.SYNOPSIS
    Complete MSI build script for Hazina Agentic Orchestration

.DESCRIPTION
    This script:
    1. Downloads WiX Toolset if not present (portable)
    2. Publishes the ASP.NET Core app (clean single-file)
    3. Dynamically generates WiX source for ALL published files
    4. Builds the MSI installer

    The MSI installs a Windows Service on port 5123 with auto-start.

.PARAMETER Configuration
    Build configuration (default: Release)

.PARAMETER Platform
    Target platform (default: win-x64)

.EXAMPLE
    .\Build-MSI-Complete.ps1
#>

param(
    [string]$Configuration = "Release",
    [string]$Platform = "win-x64"
)

$ErrorActionPreference = "Stop"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "  Hazina Orchestration - MSI Build (Port 5123)" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host ""

# Paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Join-Path (Split-Path -Parent $scriptDir) "Hazina.Demo.AgenticOrchestration"
$appProjectPath = Join-Path $projectDir "Hazina.Demo.AgenticOrchestration.csproj"
$publishDir = Join-Path $projectDir "bin\$Configuration\net9.0\$Platform\publish"
$msiOutputDir = Join-Path $scriptDir "bin\$Configuration"
$wixDir = Join-Path $scriptDir "wix-tools"

Write-Host "Project: $appProjectPath" -ForegroundColor Gray
Write-Host "Publish: $publishDir" -ForegroundColor Gray
Write-Host "Output:  $msiOutputDir" -ForegroundColor Gray
Write-Host ""

# Verify project exists
if (-not (Test-Path $appProjectPath)) {
    Write-Host "[FATAL] Project not found: $appProjectPath" -ForegroundColor Red
    exit 1
}

# ===================================================================
# STEP 1: Download WiX if not present
# ===================================================================
Write-Host "STEP 1: Checking WiX Toolset..." -ForegroundColor Cyan

$candleExe = Join-Path $wixDir "candle.exe"
$lightExe = Join-Path $wixDir "light.exe"

if (-not (Test-Path $candleExe)) {
    Write-Host "  Downloading WiX 3.14 portable..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $wixDir -Force | Out-Null

    $wixUrl = "https://github.com/wixtoolset/wix3/releases/download/wix314rtm/wix314-binaries.zip"
    $wixZip = Join-Path $wixDir "wix-binaries.zip"

    try {
        Invoke-WebRequest -Uri $wixUrl -OutFile $wixZip -UseBasicParsing
        Expand-Archive -Path $wixZip -DestinationPath $wixDir -Force
        Remove-Item $wixZip -Force
        Write-Host "  [OK] WiX extracted" -ForegroundColor Green
    }
    catch {
        Write-Host "  [FAILED] Download failed: $_" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "  [OK] WiX found" -ForegroundColor Green
}
Write-Host ""

# ===================================================================
# STEP 2: Clean publish and rebuild
# ===================================================================
Write-Host "STEP 2: Publishing application..." -ForegroundColor Cyan

# Clean old publish output
if (Test-Path $publishDir) {
    Write-Host "  Cleaning old publish..." -ForegroundColor Gray
    Remove-Item $publishDir -Recurse -Force
}

$publishArgs = @(
    "publish"
    $appProjectPath
    "--configuration", $Configuration
    "--runtime", $Platform
    "--self-contained", "true"
    "/p:PublishSingleFile=true"
    "/p:IncludeNativeLibrariesForSelfExtract=true"
    "/p:EnableCompressionInSingleFile=true"
    "--output", $publishDir
)

Write-Host "  Running: dotnet publish..." -ForegroundColor Gray
& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "  [FAILED] Publish failed" -ForegroundColor Red
    exit 1
}

$exePath = Join-Path $publishDir "HazinaOrchestration.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "  [FAILED] HazinaOrchestration.exe not found" -ForegroundColor Red
    exit 1
}

$exeSize = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
Write-Host "  [OK] Published: HazinaOrchestration.exe ($exeSize MB)" -ForegroundColor Green

# ===================================================================
# STEP 2b: Clean publish directory (remove unwanted files)
# ===================================================================
Write-Host "  Cleaning publish directory..." -ForegroundColor Gray

# Remove secrets, PDBs, XML docs
$removePatterns = @("*.Secrets.json", "*.pdb")
foreach ($pattern in $removePatterns) {
    Get-ChildItem $publishDir -Filter $pattern -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "    Removed: $($_.Name)" -ForegroundColor DarkGray
        Remove-Item $_.FullName -Force
    }
}

# Remove XML doc files (not needed at runtime)
Get-ChildItem $publishDir -Filter "*.xml" -ErrorAction SilentlyContinue | ForEach-Object {
    # Keep web.config, remove all other XML files
    if ($_.Name -ne "web.config") {
        Write-Host "    Removed: $($_.Name)" -ForegroundColor DarkGray
        Remove-Item $_.FullName -Force
    }
}

# Remove stale directories
$removeDirs = @("linux-x64", "win-x64", "publish", "ClientApp")
foreach ($dir in $removeDirs) {
    $dirPath = Join-Path $publishDir $dir
    if (Test-Path $dirPath) {
        Write-Host "    Removed dir: $dir/" -ForegroundColor DarkGray
        Remove-Item $dirPath -Recurse -Force
    }
}

Write-Host ""

# ===================================================================
# STEP 2c: Copy setup files to publish directory
# ===================================================================
Write-Host "  Copying setup files to publish directory..." -ForegroundColor Gray

$setupFiles = @("Setup.ps1", "Setup.cmd", "Setup-Config.example.json", "License.rtf", "SetCredentials.ps1", "SetFilePermissions.ps1", "Fix-Permissions.cmd")
foreach ($sf in $setupFiles) {
    $srcPath = Join-Path $scriptDir $sf
    if (Test-Path $srcPath) {
        Copy-Item $srcPath -Destination $publishDir -Force
        Write-Host "    Copied: $sf" -ForegroundColor DarkGray
    } else {
        Write-Host "    [WARN] Setup file not found: $sf" -ForegroundColor Yellow
    }
}

Write-Host ""

# ===================================================================
# STEP 3: Inventory all published files
# ===================================================================
Write-Host "STEP 3: Inventorying published files..." -ForegroundColor Cyan

# Core files that MUST exist
$coreFiles = @{
    "HazinaOrchestration.exe" = $true
    "appsettings.json" = $true
    "appsettings.Production.json" = $true
}

# Optional core files
$optionalCoreFiles = @(
    "web.config",
    "entities.yaml",
    "HazinaOrchestration.staticwebassets.endpoints.json"
)

# Check core files
foreach ($file in $coreFiles.Keys) {
    $path = Join-Path $publishDir $file
    if (-not (Test-Path $path)) {
        Write-Host "  [FATAL] Missing required file: $file" -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] $file" -ForegroundColor Green
}

# Check optional files
$presentOptional = @()
foreach ($file in $optionalCoreFiles) {
    $path = Join-Path $publishDir $file
    if (Test-Path $path) {
        $presentOptional += $file
        Write-Host "  [OK] $file" -ForegroundColor Green
    } else {
        Write-Host "  [--] $file (not present, skipping)" -ForegroundColor DarkGray
    }
}

# wwwroot files
$wwwrootDir = Join-Path $publishDir "wwwroot"
$assetsDir = Join-Path $wwwrootDir "assets"

if (-not (Test-Path (Join-Path $wwwrootDir "index.html"))) {
    Write-Host "  [FATAL] wwwroot/index.html missing (React SPA not built)" -ForegroundColor Red
    exit 1
}
Write-Host "  [OK] wwwroot/index.html" -ForegroundColor Green

# Discover ALL asset files (JS, CSS, and their .br/.gz compressed variants)
$assetFiles = @()
if (Test-Path $assetsDir) {
    $assetFiles = Get-ChildItem $assetsDir -File | Sort-Object Name
    Write-Host "  [OK] wwwroot/assets/ ($($assetFiles.Count) files)" -ForegroundColor Green
    foreach ($af in $assetFiles) {
        $size = if ($af.Length -gt 1KB) { "$([math]::Round($af.Length / 1KB, 1)) KB" } else { "$($af.Length) B" }
        Write-Host "       $($af.Name) ($size)" -ForegroundColor DarkGray
    }
}

# Discover any other wwwroot files (besides index.html and assets/)
$wwwrootExtraFiles = Get-ChildItem $wwwrootDir -File | Where-Object { $_.Name -ne "index.html" }

Write-Host ""

# ===================================================================
# STEP 4: Generate WiX source dynamically
# ===================================================================
Write-Host "STEP 4: Generating WiX installer definition..." -ForegroundColor Cyan

# Helper: make a WiX-safe ID from a filename
function Get-WixId {
    param([string]$prefix, [string]$name)
    $safe = $name -replace '[^a-zA-Z0-9]', '_'
    return "${prefix}_${safe}"
}

# Helper: generate a deterministic GUID from a string (for stable component GUIDs)
function Get-DeterministicGuid {
    param([string]$seed)
    $md5 = [System.Security.Cryptography.MD5]::Create()
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($seed)
    $hash = $md5.ComputeHash($bytes)
    $guid = [guid]::new($hash)
    return $guid.ToString("D").ToUpper()
}

# Generate complete WiX XML - using absolute paths (no WiX variables = no resolution bugs)
$pubEsc = $publishDir -replace '\\', '\'  # Ensure clean backslashes

# Build the WXS as a .NET StringBuilder for clean string handling
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('<?xml version="1.0" encoding="UTF-8"?>')
[void]$sb.AppendLine('<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi">')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('  <Product Id="*"')
[void]$sb.AppendLine('           Name="Hazina Agentic Orchestration"')
[void]$sb.AppendLine('           Language="1033"')
[void]$sb.AppendLine('           Version="2.3.0"')
[void]$sb.AppendLine('           Manufacturer="Hazina Framework"')
[void]$sb.AppendLine('           UpgradeCode="12345678-1234-1234-1234-123456789012">')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <Package InstallerVersion="500"')
[void]$sb.AppendLine('             Compressed="yes"')
[void]$sb.AppendLine('             InstallScope="perMachine"')
[void]$sb.AppendLine('             Description="Hazina Agentic Orchestration Desktop App"')
[void]$sb.AppendLine('             Comments="Installs Hazina Agentic Orchestration as a desktop tray application (Port 5123)" />')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <MajorUpgrade DowngradeErrorMessage="A newer version of [ProductName] is already installed." />')
[void]$sb.AppendLine('    <MediaTemplate EmbedCab="yes" />')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <!-- Authentication credentials properties -->')
[void]$sb.AppendLine('    <Property Id="AUTH_USERNAME" Value="admin" />')
[void]$sb.AppendLine('    <Property Id="AUTH_PASSWORD" Secure="yes" />')
[void]$sb.AppendLine('    <Property Id="AUTH_PASSWORD_CONFIRM" Secure="yes" />')
[void]$sb.AppendLine('    <Property Id="SHELL_COMMAND" Value="C:\scripts\claude_agent.bat" />')
[void]$sb.AppendLine('    <Property Id="WORKING_DIRECTORY" Value="C:\scripts" />')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <!-- Kill any running HazinaOrchestration.exe before install/upgrade -->')
[void]$sb.AppendLine('    <Property Id="TASKKILL" Value="taskkill.exe" />')
[void]$sb.AppendLine('    <CustomAction Id="KillRunningProcess"')
[void]$sb.AppendLine('                  Property="TASKKILL"')
[void]$sb.AppendLine('                  ExeCommand="/F /IM HazinaOrchestration.exe"')
[void]$sb.AppendLine('                  Execute="immediate"')
[void]$sb.AppendLine('                  Return="ignore" />')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <!-- Remove old Windows Service if it exists (migration from v1) -->')
[void]$sb.AppendLine('    <Property Id="SC_EXE" Value="sc.exe" />')
[void]$sb.AppendLine('    <CustomAction Id="StopOldService"')
[void]$sb.AppendLine('                  Property="SC_EXE"')
[void]$sb.AppendLine('                  ExeCommand="stop HazinaOrchestration"')
[void]$sb.AppendLine('                  Execute="immediate"')
[void]$sb.AppendLine('                  Return="ignore" />')
[void]$sb.AppendLine('    <CustomAction Id="DeleteOldService"')
[void]$sb.AppendLine('                  Property="SC_EXE"')
[void]$sb.AppendLine('                  ExeCommand="delete HazinaOrchestration"')
[void]$sb.AppendLine('                  Execute="immediate"')
[void]$sb.AppendLine('                  Return="ignore" />')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <!-- Custom action to update credentials (immediate execution - properties work!) -->')
[void]$sb.AppendLine('    <Property Id="POWERSHELL_EXE" Value="powershell.exe" />')
[void]$sb.AppendLine('    <CustomAction Id="SetCredentials"')
[void]$sb.AppendLine('                  Property="POWERSHELL_EXE"')
[void]$sb.AppendLine('                  ExeCommand="-ExecutionPolicy Bypass -File &quot;[INSTALLFOLDER]SetCredentials.ps1&quot; -InstallDir &quot;[INSTALLFOLDER]&quot; -Username &quot;[AUTH_USERNAME]&quot; -Password &quot;[AUTH_PASSWORD]&quot; -ShellCommand &quot;[SHELL_COMMAND]&quot; -WorkingDirectory &quot;[WORKING_DIRECTORY]&quot;"')
[void]$sb.AppendLine('                  Execute="immediate"')
[void]$sb.AppendLine('                  Return="check" />')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <!-- Custom action to set file permissions using WixQuietExec pattern -->')
[void]$sb.AppendLine('    <!-- Step 1: Set command line for WixQuietExec (immediate) -->')
[void]$sb.AppendLine('    <CustomAction Id="SetFilePermissions_SetCmdLine"')
[void]$sb.AppendLine('                  Property="SetFilePermissions"')
[void]$sb.AppendLine('                  Value="&quot;powershell.exe&quot; -ExecutionPolicy Bypass -File &quot;[INSTALLFOLDER]SetFilePermissions.ps1&quot; -InstallDirParam [INSTALLFOLDER]"')
[void]$sb.AppendLine('                  Execute="immediate" />')
[void]$sb.AppendLine('    <!-- Step 2: Execute PowerShell via WixQuietExec (deferred with elevation) -->')
[void]$sb.AppendLine('    <CustomAction Id="SetFilePermissions"')
[void]$sb.AppendLine('                  Execute="deferred"')
[void]$sb.AppendLine('                  Impersonate="no"')
[void]$sb.AppendLine('                  BinaryKey="WixCA"')
[void]$sb.AppendLine('                  DllEntry="WixQuietExec"')
[void]$sb.AppendLine('                  Return="check" />')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <InstallExecuteSequence>')
[void]$sb.AppendLine('      <Custom Action="KillRunningProcess" Before="InstallValidate">1</Custom>')
[void]$sb.AppendLine('      <Custom Action="StopOldService" After="KillRunningProcess">1</Custom>')
[void]$sb.AppendLine('      <Custom Action="DeleteOldService" After="StopOldService">1</Custom>')
[void]$sb.AppendLine('      <!-- SetCredentials DISABLED for testing -->')
[void]$sb.AppendLine('      <!-- <Custom Action="SetCredentials" After="InstallFiles">NOT Installed</Custom> -->')
[void]$sb.AppendLine('      <!-- Set file permissions so regular users can modify appsettings.json -->')
[void]$sb.AppendLine('      <Custom Action="SetFilePermissions_SetCmdLine" Before="InstallFinalize">1</Custom>')
[void]$sb.AppendLine('      <Custom Action="SetFilePermissions" After="SetFilePermissions_SetCmdLine">1</Custom>')
[void]$sb.AppendLine('    </InstallExecuteSequence>')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <Feature Id="ProductFeature" Title="Hazina Agentic Orchestration" Level="1">')
[void]$sb.AppendLine('      <ComponentRef Id="MainExecutable" />')
[void]$sb.AppendLine('      <ComponentRef Id="StartMenuShortcut" />')
[void]$sb.AppendLine('      <ComponentRef Id="AppSettingsConfig" />')
[void]$sb.AppendLine('      <ComponentRef Id="ProductionConfig" />')
foreach ($file in $presentOptional) {
    $compId = Get-WixId "Core" $file
    [void]$sb.AppendLine("      <ComponentRef Id=`"$compId`" />")
}
[void]$sb.AppendLine('      <ComponentRef Id="IndexHtml" />')
foreach ($wf in $wwwrootExtraFiles) {
    $compId = Get-WixId "WwwRoot" $wf.Name
    [void]$sb.AppendLine("      <ComponentRef Id=`"$compId`" />")
}
foreach ($af in $assetFiles) {
    $compId = Get-WixId "AssetFile" $af.Name
    [void]$sb.AppendLine("      <ComponentRef Id=`"$compId`" />")
}
[void]$sb.AppendLine('      <ComponentRef Id="SetupScript" />')
[void]$sb.AppendLine('      <ComponentRef Id="SetupCmd" />')
[void]$sb.AppendLine('      <ComponentRef Id="SetupConfigExample" />')
[void]$sb.AppendLine('      <ComponentRef Id="SetCredentialsScript" />')
[void]$sb.AppendLine('      <ComponentRef Id="SetFilePermissionsScript" />')
[void]$sb.AppendLine('      <ComponentRef Id="FixPermissionsCmd" />')
[void]$sb.AppendLine('      <ComponentRef Id="DataDirectory" />')
[void]$sb.AppendLine('      <ComponentRef Id="LogsDirectory" />')
[void]$sb.AppendLine('      <ComponentRef Id="SessionLogsDirectory" />')
[void]$sb.AppendLine('    </Feature>')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <Property Id="WIXUI_INSTALLDIR" Value="INSTALLFOLDER" />')
[void]$sb.AppendLine("    <Icon Id=`"AppIcon`" SourceFile=`"$pubEsc\HazinaOrchestration.exe`" />")
[void]$sb.AppendLine('    <Property Id="ARPPRODUCTICON" Value="AppIcon" />')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <!-- Custom UI sequence (WixUI_Common base + our dialogs) -->')
[void]$sb.AppendLine('    <UIRef Id="WixUI_Common" />')
[void]$sb.AppendLine("    <WixVariable Id=`"WixUILicenseRtf`" Value=`"$pubEsc\License.rtf`" />")
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <!-- Exit dialog checkbox (checked by default) -->')
[void]$sb.AppendLine('    <Property Id="WIXUI_EXITDIALOGOPTIONALCHECKBOXTEXT"')
[void]$sb.AppendLine('              Value="Configure network access and security (recommended)" />')
[void]$sb.AppendLine('    <Property Id="WIXUI_EXITDIALOGOPTIONALCHECKBOX" Value="1" />')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <!-- Launch Setup.ps1 directly (no pause, non-blocking) -->')
[void]$sb.AppendLine('    <Property Id="WixShellExecTarget" Value="powershell.exe" />')
[void]$sb.AppendLine('    <Property Id="WixShellExecParams" Value="-NoProfile -ExecutionPolicy Bypass -File &quot;[INSTALLFOLDER]Setup.ps1&quot; -Mode interactive -SkipMsiInstall" />')
[void]$sb.AppendLine('    <CustomAction Id="LaunchSetup" BinaryKey="WixCA" DllEntry="WixShellExec" Impersonate="yes" />')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <!-- Complete custom UI sequence -->')
[void]$sb.AppendLine('    <UI>')
[void]$sb.AppendLine('      <!-- Font definitions -->')
[void]$sb.AppendLine('      <TextStyle Id="WixUI_Font_Normal" FaceName="Tahoma" Size="8" />')
[void]$sb.AppendLine('      <TextStyle Id="WixUI_Font_Bigger" FaceName="Tahoma" Size="12" />')
[void]$sb.AppendLine('      <TextStyle Id="WixUI_Font_Title" FaceName="Tahoma" Size="9" Bold="yes" />')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('      <!-- Import standard WiX dialogs -->')
[void]$sb.AppendLine('      <DialogRef Id="ErrorDlg" />')
[void]$sb.AppendLine('      <DialogRef Id="FatalError" />')
[void]$sb.AppendLine('      <DialogRef Id="FilesInUse" />')
[void]$sb.AppendLine('      <DialogRef Id="MsiRMFilesInUse" />')
[void]$sb.AppendLine('      <DialogRef Id="PrepareDlg" />')
[void]$sb.AppendLine('      <DialogRef Id="ProgressDlg" />')
[void]$sb.AppendLine('      <DialogRef Id="ResumeDlg" />')
[void]$sb.AppendLine('      <DialogRef Id="UserExit" />')
[void]$sb.AppendLine('      <DialogRef Id="WelcomeDlg" />')
[void]$sb.AppendLine('      <DialogRef Id="LicenseAgreementDlg" />')
[void]$sb.AppendLine('      <DialogRef Id="InstallDirDlg" />')
[void]$sb.AppendLine('      <DialogRef Id="VerifyReadyDlg" />')
[void]$sb.AppendLine('      <DialogRef Id="ExitDialog" />')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('      <!-- Our custom credentials dialog -->')
[void]$sb.AppendLine('      <Dialog Id="CredentialsDlg" Width="370" Height="320" Title="[ProductName] - Configuration">')
[void]$sb.AppendLine('        <Control Id="Title" Type="Text" X="15" Y="6" Width="200" Height="15" Transparent="yes" NoPrefix="yes" Text="{\WixUI_Font_Title}Set Login Credentials" />')
[void]$sb.AppendLine('        <Control Id="Description" Type="Text" X="25" Y="23" Width="280" Height="20" Transparent="yes" NoPrefix="yes" Text="Configure login credentials and terminal settings." />')
[void]$sb.AppendLine('        <Control Id="BannerLine" Type="Line" X="0" Y="44" Width="370" Height="0" />')
[void]$sb.AppendLine('        <Control Id="BottomLine" Type="Line" X="0" Y="284" Width="370" Height="0" />')
[void]$sb.AppendLine('        <Control Id="UsernameLabel" Type="Text" X="20" Y="60" Width="290" Height="15" NoPrefix="yes" Text="Username:" />')
[void]$sb.AppendLine('        <Control Id="UsernameEdit" Type="Edit" X="20" Y="75" Width="320" Height="18" Property="AUTH_USERNAME" />')
[void]$sb.AppendLine('        <Control Id="PasswordLabel" Type="Text" X="20" Y="100" Width="290" Height="15" NoPrefix="yes" Text="Password:" />')
[void]$sb.AppendLine('        <Control Id="PasswordEdit" Type="Edit" X="20" Y="115" Width="320" Height="18" Property="AUTH_PASSWORD" Password="yes" />')
[void]$sb.AppendLine('        <Control Id="ConfirmPasswordLabel" Type="Text" X="20" Y="140" Width="290" Height="15" NoPrefix="yes" Text="Confirm Password:" />')
[void]$sb.AppendLine('        <Control Id="ConfirmPasswordEdit" Type="Edit" X="20" Y="155" Width="320" Height="18" Property="AUTH_PASSWORD_CONFIRM" Password="yes" />')
[void]$sb.AppendLine('        <Control Id="ShellCommandLabel" Type="Text" X="20" Y="180" Width="290" Height="15" NoPrefix="yes" Text="Shell Command:" />')
[void]$sb.AppendLine('        <Control Id="ShellCommandEdit" Type="Edit" X="20" Y="195" Width="320" Height="18" Property="SHELL_COMMAND" />')
[void]$sb.AppendLine('        <Control Id="WorkingDirectoryLabel" Type="Text" X="20" Y="220" Width="290" Height="15" NoPrefix="yes" Text="Working Directory:" />')
[void]$sb.AppendLine('        <Control Id="WorkingDirectoryEdit" Type="Edit" X="20" Y="235" Width="320" Height="18" Property="WORKING_DIRECTORY" />')
[void]$sb.AppendLine('        <Control Id="ErrorText" Type="Text" X="20" Y="260" Width="330" Height="20" NoPrefix="yes" Text="Passwords do not match!" Hidden="yes">')
[void]$sb.AppendLine('          <Condition Action="show">AUTH_PASSWORD &lt;&gt; AUTH_PASSWORD_CONFIRM AND AUTH_PASSWORD_CONFIRM &lt;&gt; ""</Condition>')
[void]$sb.AppendLine('          <Condition Action="hide">AUTH_PASSWORD = AUTH_PASSWORD_CONFIRM OR AUTH_PASSWORD_CONFIRM = ""</Condition>')
[void]$sb.AppendLine('        </Control>')
[void]$sb.AppendLine('        <Control Id="Back" Type="PushButton" X="180" Y="293" Width="56" Height="17" Text="&amp;Back">')
[void]$sb.AppendLine('          <Publish Event="NewDialog" Value="InstallDirDlg">1</Publish>')
[void]$sb.AppendLine('        </Control>')
[void]$sb.AppendLine('        <Control Id="Next" Type="PushButton" X="236" Y="293" Width="56" Height="17" Default="yes" Text="&amp;Next">')
[void]$sb.AppendLine('          <Publish Event="NewDialog" Value="VerifyReadyDlg">AUTH_PASSWORD = AUTH_PASSWORD_CONFIRM</Publish>')
[void]$sb.AppendLine('        </Control>')
[void]$sb.AppendLine('        <Control Id="Cancel" Type="PushButton" X="304" Y="293" Width="56" Height="17" Cancel="yes" Text="Cancel">')
[void]$sb.AppendLine('          <Publish Event="SpawnDialog" Value="CancelDlg">1</Publish>')
[void]$sb.AppendLine('        </Control>')
[void]$sb.AppendLine('      </Dialog>')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('      <!-- COMPLETE navigation graph for fresh install -->')
[void]$sb.AppendLine('      <!-- Welcome -> License -->')
[void]$sb.AppendLine('      <Publish Dialog="WelcomeDlg" Control="Next" Event="NewDialog" Value="LicenseAgreementDlg">NOT Installed</Publish>')
[void]$sb.AppendLine('      <!-- License -> InstallDir -->')
[void]$sb.AppendLine('      <Publish Dialog="LicenseAgreementDlg" Control="Next" Event="NewDialog" Value="InstallDirDlg">LicenseAccepted = "1"</Publish>')
[void]$sb.AppendLine('      <Publish Dialog="LicenseAgreementDlg" Control="Back" Event="NewDialog" Value="WelcomeDlg">1</Publish>')
[void]$sb.AppendLine('      <!-- InstallDir -> Credentials (THE KEY CHANGE) -->')
[void]$sb.AppendLine('      <Publish Dialog="InstallDirDlg" Control="Next" Event="NewDialog" Value="CredentialsDlg">1</Publish>')
[void]$sb.AppendLine('      <Publish Dialog="InstallDirDlg" Control="Back" Event="NewDialog" Value="LicenseAgreementDlg">1</Publish>')
[void]$sb.AppendLine('      <Publish Dialog="InstallDirDlg" Control="ChangeFolder" Property="_BrowseProperty" Value="[WIXUI_INSTALLDIR]" Order="1">1</Publish>')
[void]$sb.AppendLine('      <Publish Dialog="InstallDirDlg" Control="ChangeFolder" Event="SpawnDialog" Value="BrowseDlg" Order="2">1</Publish>')
[void]$sb.AppendLine('      <!-- NOTE: Credentials dialog navigation defined IN the dialog controls above -->')
[void]$sb.AppendLine('      <!-- VerifyReady -> Back to Credentials -->')
[void]$sb.AppendLine('      <Publish Dialog="VerifyReadyDlg" Control="Back" Event="NewDialog" Value="CredentialsDlg" Order="1">NOT Installed</Publish>')
[void]$sb.AppendLine('      <Publish Dialog="VerifyReadyDlg" Control="Back" Event="NewDialog" Value="MaintenanceTypeDlg" Order="2">Installed AND NOT PATCH</Publish>')
[void]$sb.AppendLine('      <!-- Exit dialog: launch setup script -->')
[void]$sb.AppendLine('      <Publish Dialog="ExitDialog" Control="Finish" Event="DoAction" Value="LaunchSetup">')
[void]$sb.AppendLine('        WIXUI_EXITDIALOGOPTIONALCHECKBOX = 1 and NOT Installed')
[void]$sb.AppendLine('      </Publish>')
[void]$sb.AppendLine('    </UI>')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('  </Product>')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('  <!-- Directory Structure -->')
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <Directory Id="TARGETDIR" Name="SourceDir">')
[void]$sb.AppendLine('      <Directory Id="ProgramFilesFolder">')
[void]$sb.AppendLine('        <Directory Id="INSTALLFOLDER" Name="Hazina Orchestration">')
[void]$sb.AppendLine('          <Directory Id="WWWROOT" Name="wwwroot">')
[void]$sb.AppendLine('            <Directory Id="WWWROOT_ASSETS" Name="assets" />')
[void]$sb.AppendLine('          </Directory>')
[void]$sb.AppendLine('          <Directory Id="DATADIR" Name="data" />')
[void]$sb.AppendLine('          <Directory Id="LOGSDIR" Name="logs">')
[void]$sb.AppendLine('            <Directory Id="LOGS_SESSIONS" Name="agent-sessions" />')
[void]$sb.AppendLine('          </Directory>')
[void]$sb.AppendLine('        </Directory>')
[void]$sb.AppendLine('      </Directory>')
[void]$sb.AppendLine('      <Directory Id="ProgramMenuFolder">')
[void]$sb.AppendLine('        <Directory Id="ApplicationProgramsFolder" Name="Hazina Orchestration" />')
[void]$sb.AppendLine('      </Directory>')
[void]$sb.AppendLine('    </Directory>')
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('')

# Main components fragment
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <Component Id="MainExecutable" Directory="INSTALLFOLDER" Guid="11111111-1111-1111-1111-111111111111">')
[void]$sb.AppendLine("      <File Id=`"HazinaOrchestrationExe`" Source=`"$pubEsc\HazinaOrchestration.exe`" KeyPath=`"yes`" />")
[void]$sb.AppendLine('    </Component>')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <!-- Start Menu shortcut -->')
[void]$sb.AppendLine('    <Component Id="StartMenuShortcut" Directory="ApplicationProgramsFolder" Guid="44444444-4444-4444-4444-444444444444">')
[void]$sb.AppendLine('      <Shortcut Id="ApplicationStartMenuShortcut"')
[void]$sb.AppendLine('                Name="Hazina Orchestration"')
[void]$sb.AppendLine('                Description="Hazina Agentic Orchestration Desktop App"')
[void]$sb.AppendLine('                Target="[INSTALLFOLDER]HazinaOrchestration.exe"')
[void]$sb.AppendLine('                WorkingDirectory="INSTALLFOLDER"')
[void]$sb.AppendLine('                Icon="AppIcon" />')
[void]$sb.AppendLine('      <RemoveFolder Id="CleanUpShortcutFolder" On="uninstall" />')
[void]$sb.AppendLine('      <RegistryValue Root="HKCU" Key="Software\Hazina\Orchestration" Name="installed" Type="integer" Value="1" KeyPath="yes" />')
[void]$sb.AppendLine('    </Component>')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <Component Id="AppSettingsConfig" Directory="INSTALLFOLDER" Guid="22222222-2222-2222-2222-222222222222">')
[void]$sb.AppendLine("      <File Id=`"AppSettingsJson`" Source=`"$pubEsc\appsettings.json`" KeyPath=`"yes`" />")
[void]$sb.AppendLine('    </Component>')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <Component Id="ProductionConfig" Directory="INSTALLFOLDER" Guid="33333333-3333-3333-3333-333333333333">')
[void]$sb.AppendLine("      <File Id=`"AppSettingsProductionJson`" Source=`"$pubEsc\appsettings.Production.json`" KeyPath=`"yes`" />")
[void]$sb.AppendLine('    </Component>')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <!-- Setup files (for post-install Tailscale/HTTPS configuration) -->')
[void]$sb.AppendLine('    <Component Id="SetupScript" Directory="INSTALLFOLDER" Guid="55555555-5555-5555-5555-555555555501">')
[void]$sb.AppendLine("      <File Id=`"SetupPs1File`" Source=`"$pubEsc\Setup.ps1`" KeyPath=`"yes`" />")
[void]$sb.AppendLine('    </Component>')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <Component Id="SetupCmd" Directory="INSTALLFOLDER" Guid="55555555-5555-5555-5555-555555555502">')
[void]$sb.AppendLine("      <File Id=`"SetupCmdFile`" Source=`"$pubEsc\Setup.cmd`" KeyPath=`"yes`" />")
[void]$sb.AppendLine('    </Component>')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <Component Id="SetupConfigExample" Directory="INSTALLFOLDER" Guid="55555555-5555-5555-5555-555555555503">')
[void]$sb.AppendLine("      <File Id=`"SetupConfigExampleFile`" Source=`"$pubEsc\Setup-Config.example.json`" KeyPath=`"yes`" />")
[void]$sb.AppendLine('    </Component>')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <Component Id="SetCredentialsScript" Directory="INSTALLFOLDER" Guid="55555555-5555-5555-5555-555555555504">')
[void]$sb.AppendLine("      <File Id=`"SetCredentialsPs1File`" Source=`"$pubEsc\SetCredentials.ps1`" KeyPath=`"yes`" />")
[void]$sb.AppendLine('    </Component>')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <Component Id="SetFilePermissionsScript" Directory="INSTALLFOLDER" Guid="55555555-5555-5555-5555-555555555505">')
[void]$sb.AppendLine("      <File Id=`"SetFilePermissionsPs1File`" Source=`"$pubEsc\SetFilePermissions.ps1`" KeyPath=`"yes`" />")
[void]$sb.AppendLine('    </Component>')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('    <Component Id="FixPermissionsCmd" Directory="INSTALLFOLDER" Guid="55555555-5555-5555-5555-555555555506">')
[void]$sb.AppendLine("      <File Id=`"FixPermissionsCmdFile`" Source=`"$pubEsc\Fix-Permissions.cmd`" KeyPath=`"yes`" />")
[void]$sb.AppendLine('    </Component>')

# Optional core files
foreach ($file in $presentOptional) {
    $compId = Get-WixId "Core" $file
    $fileId = Get-WixId "CF" $file
    $guid = Get-DeterministicGuid "core:$file"
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine("    <Component Id=`"$compId`" Directory=`"INSTALLFOLDER`" Guid=`"$guid`">")
    [void]$sb.AppendLine("      <File Id=`"$fileId`" Source=`"$pubEsc\$file`" KeyPath=`"yes`" />")
    [void]$sb.AppendLine('    </Component>')
}
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('')

# wwwroot fragment
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <Component Id="IndexHtml" Directory="WWWROOT" Guid="AAAA1111-1111-1111-1111-111111111111">')
[void]$sb.AppendLine("      <File Id=`"IndexHtmlFile`" Source=`"$pubEsc\wwwroot\index.html`" KeyPath=`"yes`" />")
[void]$sb.AppendLine('    </Component>')
foreach ($wf in $wwwrootExtraFiles) {
    $compId = Get-WixId "WwwRoot" $wf.Name
    $fileId = Get-WixId "WR" $wf.Name
    $guid = Get-DeterministicGuid "wwwroot:$($wf.Name)"
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine("    <Component Id=`"$compId`" Directory=`"WWWROOT`" Guid=`"$guid`">")
    [void]$sb.AppendLine("      <File Id=`"$fileId`" Source=`"$pubEsc\wwwroot\$($wf.Name)`" KeyPath=`"yes`" />")
    [void]$sb.AppendLine('    </Component>')
}
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('')

# Assets fragment
[void]$sb.AppendLine('  <Fragment>')
$assetCounter = 0
foreach ($af in $assetFiles) {
    $assetCounter++
    $compId = Get-WixId "AssetFile" $af.Name
    $fileId = Get-WixId "AF" $af.Name
    $guid = Get-DeterministicGuid "asset:$($af.Name)"
    [void]$sb.AppendLine("    <Component Id=`"$compId`" Directory=`"WWWROOT_ASSETS`" Guid=`"$guid`">")
    [void]$sb.AppendLine("      <File Id=`"$fileId`" Source=`"$pubEsc\wwwroot\assets\$($af.Name)`" KeyPath=`"yes`" />")
    [void]$sb.AppendLine('    </Component>')
}
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('')

# Empty directories
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <Component Id="DataDirectory" Directory="DATADIR" Guid="CCCC1111-1111-1111-1111-111111111111">')
[void]$sb.AppendLine('      <CreateFolder />')
[void]$sb.AppendLine('    </Component>')
[void]$sb.AppendLine('    <Component Id="LogsDirectory" Directory="LOGSDIR" Guid="CCCC2222-2222-2222-2222-222222222222">')
[void]$sb.AppendLine('      <CreateFolder />')
[void]$sb.AppendLine('    </Component>')
[void]$sb.AppendLine('    <Component Id="SessionLogsDirectory" Directory="LOGS_SESSIONS" Guid="CCCC3333-3333-3333-3333-333333333333">')
[void]$sb.AppendLine('      <CreateFolder />')
[void]$sb.AppendLine('    </Component>')
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('</Wix>')

$generatedWxs = Join-Path $scriptDir "Product-Generated.wxs"
[System.IO.File]::WriteAllText($generatedWxs, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Host "  [OK] Generated WiX source ($assetCounter asset files, $($presentOptional.Count) optional core files)" -ForegroundColor Green

$totalComponents = 3 + 3 + $presentOptional.Count + 1 + $wwwrootExtraFiles.Count + $assetFiles.Count + 3  # 3 core + 3 setup + optional + index + wwwroot extras + assets + 3 dirs
Write-Host "  Total components: $totalComponents" -ForegroundColor Gray
Write-Host ""

# ===================================================================
# STEP 5: Build MSI
# ===================================================================
Write-Host "STEP 5: Building MSI..." -ForegroundColor Cyan

# Clean old MSI to avoid stale builds (skip if locked)
if (Test-Path (Join-Path $msiOutputDir "HazinaOrchestrationSetup.msi")) {
    Remove-Item (Join-Path $msiOutputDir "HazinaOrchestrationSetup.msi") -Force -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Path $msiOutputDir -Force | Out-Null
$objDir = Join-Path $scriptDir "obj\$Configuration"
New-Item -ItemType Directory -Path $objDir -Force | Out-Null

# Use timestamped filename to avoid lock issues
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$msiFileName = "HazinaOrchestrationSetup-$timestamp.msi"
Write-Host "  Building MSI: $msiFileName" -ForegroundColor Yellow

Push-Location $scriptDir

Write-Host "  Compiling (candle.exe)..." -ForegroundColor Gray
& $candleExe "Product-Generated.wxs" -ext WixUIExtension -ext WixUtilExtension -out "obj\$Configuration\" 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "  [FAILED] candle.exe failed" -ForegroundColor Red
    Pop-Location
    exit 1
}

Write-Host "  Linking (light.exe)..." -ForegroundColor Gray
& $lightExe "obj\$Configuration\Product-Generated.wixobj" -ext WixUIExtension -ext WixUtilExtension -out "$msiOutputDir\$msiFileName" -sval 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "  [FAILED] light.exe failed" -ForegroundColor Red
    Pop-Location
    exit 1
}

Pop-Location

$msiPath = Join-Path $msiOutputDir "HazinaOrchestrationSetup.msi"
if (-not (Test-Path $msiPath)) {
    Write-Host "  [FAILED] MSI not created" -ForegroundColor Red
    exit 1
}

$msiSize = [math]::Round((Get-Item $msiPath).Length / 1MB, 2)
Write-Host "  [OK] MSI created: $msiSize MB" -ForegroundColor Green
Write-Host ""

# ===================================================================
# SUCCESS
# ===================================================================
Write-Host "=================================================================" -ForegroundColor Green
Write-Host "  BUILD COMPLETE" -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  MSI: $msiPath" -ForegroundColor White
Write-Host ""
Write-Host "  What the MSI does:" -ForegroundColor Cyan
Write-Host "    - Kills any running HazinaOrchestration.exe" -ForegroundColor Gray
Write-Host "    - Removes old Windows Service (if exists, migration from v1)" -ForegroundColor Gray
Write-Host "    - Installs to: C:\Program Files\Hazina Orchestration\" -ForegroundColor Gray
Write-Host "    - Creates Start Menu shortcut" -ForegroundColor Gray
Write-Host "    - Includes React SPA + $assetCounter asset files" -ForegroundColor Gray
Write-Host "    - Shows license agreement during install" -ForegroundColor Gray
Write-Host "    - Finish dialog offers to run Setup.ps1 (Tailscale/HTTPS config)" -ForegroundColor Gray
Write-Host ""
Write-Host "  After install:" -ForegroundColor Cyan
Write-Host "    Finish dialog checkbox launches Setup.ps1 for Tailscale/HTTPS setup" -ForegroundColor Gray
Write-Host "    Or launch from Start Menu / run HazinaOrchestration.exe" -ForegroundColor Gray
Write-Host "    System tray icon with context menu (dashboard, swagger, exit)" -ForegroundColor Gray
Write-Host "    Toggle 'Start with Windows' from tray menu" -ForegroundColor Gray
Write-Host "    Web UI:  https://localhost:5123" -ForegroundColor Gray
Write-Host "    Swagger: https://localhost:5123/swagger" -ForegroundColor Gray
Write-Host ""
