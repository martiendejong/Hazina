param([string]$Version = "1.0.6")

$targetVersion = $Version
& "$PSScriptRoot\updateversions.ps1"
