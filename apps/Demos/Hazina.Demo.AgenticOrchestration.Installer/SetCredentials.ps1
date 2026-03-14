param(
    [string]$Username,
    [string]$Password,
    [string]$ShellCommand,
    [string]$WorkingDirectory
)

# Derive install directory from script location (avoids MSI path quoting issues)
$InstallDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Log to user temp (perUser install runs as user, not SYSTEM)
$logDir = if ($env:TEMP) { $env:TEMP } else { "C:\Windows\Temp" }

# Emergency logging - write BEFORE anything else
$emergencyLog = Join-Path $logDir "SetCredentials-ENTRY-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
try {
    "Script started at $(Get-Date)" | Out-File $emergencyLog -Encoding UTF8
    "Derived InstallDir: $InstallDir" | Out-File $emergencyLog -Append -Encoding UTF8
    "Parameters: Username=$Username" | Out-File $emergencyLog -Append -Encoding UTF8
} catch {
    # If even this fails, we have bigger problems
}

$ErrorActionPreference = "Stop"
$logFile = Join-Path $logDir "SetCredentials-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"

function Write-Log {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] $Message"
    Add-Content -Path $logFile -Value $logMessage -Encoding UTF8
    Write-Host $logMessage
}

try {
    Write-Log "=== SetCredentials Script Started ==="
    Write-Log "Install Directory: $InstallDir"
    Write-Log "Username: $Username"
    Write-Log "Shell Command: $ShellCommand"
    Write-Log "Working Directory: $WorkingDirectory"
    Write-Log "Log File: $logFile"
    Write-Log "PowerShell Version: $($PSVersionTable.PSVersion)"
    Write-Log "Running as: $([System.Security.Principal.WindowsIdentity]::GetCurrent().Name)"

    $configPath = Join-Path $InstallDir "appsettings.Production.json"
    Write-Log "Config Path: $configPath"

    # Check if config file exists
    if (-not (Test-Path $configPath)) {
        Write-Log "WARNING: Config file not found at: $configPath"
        Write-Log "Listing files in $InstallDir :"
        Get-ChildItem $InstallDir -ErrorAction SilentlyContinue | ForEach-Object {
            Write-Log "  - $($_.Name)"
        }
        Write-Log "Configuration will use defaults - installer can continue"
        exit 0  # Exit gracefully
    }

    Write-Log "Config file found, reading..."
    $jsonContent = Get-Content $configPath -Raw -Encoding UTF8
    Write-Log "JSON content length: $($jsonContent.Length) characters"

    Write-Log "Parsing JSON..."
    $config = $jsonContent | ConvertFrom-Json
    Write-Log "JSON parsed successfully"

    # Verify structure
    if (-not $config.Authentication) {
        Write-Log "WARNING: Authentication section not found - skipping auth config"
        exit 0  # Exit gracefully
    }
    if (-not $config.AgenticOrchestration.Terminal) {
        Write-Log "WARNING: Terminal section not found - skipping terminal config"
        exit 0  # Exit gracefully
    }

    Write-Log "Config structure verified"

    # Update credentials (only if provided)
    if (-not [string]::IsNullOrEmpty($Username)) {
        Write-Log "Updating username to: $Username"
        $config.Authentication.Username = $Username
    }
    if (-not [string]::IsNullOrEmpty($Password)) {
        Write-Log "Updating password (length: $($Password.Length))"
        $config.Authentication.Password = $Password
    }

    # Update terminal settings (only if provided)
    if (-not [string]::IsNullOrEmpty($ShellCommand)) {
        Write-Log "Updating shell command to: $ShellCommand"
        $config.AgenticOrchestration.Terminal.DefaultCommand = $ShellCommand
    }
    if (-not [string]::IsNullOrEmpty($WorkingDirectory)) {
        Write-Log "Updating working directory to: $WorkingDirectory"
        $config.AgenticOrchestration.Terminal.DefaultWorkingDirectory = $WorkingDirectory
    }

    Write-Log "Converting to JSON..."
    $newJson = $config | ConvertTo-Json -Depth 20
    Write-Log "JSON conversion complete, length: $($newJson.Length) characters"

    # Create backup
    $backupPath = "$configPath.backup"
    Write-Log "Creating backup at: $backupPath"
    Copy-Item $configPath $backupPath -Force

    # Write to file
    Write-Log "Writing to config file..."
    [System.IO.File]::WriteAllText($configPath, $newJson, [System.Text.Encoding]::UTF8)
    Write-Log "File written successfully"

    # Verify
    if (Test-Path $configPath) {
        $newSize = (Get-Item $configPath).Length
        Write-Log "Verification: Config file exists, size: $newSize bytes"
    }

    Write-Log "=== SetCredentials Script Completed Successfully ==="
    exit 0
}
catch {
    Write-Log "=== ERROR OCCURRED ==="
    Write-Log "Error Type: $($_.Exception.GetType().FullName)"
    Write-Log "Error Message: $($_.Exception.Message)"
    Write-Log "Stack Trace: $($_.ScriptStackTrace)"
    Write-Log "=== SetCredentials Script Had Errors - But Installer Can Continue ==="
    Write-Log "Check log file at: $logFile for details"
    exit 0  # Always exit successfully so installer doesn't hang
}
