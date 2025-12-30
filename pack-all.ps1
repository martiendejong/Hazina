# Pack all Hazina NuGet packages
$ErrorActionPreference = "Stop"

Write-Host "Packing all Hazina NuGet packages..." -ForegroundColor Cyan

# Output directory for packages
$outputDir = "nupkgs"
if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

# Find all library projects (exclude tests, apps, demos)
$projects = Get-ChildItem -Path "src" -Filter "*.csproj" -Recurse |
    Where-Object {
        $_.FullName -notmatch "Tests" -and
        $_.FullName -notmatch "\\apps\\" -and
        $_.FullName -notmatch "\\Apps\\"
    }

Write-Host "Found $($projects.Count) projects to pack" -ForegroundColor Green
Write-Host ""

$successCount = 0
$failCount = 0

foreach ($project in $projects) {
    Write-Host "Packing: $($project.BaseName)..." -ForegroundColor Yellow

    try {
        $result = dotnet pack $project.FullName --configuration Release --output $outputDir /p:ContinuousIntegrationBuild=true 2>&1

        if ($LASTEXITCODE -eq 0) {
            $successCount++
            Write-Host "  Success" -ForegroundColor Green
        } else {
            $failCount++
            Write-Host "  Failed: $($result | Select-Object -Last 1)" -ForegroundColor Red
        }
    }
    catch {
        $failCount++
        Write-Host "  Error: $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Packing complete!" -ForegroundColor Cyan
Write-Host "Success: $successCount" -ForegroundColor Green
Write-Host "Failed: $failCount" -ForegroundColor Red
Write-Host "Output: $outputDir" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
