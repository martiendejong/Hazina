# PowerShell script to increment and publish NuGet packages
param(
    [ValidateSet("patch", "minor", "major")]
    [string]$IncrementType = "patch",

    [switch]$NoPublish
)

Write-Host ""
Write-Host "========================================"
Write-Host "DevGPT NuGet Version Manager"
Write-Host "========================================"
Write-Host "Increment type: $IncrementType"
Write-Host "Publish after increment: $(!$NoPublish)"
Write-Host ""

# Get current version from first csproj file
$csprojPath = "LLMs\Classes\DevGPT.LLMs.Classes.csproj"
$content = Get-Content $csprojPath -Raw

if ($content -match '<Version>(\d+)\.(\d+)\.(\d+)</Version>') {
    $major = [int]$matches[1]
    $minor = [int]$matches[2]
    $patch = [int]$matches[3]
    $currentVersion = "$major.$minor.$patch"
} else {
    Write-Host "ERROR: Could not find valid version in $csprojPath" -ForegroundColor Red
    Write-Host "Expected format: <Version>1.0.0</Version>"
    exit 1
}

Write-Host "Current version: $currentVersion"

# Increment based on type
switch ($IncrementType) {
    "patch" {
        $patch++
    }
    "minor" {
        $minor++
        $patch = 0
    }
    "major" {
        $major++
        $minor = 0
        $patch = 0
    }
}

$newVersion = "$major.$minor.$patch"
Write-Host "New version: $newVersion"
Write-Host ""

# Confirm with user
$confirm = Read-Host "This will update all NuGet packages to version $newVersion. Continue? (y/n)"
if ($confirm -ne "y") {
    Write-Host "Cancelled by user."
    exit 0
}

Write-Host ""
Write-Host "Updating version numbers..."

# Update all csproj files
$targetVersion = $newVersion
& "$PSScriptRoot\updateversions.ps1"

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to update versions" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Building packages in Release mode..."
dotnet clean
dotnet build -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Creating NuGet packages..."
dotnet pack -c Release --no-build

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Pack failed" -ForegroundColor Red
    exit 1
}

if ($NoPublish) {
    Write-Host ""
    Write-Host "========================================"
    Write-Host "Version increment complete!"
    Write-Host "Packages created but not published."
    Write-Host "To publish manually, run: scripts\nuget-publish.bat"
    Write-Host "========================================"
    exit 0
}

Write-Host ""
Write-Host "Publishing to NuGet..."
& "$PSScriptRoot\nuget-publish.bat"

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Publish failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================"
Write-Host "SUCCESS! All packages published."
Write-Host "Version: $currentVersion => $newVersion"
Write-Host "========================================"
Write-Host ""
Write-Host "Don't forget to:"
Write-Host "1. Commit the version changes: git add . && git commit -m 'Bump version to $newVersion'"
Write-Host "2. Tag the release: git tag v$newVersion"
Write-Host "3. Push: git push && git push --tags"
Write-Host ""
