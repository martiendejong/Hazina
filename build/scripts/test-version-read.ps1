# Simple test to verify version reading
$content = Get-Content "LLMs\Classes\DevGPT.LLMs.Classes.csproj" -Raw
if ($content -match '<Version>(\d+\.\d+\.\d+)</Version>') {
    $version = $matches[1]
    Write-Host "Found version: $version"

    $parts = $version.Split('.')
    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = [int]$parts[2]

    Write-Host "Parsed - Major: $major, Minor: $minor, Patch: $patch"
    Write-Host ""
    Write-Host "Increment tests:"
    Write-Host "  Patch: $version => $major.$minor.$($patch + 1)"
    Write-Host "  Minor: $version => $major.$($minor + 1).0"
    Write-Host "  Major: $version => $($major + 1).0.0"
} else {
    Write-Host "ERROR: Could not find version"
}
