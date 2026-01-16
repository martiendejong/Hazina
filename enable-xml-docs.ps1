# Enable XML Documentation Generation for Hazina Core Libraries
# This script adds <GenerateDocumentationFile>true</GenerateDocumentationFile> to all Core project files

param(
    [switch]$WhatIf = $false
)

Write-Host "Enabling XML documentation generation for Hazina Core libraries..." -ForegroundColor Cyan

# Find all Core .csproj files
$csprojFiles = Get-ChildItem -Path "src/Core" -Filter "*.csproj" -Recurse

$updatedCount = 0
$alreadyEnabledCount = 0

foreach ($csproj in $csprojFiles) {
    Write-Host "`nProcessing: $($csproj.FullName)" -ForegroundColor Yellow

    [xml]$xml = Get-Content $csproj.FullName

    # Check if GenerateDocumentationFile already exists
    $propertyGroup = $xml.Project.PropertyGroup | Where-Object { $_.GenerateDocumentationFile -ne $null }

    if ($propertyGroup) {
        Write-Host "  [OK] XML documentation already enabled" -ForegroundColor Green
        $alreadyEnabledCount++
        continue
    }

    # Find the first PropertyGroup (usually contains TargetFramework)
    $firstPropertyGroup = $xml.Project.PropertyGroup | Select-Object -First 1

    if ($firstPropertyGroup) {
        # Create GenerateDocumentationFile element
        $generateDocElement = $xml.CreateElement("GenerateDocumentationFile")
        $generateDocElement.InnerText = "true"

        # Add NoWarn for common documentation warnings
        $noWarnElement = $xml.CreateElement("NoWarn")
        $existingNoWarn = $firstPropertyGroup.NoWarn
        if ($existingNoWarn) {
            $noWarnElement.InnerText = "$existingNoWarn;CS1591"
        } else {
            $noWarnElement.InnerText = "CS1591"
        }

        if (!$WhatIf) {
            $firstPropertyGroup.AppendChild($generateDocElement) | Out-Null

            # Update NoWarn if it exists, otherwise create it
            if ($firstPropertyGroup.NoWarn) {
                $firstPropertyGroup.NoWarn = $noWarnElement.InnerText
            } else {
                $firstPropertyGroup.AppendChild($noWarnElement) | Out-Null
            }

            $xml.Save($csproj.FullName)
            Write-Host "  [OK] XML documentation enabled" -ForegroundColor Green
            $updatedCount++
        } else {
            Write-Host "  [WHATIF] Would enable XML documentation" -ForegroundColor Cyan
        }
    } else {
        Write-Host "  [ERROR] Could not find PropertyGroup" -ForegroundColor Red
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Updated: $updatedCount" -ForegroundColor Green
Write-Host "  Already enabled: $alreadyEnabledCount" -ForegroundColor Yellow
Write-Host "  Total processed: $($csprojFiles.Count)" -ForegroundColor White

if ($WhatIf) {
    Write-Host "`nThis was a dry run. Run without -WhatIf to apply changes." -ForegroundColor Cyan
}
