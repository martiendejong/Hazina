#!/usr/bin/env pwsh
# Publish NuGet packages for all projects
# Usage: ./publish-nuget.ps1 [patch|minor|major] [-NoPublish]
# Requires: NUGET_API_KEY environment variable

param(
    [ValidateSet("patch", "minor", "major")]
    [string]$IncrementType = "patch",

    [switch]$NoPublish,

    [string]$OutputDir = "./nupkgs"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "DevGPT Tools - NuGet Package Publisher" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Version increment logic
function Get-CurrentVersion {
    # Find first .csproj with a Version tag
    $csprojFiles = Get-ChildItem -Recurse -Filter "*.csproj" | Where-Object {
        $_.FullName -notmatch "\\obj\\" -and $_.FullName -notmatch "\\bin\\" -and $_.FullName -notmatch "\\Tests\\"
    }

    foreach ($proj in $csprojFiles) {
        try {
            [xml]$xml = Get-Content $proj.FullName
            $versionNode = $xml.SelectSingleNode("//Version")
            if ($versionNode -and $versionNode.InnerText -match '^\d+\.\d+\.\d+$') {
                return $versionNode.InnerText
            }
        } catch {
            # Continue to next file
        }
    }

    # Default version if none found
    return "1.0.0"
}

function Update-AllVersions {
    param([string]$NewVersion)

    Write-Host "Updating all .csproj files to version $NewVersion..." -ForegroundColor Yellow

    $csprojFiles = Get-ChildItem -Recurse -Filter "*.csproj" | Where-Object {
        $_.FullName -notmatch "\\obj\\" -and $_.FullName -notmatch "\\bin\\"
    }

    $updated = 0
    foreach ($proj in $csprojFiles) {
        try {
            [xml]$xml = Get-Content $proj.FullName
            $changed = $false

            # Update existing Version nodes
            foreach ($node in $xml.SelectNodes("//Version")) {
                if ($node.InnerText -ne $NewVersion) {
                    $node.InnerText = $NewVersion
                    $changed = $true
                }
            }

            # Add Version if missing
            $versionNodes = $xml.SelectNodes("//Version")
            if ($versionNodes.Count -eq 0) {
                $propGroup = $xml.SelectSingleNode("//PropertyGroup")
                if (-not $propGroup) {
                    $propGroup = $xml.CreateElement("PropertyGroup")
                    $xml.Project.AppendChild($propGroup) | Out-Null
                }
                $ver = $xml.CreateElement("Version")
                $ver.InnerText = $NewVersion
                $propGroup.AppendChild($ver) | Out-Null
                $changed = $true
            }

            if ($changed) {
                $xml.Save($proj.FullName)
                Write-Host "  Updated: $($proj.Name)" -ForegroundColor Gray
                $updated++
            }
        } catch {
            Write-Host "  Failed to update $($proj.Name): $_" -ForegroundColor Red
        }
    }

    Write-Host "Updated $updated project files" -ForegroundColor Green
    Write-Host ""
}

# Get current version
$currentVersion = Get-CurrentVersion
Write-Host "Current version: $currentVersion" -ForegroundColor Cyan

# Parse version
if ($currentVersion -match '^(\d+)\.(\d+)\.(\d+)$') {
    $major = [int]$matches[1]
    $minor = [int]$matches[2]
    $patch = [int]$matches[3]

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
    Write-Host "New version: $newVersion (increment: $IncrementType)" -ForegroundColor Green
    Write-Host ""

    # Update all versions
    Update-AllVersions -NewVersion $newVersion
} else {
    Write-Host "ERROR: Invalid version format '$currentVersion'" -ForegroundColor Red
    exit 1
}

# Get API key from environment
$ApiKey = $env:NUGET_API_KEY
$Source = "https://api.nuget.org/v3/index.json"

if ([string]::IsNullOrEmpty($ApiKey)) {
    Write-Host "WARNING: NUGET_API_KEY environment variable not set" -ForegroundColor Yellow
    Write-Host "Packages will be built but NOT published to NuGet.org" -ForegroundColor Yellow
    Write-Host "To set the API key, run: setx NUGET_API_KEY 'your-api-key'" -ForegroundColor Yellow
    Write-Host ""
    $PushToNuGet = $false
} else {
    Write-Host "NuGet API Key found - packages will be published to nuget.org" -ForegroundColor Green
    Write-Host ""
    $PushToNuGet = $true
}

# Override if -NoPublish flag is set
if ($NoPublish) {
    $PushToNuGet = $false
    Write-Host "NoPublish flag set - skipping publish step" -ForegroundColor Yellow
    Write-Host ""
}

# Get all project files
$projects = Get-ChildItem -Recurse -Filter "*.csproj" | Where-Object {
    $_.FullName -notmatch "\\obj\\" -and $_.FullName -notmatch "\\bin\\"
}

Write-Host "Found $($projects.Count) projects to package" -ForegroundColor Green
Write-Host ""

# Create output directory if it doesn't exist
if (!(Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
    Write-Host "Created output directory: $OutputDir" -ForegroundColor Yellow
}

# Clean previous packages
Write-Host "Cleaning previous packages..." -ForegroundColor Yellow
Remove-Item "$OutputDir/*.nupkg" -ErrorAction SilentlyContinue
Write-Host ""

# Build and pack each project
$successCount = 0
$failureCount = 0
$failedProjects = @()

foreach ($project in $projects) {
    Write-Host "Processing: $($project.Name)" -ForegroundColor Cyan
    Write-Host "  Path: $($project.Directory)" -ForegroundColor Gray

    try {
        # Restore dependencies
        Write-Host "  Restoring dependencies..." -ForegroundColor Gray
        dotnet restore $project.FullName --verbosity quiet

        # Build in Release mode
        Write-Host "  Building (Release)..." -ForegroundColor Gray
        dotnet build $project.FullName -c Release --no-restore --verbosity quiet

        # Pack the project
        Write-Host "  Creating NuGet package..." -ForegroundColor Gray
        dotnet pack $project.FullName -c Release --no-build -o $OutputDir --verbosity quiet

        Write-Host "  SUCCESS" -ForegroundColor Green
        $successCount++
    }
    catch {
        Write-Host "  FAILED: $($_.Exception.Message)" -ForegroundColor Red
        $failureCount++
        $failedProjects += $project.Name
    }
    Write-Host ""
}

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Build Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Successful: $successCount" -ForegroundColor Green
Write-Host "Failed: $failureCount" -ForegroundColor $(if ($failureCount -gt 0) { "Red" } else { "Green" })

if ($failureCount -gt 0) {
    Write-Host ""
    Write-Host "Failed projects:" -ForegroundColor Red
    foreach ($failed in $failedProjects) {
        Write-Host "  - $failed" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "Skipping NuGet publish due to build failures" -ForegroundColor Red
    exit 1
}

# List created packages
Write-Host ""
Write-Host "Created packages:" -ForegroundColor Cyan
$packages = Get-ChildItem "$OutputDir/*.nupkg"
foreach ($package in $packages) {
    Write-Host "  - $($package.Name)" -ForegroundColor Gray
}

# Push to NuGet automatically if API key is set
if ($PushToNuGet) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Publishing packages to NuGet.org" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    $pushSuccessCount = 0
    $pushFailureCount = 0

    foreach ($package in $packages) {
        Write-Host "Pushing: $($package.Name)..." -ForegroundColor Cyan
        try {
            dotnet nuget push $package.FullName --source $Source --api-key $ApiKey --skip-duplicate
            Write-Host "  SUCCESS" -ForegroundColor Green
            $pushSuccessCount++
        }
        catch {
            Write-Host "  FAILED: $($_.Exception.Message)" -ForegroundColor Red
            $pushFailureCount++
        }
    }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Publish Summary" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Successfully published: $pushSuccessCount" -ForegroundColor Green
    Write-Host "Failed to publish: $pushFailureCount" -ForegroundColor $(if ($pushFailureCount -gt 0) { "Red" } else { "Green" })
} else {
    Write-Host ""
    Write-Host "Packages built successfully but NOT published (NUGET_API_KEY not set)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Version Update Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Previous version: $currentVersion" -ForegroundColor Gray
Write-Host "New version: $newVersion" -ForegroundColor Green
Write-Host ""
Write-Host "Don't forget to commit and tag:" -ForegroundColor Yellow
Write-Host "  git add ." -ForegroundColor Gray
Write-Host "  git commit -m `"Bump version to $newVersion`"" -ForegroundColor Gray
Write-Host "  git tag v$newVersion" -ForegroundColor Gray
Write-Host "  git push && git push --tags" -ForegroundColor Gray
Write-Host ""
Write-Host "Done!" -ForegroundColor Green
