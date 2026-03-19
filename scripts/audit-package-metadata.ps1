# PowerShell script to audit and validate NuGet package metadata
# Usage: .\scripts\audit-package-metadata.ps1 [-Fix]

param(
    [Parameter(Mandatory=$false)]
    [switch]$Fix = $false
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Hazina Package Metadata Auditor" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Mode: $(if ($Fix) { 'FIX (will update files)' } else { 'AUDIT ONLY (read-only)' })" -ForegroundColor $(if ($Fix) { 'Yellow' } else { 'Green' })
Write-Host ""

# Required metadata fields
$requiredFields = @(
    "PackageId",
    "Version",
    "Description",
    "Authors",
    "PackageLicenseExpression",
    "RepositoryUrl"
)

# Recommended metadata fields
$recommendedFields = @(
    "PackageTags",
    "PackageProjectUrl",
    "GenerateDocumentationFile"
)

# Find all .csproj files
$projects = Get-ChildItem -Path "src" -Filter "*.csproj" -Recurse

$totalCount = 0
$passCount = 0
$warnCount = 0
$failCount = 0
$issues = @()

Write-Host "Auditing $($projects.Count) projects..." -ForegroundColor Cyan
Write-Host ""

foreach ($project in $projects) {
    $totalCount++
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project.Name)
    $projectDir = $project.DirectoryName
    $projectPath = $project.FullName

    # Skip test projects
    if ($projectName -like "*Tests*" -or $projectName -like "*Test") {
        continue
    }

    Write-Host "Checking: $projectName" -ForegroundColor White

    # Load .csproj as XML
    [xml]$csproj = Get-Content $projectPath
    $propertyGroup = $csproj.Project.PropertyGroup

    $projectIssues = @()
    $hasError = $false
    $hasWarning = $false

    # Check required fields
    foreach ($field in $requiredFields) {
        $value = $propertyGroup.$field
        if (!$value) {
            $projectIssues += "  ✗ MISSING REQUIRED: <$field>"
            $hasError = $true
        }
    }

    # Check recommended fields
    foreach ($field in $recommendedFields) {
        $value = $propertyGroup.$field
        if (!$value) {
            $projectIssues += "  ⚠ MISSING RECOMMENDED: <$field>"
            $hasWarning = $true
        }
    }

    # Check README.md exists
    $readmePath = Join-Path $projectDir "README.md"
    if (!(Test-Path $readmePath)) {
        $projectIssues += "  ⚠ MISSING: README.md in project directory"
        $hasWarning = $true
    }

    # Check version format (should be semver)
    $version = $propertyGroup.Version
    if ($version -and $version -notmatch '^\d+\.\d+\.\d+') {
        $projectIssues += "  ⚠ INVALID VERSION FORMAT: $version (should be X.Y.Z)"
        $hasWarning = $true
    }

    # Check description length (should be descriptive)
    $description = $propertyGroup.Description
    if ($description -and $description.Length -lt 50) {
        $projectIssues += "  ⚠ SHORT DESCRIPTION: $($description.Length) chars (recommend 100+)"
        $hasWarning = $true
    }

    # Check tags exist and are relevant
    $tags = $propertyGroup.PackageTags
    if ($tags) {
        $tagCount = ($tags -split ';').Count
        if ($tagCount -lt 3) {
            $projectIssues += "  ⚠ FEW TAGS: $tagCount tags (recommend 5-10)"
            $hasWarning = $true
        }
    }

    # Report results
    if ($projectIssues.Count -eq 0) {
        Write-Host "  ✓ PASS" -ForegroundColor Green
        $passCount++
    } elseif ($hasError) {
        Write-Host "  ✗ FAIL" -ForegroundColor Red
        $failCount++
        $issues += @{
            Project = $projectName
            Path = $projectPath
            Issues = $projectIssues
        }
        foreach ($issue in $projectIssues) {
            Write-Host $issue -ForegroundColor Red
        }
    } else {
        Write-Host "  ⚠ WARNINGS" -ForegroundColor Yellow
        $warnCount++
        $issues += @{
            Project = $projectName
            Path = $projectPath
            Issues = $projectIssues
        }
        foreach ($issue in $projectIssues) {
            Write-Host $issue -ForegroundColor Yellow
        }
    }

    Write-Host ""
}

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Audit Summary:" -ForegroundColor Cyan
Write-Host "  Total: $totalCount" -ForegroundColor White
Write-Host "  Pass: $passCount" -ForegroundColor Green
Write-Host "  Warnings: $warnCount" -ForegroundColor Yellow
Write-Host "  Failures: $failCount" -ForegroundColor Red
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Detailed report
if ($issues.Count -gt 0) {
    Write-Host "Detailed Issues:" -ForegroundColor Cyan
    Write-Host ""

    foreach ($issue in $issues) {
        Write-Host "$($issue.Project):" -ForegroundColor White
        Write-Host "  Path: $($issue.Path)" -ForegroundColor Gray
        foreach ($detail in $issue.Issues) {
            Write-Host $detail -ForegroundColor $(if ($detail -like "*FAIL*") { "Red" } else { "Yellow" })
        }
        Write-Host ""
    }
}

# Recommendations
if ($failCount -gt 0 -or $warnCount -gt 0) {
    Write-Host "Recommendations:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1. Add missing required fields to .csproj files:" -ForegroundColor White
    Write-Host "   <PackageId>Hazina.ProjectName</PackageId>" -ForegroundColor Gray
    Write-Host "   <Version>1.0.0</Version>" -ForegroundColor Gray
    Write-Host "   <Description>Clear description of what this package does</Description>" -ForegroundColor Gray
    Write-Host "   <Authors>Hazina Team</Authors>" -ForegroundColor Gray
    Write-Host "   <PackageLicenseExpression>MIT</PackageLicenseExpression>" -ForegroundColor Gray
    Write-Host "   <RepositoryUrl>https://github.com/martiendejong/Hazina.git</RepositoryUrl>" -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. Add README.md to each project directory" -ForegroundColor White
    Write-Host ""
    Write-Host "3. Add relevant PackageTags (5-10 tags recommended)" -ForegroundColor White
    Write-Host ""
    Write-Host "4. Enable XML documentation:" -ForegroundColor White
    Write-Host "   <GenerateDocumentationFile>true</GenerateDocumentationFile>" -ForegroundColor Gray
    Write-Host ""

    if ($Fix) {
        Write-Host "⚠ FIX mode not yet implemented - manual fixes required" -ForegroundColor Yellow
        Write-Host ""
    } else {
        Write-Host "To auto-fix some issues, run: .\scripts\audit-package-metadata.ps1 -Fix" -ForegroundColor Cyan
        Write-Host "(Note: -Fix mode not yet implemented)" -ForegroundColor Gray
        Write-Host ""
    }
}

# Exit code
if ($failCount -gt 0) {
    exit 1
} elseif ($warnCount -gt 0) {
    exit 0  # Warnings are OK
} else {
    Write-Host "✓ All packages have complete metadata!" -ForegroundColor Green
    exit 0
}
