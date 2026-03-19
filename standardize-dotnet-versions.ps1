# Standardize .NET Versions Across Hazina Framework
# Phase 4: Multi-target core libraries, upgrade apps to net10.0

$ErrorActionPreference = "Stop"

# Define target framework rules
$MULTITARGET_FRAMEWORKS = "net8.0;net9.0;net10.0"
$APP_FRAMEWORK = "net10.0"
$APP_FRAMEWORK_WINDOWS = "net10.0-windows"

# Counters
$stats = @{
    core_libs = 0
    tool_libs = 0
    apps = 0
    tests = 0
    skipped = 0
    errors = 0
}

function Update-CsprojFile {
    param([string]$FilePath)

    try {
        $content = Get-Content $FilePath -Raw
        $originalContent = $content

        # Determine project type
        $relPath = $FilePath.Replace("$PWD\", "").Replace("\", "/")
        $isCoreLib = $relPath.StartsWith("src/Core/")
        $isToolLib = $relPath.StartsWith("src/Tools/")
        $isApp = $relPath.StartsWith("apps/")
        $isTest = $FilePath -match "Test"
        $isSrcLib = $isCoreLib -or $isToolLib

        # Skip root-level projects
        if ($relPath -match "^Hazina\.[^/\\]+\.csproj$") {
            $script:stats.skipped++
            return $false
        }

        $changed = $false

        if ($isSrcLib -and -not $isTest) {
            # Multi-target core/tool libraries
            if ($content -match '<TargetFrameworks>(.*?)</TargetFrameworks>') {
                $oldValue = $Matches[1]
                if ($oldValue -ne $MULTITARGET_FRAMEWORKS) {
                    $content = $content -replace '<TargetFrameworks>.*?</TargetFrameworks>', "<TargetFrameworks>$MULTITARGET_FRAMEWORKS</TargetFrameworks>"
                    Write-Host "UPDATE MULTI: $relPath"
                    Write-Host "  $oldValue -> $MULTITARGET_FRAMEWORKS"
                    $changed = $true
                }
            } elseif ($content -match '<TargetFramework>(.*?)</TargetFramework>') {
                $oldValue = $Matches[1]
                # Convert single to multi
                $content = $content -replace '<TargetFramework>(.*?)</TargetFramework>', "<TargetFrameworks>$MULTITARGET_FRAMEWORKS</TargetFrameworks>"
                Write-Host "MULTI-TARGET: $relPath"
                Write-Host "  $oldValue -> $MULTITARGET_FRAMEWORKS"
                $changed = $true
            }

            if ($changed) {
                if ($isCoreLib) { $script:stats.core_libs++ }
                elseif ($isToolLib) { $script:stats.tool_libs++ }
            }
        } elseif ($isApp) {
            # Single-target apps to net10.0
            if ($content -match '<TargetFramework>(.*?)</TargetFramework>') {
                $oldValue = $Matches[1]
                $newValue = if ($oldValue -match "windows") { $APP_FRAMEWORK_WINDOWS } else { $APP_FRAMEWORK }

                if ($oldValue -ne $newValue) {
                    $content = $content -replace '<TargetFramework>.*?</TargetFramework>', "<TargetFramework>$newValue</TargetFramework>"
                    Write-Host "APP: $relPath"
                    Write-Host "  $oldValue -> $newValue"
                    $changed = $true
                    $script:stats.apps++
                }
            }
        } elseif ($isTest) {
            # Tests use net10.0
            if ($content -match '<TargetFramework>(.*?)</TargetFramework>') {
                $oldValue = $Matches[1]
                $newValue = if ($oldValue -match "windows") { "net10.0-windows" } else { "net10.0" }

                if ($oldValue -ne $newValue) {
                    $content = $content -replace '<TargetFramework>.*?</TargetFramework>', "<TargetFramework>$newValue</TargetFramework>"
                    Write-Host "TEST: $relPath"
                    Write-Host "  $oldValue -> $newValue"
                    $changed = $true
                    $script:stats.tests++
                }
            }
        }

        # Write if changed
        if ($changed -and $content -ne $originalContent) {
            Set-Content -Path $FilePath -Value $content -NoNewline
            return $true
        } else {
            if (-not $changed) {
                $script:stats.skipped++
            }
            return $false
        }
    } catch {
        Write-Host "ERROR: $FilePath : $_"
        $script:stats.errors++
        return $false
    }
}

# Find all .csproj files
Write-Host ("="*80)
Write-Host "HAZINA .NET VERSION STANDARDIZATION"
Write-Host ("="*80)
Write-Host ""

$csprojFiles = Get-ChildItem -Path . -Filter "*.csproj" -Recurse -File |
    Where-Object { $_.FullName -notmatch "\\(bin|obj|node_modules|artifacts|\.git)\\" }

foreach ($file in $csprojFiles) {
    Update-CsprojFile -FilePath $file.FullName
}

Write-Host ""
Write-Host ("="*80)
Write-Host "SUMMARY"
Write-Host ("="*80)
Write-Host "Core libraries multi-targeted: $($stats.core_libs)"
Write-Host "Tool libraries multi-targeted: $($stats.tool_libs)"
Write-Host "Apps upgraded to net10.0: $($stats.apps)"
Write-Host "Tests upgraded to net10.0: $($stats.tests)"
Write-Host "Skipped: $($stats.skipped)"
Write-Host "Errors: $($stats.errors)"
Write-Host ""
