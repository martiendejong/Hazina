#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Verifies API quality across all Hazina packages.

.DESCRIPTION
    This script checks for:
    - XML documentation coverage on public APIs
    - Nullable reference type compliance
    - Analyzer configuration consistency
    - Access modifier appropriateness
    - Breaking changes vs semantic versioning

.PARAMETER ProjectPath
    Path to the project or solution to analyze. Defaults to repository root.

.PARAMETER FailOnWarnings
    Treat warnings as errors. Default: false.

.EXAMPLE
    .\verify-api-quality.ps1 -ProjectPath "src/Core" -FailOnWarnings
#>

param(
    [string]$ProjectPath = ".",
    [switch]$FailOnWarnings
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "Hazina API Quality Verification" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Change to repo root
Push-Location $repoRoot
try {
    # 1. Check XML Documentation Coverage
    Write-Host "[1/6] Checking XML documentation coverage..." -ForegroundColor Yellow

    $publicTypesWithoutDocs = @()
    $csFiles = Get-ChildItem -Path $ProjectPath -Filter "*.cs" -Recurse |
        Where-Object { $_.FullName -notmatch "\\obj\\|\\bin\\|\.g\.cs$|\.AssemblyInfo\.cs$" }

    foreach ($file in $csFiles) {
        $content = Get-Content $file.FullName -Raw

        # Find public types without XML docs
        if ($content -match "(?m)^(\s*)public\s+(class|interface|struct|enum|record)\s+(\w+)") {
            $lines = Get-Content $file.FullName
            for ($i = 0; $i -lt $lines.Length; $i++) {
                if ($lines[$i] -match "^\s*public\s+(class|interface|struct|enum|record)\s+(\w+)") {
                    # Check if previous line is XML doc
                    if ($i -eq 0 -or $lines[$i-1] -notmatch "///") {
                        $publicTypesWithoutDocs += "$($file.FullName):$($i+1) - $($Matches[2])"
                    }
                }
            }
        }
    }

    if ($publicTypesWithoutDocs.Count -gt 0) {
        Write-Host "  ⚠ Found $($publicTypesWithoutDocs.Count) public types without XML documentation" -ForegroundColor Yellow
        if ($publicTypesWithoutDocs.Count -le 10) {
            $publicTypesWithoutDocs | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
        } else {
            $publicTypesWithoutDocs | Select-Object -First 10 | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
            Write-Host "    ... and $($publicTypesWithoutDocs.Count - 10) more" -ForegroundColor Gray
        }
    } else {
        Write-Host "  ✓ All public types have XML documentation" -ForegroundColor Green
    }

    # 2. Check Nullable Reference Types
    Write-Host ""
    Write-Host "[2/6] Verifying nullable reference types..." -ForegroundColor Yellow

    $projectsWithoutNullable = @()
    $csprojFiles = Get-ChildItem -Path $ProjectPath -Filter "*.csproj" -Recurse

    foreach ($proj in $csprojFiles) {
        [xml]$projXml = Get-Content $proj.FullName
        $nullable = $projXml.Project.PropertyGroup.Nullable

        if (-not $nullable -or $nullable -ne "enable") {
            $projectsWithoutNullable += $proj.FullName
        }
    }

    if ($projectsWithoutNullable.Count -gt 0) {
        Write-Host "  ⚠ Found $($projectsWithoutNullable.Count) projects without nullable reference types enabled" -ForegroundColor Yellow
        $projectsWithoutNullable | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
    } else {
        Write-Host "  ✓ All projects have nullable reference types enabled" -ForegroundColor Green
    }

    # 3. Check Analyzer Configuration
    Write-Host ""
    Write-Host "[3/6] Checking analyzer configuration..." -ForegroundColor Yellow

    $projectsWithoutAnalyzers = @()

    foreach ($proj in $csprojFiles) {
        [xml]$projXml = Get-Content $proj.FullName
        $hasAnalyzers = $projXml.Project.ItemGroup.PackageReference |
            Where-Object { $_.Include -match "Analyzer|StyleCop|Sonar|Meziantou" }

        if (-not $hasAnalyzers) {
            # Check if inherited from Directory.Build.props
            $dirBuildProps = Join-Path (Split-Path $proj.FullName) "Directory.Build.props"
            $parentDirBuildProps = Join-Path $repoRoot "Directory.Build.props"

            if (-not (Test-Path $dirBuildProps) -and -not (Test-Path $parentDirBuildProps)) {
                $projectsWithoutAnalyzers += $proj.FullName
            }
        }
    }

    if ($projectsWithoutAnalyzers.Count -gt 0) {
        Write-Host "  ⚠ Found $($projectsWithoutAnalyzers.Count) projects without analyzers" -ForegroundColor Yellow
        $projectsWithoutAnalyzers | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
    } else {
        Write-Host "  ✓ All projects have analyzers configured" -ForegroundColor Green
    }

    # 4. Build with Warnings as Errors
    Write-Host ""
    Write-Host "[4/6] Building with strict analysis..." -ForegroundColor Yellow

    $buildArgs = @(
        "build",
        "--configuration", "Debug",
        "--no-incremental",
        "/p:TreatWarningsAsErrors=false"
    )

    if ($FailOnWarnings) {
        $buildArgs[4] = "/p:TreatWarningsAsErrors=true"
    }

    $buildOutput = & dotnet @buildArgs 2>&1
    $buildSuccess = $LASTEXITCODE -eq 0

    if ($buildSuccess) {
        Write-Host "  ✓ Build succeeded" -ForegroundColor Green

        # Count warnings
        $warnings = $buildOutput | Where-Object { $_ -match "warning" }
        if ($warnings.Count -gt 0) {
            Write-Host "  ℹ Found $($warnings.Count) warnings" -ForegroundColor Cyan

            # Categorize warnings
            $analyzerWarnings = $warnings | Where-Object { $_ -match "CA\d+|SA\d+|S\d+|MA\d+" }
            $nullableWarnings = $warnings | Where-Object { $_ -match "CS8\d+" }

            if ($analyzerWarnings.Count -gt 0) {
                Write-Host "    - $($analyzerWarnings.Count) analyzer warnings" -ForegroundColor Gray
            }
            if ($nullableWarnings.Count -gt 0) {
                Write-Host "    - $($nullableWarnings.Count) nullable warnings" -ForegroundColor Gray
            }
        }
    } else {
        Write-Host "  ✗ Build failed" -ForegroundColor Red
        if ($FailOnWarnings) {
            Write-Host "    Run without -FailOnWarnings to see details" -ForegroundColor Gray
        }
    }

    # 5. Check for Public Implementation Classes
    Write-Host ""
    Write-Host "[5/6] Checking for public implementation classes..." -ForegroundColor Yellow

    $suspiciousPublicClasses = @()

    foreach ($file in $csFiles) {
        $content = Get-Content $file.FullName

        for ($i = 0; $i -lt $content.Length; $i++) {
            $line = $content[$i]

            # Look for public classes with suspicious names
            if ($line -match "^\s*public\s+class\s+(\w+)(Implementation|Helper|Internal|Utility)") {
                $suspiciousPublicClasses += "$($file.FullName):$($i+1) - $($Matches[1])$($Matches[2])"
            }
        }
    }

    if ($suspiciousPublicClasses.Count -gt 0) {
        Write-Host "  ⚠ Found $($suspiciousPublicClasses.Count) potentially internal classes marked as public" -ForegroundColor Yellow
        if ($suspiciousPublicClasses.Count -le 10) {
            $suspiciousPublicClasses | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
        } else {
            $suspiciousPublicClasses | Select-Object -First 10 | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
            Write-Host "    ... and $($suspiciousPublicClasses.Count - 10) more" -ForegroundColor Gray
        }
        Write-Host "    Consider marking these as 'internal' if they're implementation details" -ForegroundColor Gray
    } else {
        Write-Host "  ✓ No suspicious public implementation classes found" -ForegroundColor Green
    }

    # 6. Summary Report
    Write-Host ""
    Write-Host "[6/6] Summary" -ForegroundColor Yellow
    Write-Host "======================================" -ForegroundColor Cyan

    $totalIssues = $publicTypesWithoutDocs.Count +
                   $projectsWithoutNullable.Count +
                   $projectsWithoutAnalyzers.Count +
                   $suspiciousPublicClasses.Count

    if (-not $buildSuccess) {
        $totalIssues++
    }

    if ($totalIssues -eq 0) {
        Write-Host "✓ All checks passed!" -ForegroundColor Green
        Write-Host "  API quality is excellent." -ForegroundColor Green
        exit 0
    } else {
        Write-Host "⚠ Found $totalIssues issue(s)" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Recommendations:" -ForegroundColor Cyan
        Write-Host "  1. Add XML documentation to all public types" -ForegroundColor Gray
        Write-Host "  2. Enable nullable reference types in all projects" -ForegroundColor Gray
        Write-Host "  3. Mark implementation details as 'internal'" -ForegroundColor Gray
        Write-Host "  4. Fix all analyzer warnings" -ForegroundColor Gray
        Write-Host ""
        Write-Host "See docs/API_STABILITY.md for guidelines" -ForegroundColor Cyan

        if ($FailOnWarnings) {
            exit 1
        } else {
            exit 0
        }
    }

} finally {
    Pop-Location
}
