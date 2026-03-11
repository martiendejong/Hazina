<#
.SYNOPSIS
    One-shot deployment for Hazina Orchestration on DESKTOP-ECBAUNU (Martien's PC)

.DESCRIPTION
    Thin wrapper around Setup.ps1 with machine-specific configuration.
    Builds MSI from source, installs with Tailscale HTTPS, configures
    machine-specific paths and credentials.

    MUST RUN AS ADMINISTRATOR (for MSI install + cert generation).

.PARAMETER SkipBuild
    Skip MSI build (use existing MSI)

.PARAMETER Force
    Skip confirmation prompt

.EXAMPLE
    .\Deploy-ThisPC.ps1
    .\Deploy-ThisPC.ps1 -SkipBuild   # Use existing MSI
#>

param(
    [switch]$SkipBuild,
    [switch]$Force
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Build args
$buildArgs = @{}
if (-not $SkipBuild) { $buildArgs["IncludeBuild"] = $true }
if ($Force) { $buildArgs["Force"] = $true }

# Call Setup.ps1 with machine-specific config for DESKTOP-ECBAUNU
& "$scriptDir\Setup.ps1" `
    -Mode tailscale `
    -Port 5123 `
    -AuthUsername "bosi" `
    -AuthPassword "Th1s1sSp4rt4!" `
    -TerminalCommand "C:\scripts\claude_agent.bat" `
    -TerminalWorkDir "C:\scripts" `
    -DatabasePath "C:\scripts\_machine\agent-activity.db" `
    -LogsPath "C:\scripts\logs" `
    @buildArgs
