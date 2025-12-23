# Publish all NuGet packages to NuGet.org
# Usage: .\publish-packages.ps1 <your-api-key>

param(
    [Parameter(Mandatory=$true)]
    [string]$ApiKey
)

$packages = Get-ChildItem -Path ".\nupkgs\*.nupkg"

Write-Host "Found $($packages.Count) packages to publish" -ForegroundColor Green

foreach ($package in $packages) {
    Write-Host "`nPublishing $($package.Name)..." -ForegroundColor Cyan

    dotnet nuget push $package.FullName `
        --api-key $ApiKey `
        --source https://api.nuget.org/v3/index.json `
        --skip-duplicate

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Successfully published $($package.Name)" -ForegroundColor Green
    } else {
        Write-Host "✗ Failed to publish $($package.Name)" -ForegroundColor Red
    }
}

Write-Host "`nDone!" -ForegroundColor Green
