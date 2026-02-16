param(
    [string]$InstallDir,
    [string]$Username,
    [string]$Password,
    [string]$ShellCommand,
    [string]$WorkingDirectory
)

$ErrorActionPreference = "Stop"

try {
    $configPath = Join-Path $InstallDir "appsettings.Production.json"

    Write-Host "Updating configuration in: $configPath"
    Write-Host "Username: $Username"
    Write-Host "Shell Command: $ShellCommand"
    Write-Host "Working Directory: $WorkingDirectory"

    if (-not (Test-Path $configPath)) {
        Write-Host "Config file not found: $configPath"
        exit 1
    }

    # Read the JSON file
    $config = Get-Content $configPath -Raw | ConvertFrom-Json

    # Update credentials
    $config.Authentication.Username = $Username
    $config.Authentication.Password = $Password

    # Update terminal settings
    if ($ShellCommand) {
        $config.AgenticOrchestration.Terminal.DefaultCommand = $ShellCommand
    }
    if ($WorkingDirectory) {
        $config.AgenticOrchestration.Terminal.DefaultWorkingDirectory = $WorkingDirectory
    }

    # Write back to file
    $config | ConvertTo-Json -Depth 10 | Set-Content $configPath -Encoding UTF8

    Write-Host "Configuration updated successfully!"
    exit 0
}
catch {
    Write-Host "Error updating configuration: $_"
    exit 1
}
