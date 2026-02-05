# Iteration 18: Release build script

param(
    [string]$Version = "0.1.0",
    [switch]$Package
)

Write-Host "📦 Building HazinaCoder Release v$Version" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# Clean previous builds
Write-Host "🧹 Cleaning..." -ForegroundColor Yellow
dotnet clean --configuration Release

# Restore
Write-Host "📥 Restoring..." -ForegroundColor Yellow
dotnet restore

# Build
Write-Host "🔨 Building..." -ForegroundColor Yellow
dotnet build --configuration Release --no-restore

# Run tests
Write-Host "🧪 Testing..." -ForegroundColor Yellow
dotnet test --configuration Release --no-build

# Publish
Write-Host "📦 Publishing..." -ForegroundColor Yellow
dotnet publish --configuration Release --output "./publish/hazinacoder-$Version"

if ($Package) {
    Write-Host "📦 Creating NuGet package..." -ForegroundColor Yellow
    dotnet pack --configuration Release --output "./publish/packages"
}

Write-Host ""
Write-Host "✅ Release build complete!" -ForegroundColor Green
Write-Host "Output: ./publish/hazinacoder-$Version" -ForegroundColor Green
