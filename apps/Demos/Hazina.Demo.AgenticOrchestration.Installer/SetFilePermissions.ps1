param(
    [string]$InstallDir
)

# Emergency logging - write BEFORE anything else
$emergencyLog = "C:\Windows\Temp\SetFilePermissions-ENTRY-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
try {
    "Script started at $(Get-Date)" | Out-File $emergencyLog -Encoding UTF8
    "Parameters: InstallDir=$InstallDir" | Out-File $emergencyLog -Append -Encoding UTF8
} catch {
    # If even this fails, we have bigger problems
}

$ErrorActionPreference = "Stop"
# Log to Windows\Temp which SYSTEM can always write to
$logFile = "C:\Windows\Temp\SetFilePermissions-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"

function Write-Log {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] $Message"
    Add-Content -Path $logFile -Value $logMessage -Encoding UTF8
    Write-Host $logMessage
}

try {
    Write-Log "=== SetFilePermissions Script Started ==="
    Write-Log "Install Directory: $InstallDir"
    Write-Log "PowerShell Version: $($PSVersionTable.PSVersion)"
    Write-Log "Running as: $([System.Security.Principal.WindowsIdentity]::GetCurrent().Name)"

    if ([string]::IsNullOrEmpty($InstallDir)) {
        Write-Log "WARNING: InstallDir parameter is empty - skipping file permissions setup"
        exit 0  # Exit gracefully so installer can continue
    }

    # Files that need write access for regular users
    $filesToModify = @(
        "appsettings.json",
        "appsettings.Production.json"
    )

    foreach ($fileName in $filesToModify) {
        $filePath = Join-Path $InstallDir $fileName

        if (-not (Test-Path $filePath)) {
            Write-Log "WARNING: File not found: $filePath - skipping"
            continue
        }

        Write-Log "Processing: $fileName"

        # Get current ACL
        $acl = Get-Acl $filePath
        Write-Log "  Current owner: $($acl.Owner)"

        # Create access rule for BUILTIN\Users group with Modify rights
        # Modify = Read + Write + Delete (everything except change permissions/ownership)
        $usersGroup = New-Object System.Security.Principal.SecurityIdentifier("S-1-5-32-545")
        $fileSystemRights = [System.Security.AccessControl.FileSystemRights]::Modify
        $inheritanceFlags = [System.Security.AccessControl.InheritanceFlags]::None
        $propagationFlags = [System.Security.AccessControl.PropagationFlags]::None
        $accessControlType = [System.Security.AccessControl.AccessControlType]::Allow

        $accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
            $usersGroup,
            $fileSystemRights,
            $inheritanceFlags,
            $propagationFlags,
            $accessControlType
        )

        Write-Log "  Adding Modify permission for BUILTIN\Users"
        $acl.AddAccessRule($accessRule)

        # Apply the modified ACL
        Set-Acl -Path $filePath -AclObject $acl
        Write-Log "  Permissions applied successfully"

        # Verify
        $newAcl = Get-Acl $filePath
        $usersAccess = $newAcl.Access | Where-Object {
            $_.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value -eq "S-1-5-32-545"
        }

        if ($usersAccess) {
            Write-Log "  Verification: Users group has $($usersAccess.FileSystemRights) rights"
        } else {
            Write-Log "  WARNING: Could not verify Users group permissions"
        }
    }

    Write-Log "=== SetFilePermissions Script Completed Successfully ==="
    exit 0
}
catch {
    Write-Log "=== ERROR OCCURRED ==="
    Write-Log "Error Type: $($_.Exception.GetType().FullName)"
    Write-Log "Error Message: $($_.Exception.Message)"
    Write-Log "Stack Trace: $($_.ScriptStackTrace)"
    Write-Log "=== SetFilePermissions Script Had Errors - But Installer Can Continue ==="
    Write-Log "Check log file at: $logFile for details"
    exit 0  # Always exit successfully so installer doesn't hang
}
