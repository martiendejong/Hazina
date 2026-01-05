# PowerShell script to build and publish Hazina Docker images
# Usage: .\scripts\publish-docker.ps1 -Registry "ghcr.io/yourorg" [-Version "1.0.0"]

param(
    [Parameter(Mandatory=$true)]
    [string]$Registry,

    [Parameter(Mandatory=$false)]
    [string]$Version = "1.0.0",

    [Parameter(Mandatory=$false)]
    [switch]$DryRun = $false,

    [Parameter(Mandatory=$false)]
    [switch]$Latest = $true
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Hazina Docker Image Publisher" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Registry: $Registry" -ForegroundColor Yellow
Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host "Dry Run: $DryRun" -ForegroundColor Yellow
Write-Host ""

# Define applications to build
$applications = @(
    @{
        Name = "hazina-cli"
        DisplayName = "Hazina CLI (Claude Code)"
        ProjectPath = "apps/CLI/Hazina.App.ClaudeCode"
        ProjectName = "Hazina.App.ClaudeCode"
    }
    # Add more applications as needed
    # @{
    #     Name = "hazina-web"
    #     DisplayName = "Hazina Web (HTML Mockup Generator)"
    #     ProjectPath = "apps/Web/Hazina.App.HtmlMockupGenerator"
    #     ProjectName = "Hazina.App.HtmlMockupGenerator"
    # }
)

$successCount = 0
$failCount = 0

Write-Host "Step 1: Checking Docker..." -ForegroundColor Cyan
try {
    docker --version | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Docker not found" }
    Write-Host "✓ Docker is available" -ForegroundColor Green
} catch {
    Write-Host "✗ Docker is not installed or not running" -ForegroundColor Red
    exit 1
}
Write-Host ""

Write-Host "Step 2: Logging in to container registry..." -ForegroundColor Cyan
if (-not $DryRun) {
    Write-Host "Please ensure you're logged in to $Registry" -ForegroundColor Yellow
    Write-Host "For GitHub Container Registry: echo \$GITHUB_TOKEN | docker login ghcr.io -u USERNAME --password-stdin" -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to continue (Ctrl+C to abort)"
}
Write-Host ""

Write-Host "Step 3: Building and pushing Docker images..." -ForegroundColor Cyan
Write-Host ""

foreach ($app in $applications) {
    $imageName = "$Registry/$($app.Name)"
    $tags = @(
        "${imageName}:${Version}",
        "${imageName}:latest"
    )

    Write-Host "Building: $($app.DisplayName)" -ForegroundColor White
    Write-Host "  Image: $imageName" -ForegroundColor Gray
    Write-Host "  Tags: $($tags -join ', ')" -ForegroundColor Gray
    Write-Host ""

    try {
        # Build the image
        $buildArgs = @(
            "build",
            "--build-arg", "PROJECT_PATH=$($app.ProjectPath)",
            "--build-arg", "PROJECT_NAME=$($app.ProjectName)",
            "-t", "${imageName}:${Version}",
            "-f", "Dockerfile",
            "."
        )

        if ($Latest) {
            $buildArgs += "-t", "${imageName}:latest"
        }

        Write-Host "  Building image..." -ForegroundColor Cyan
        & docker @buildArgs

        if ($LASTEXITCODE -ne 0) {
            throw "Docker build failed"
        }

        Write-Host "  ✓ Build successful" -ForegroundColor Green
        Write-Host ""

        # Push the images
        if (-not $DryRun) {
            Write-Host "  Pushing image..." -ForegroundColor Cyan

            foreach ($tag in $tags) {
                Write-Host "    Pushing: $tag" -ForegroundColor Gray
                docker push $tag

                if ($LASTEXITCODE -ne 0) {
                    throw "Docker push failed for $tag"
                }
            }

            Write-Host "  ✓ Push successful" -ForegroundColor Green
            $successCount++
        } else {
            Write-Host "  ⓘ DRY RUN: Image built but not pushed" -ForegroundColor Yellow
            $successCount++
        }

    } catch {
        Write-Host "  ✗ Failed: $_" -ForegroundColor Red
        $failCount++
    }

    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Docker Build Summary:" -ForegroundColor Cyan
Write-Host "  Success: $successCount" -ForegroundColor Green
Write-Host "  Failed: $failCount" -ForegroundColor Red
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($failCount -gt 0) {
    Write-Host "⚠ Some images failed to build/push" -ForegroundColor Yellow
    exit 1
}

if ($DryRun) {
    Write-Host "✓ DRY RUN: All images built successfully (not pushed)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To push images, run without -DryRun flag" -ForegroundColor Yellow
} else {
    Write-Host "✓ All images built and pushed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Pull your images with:" -ForegroundColor Cyan
    foreach ($app in $applications) {
        Write-Host "  docker pull $Registry/$($app.Name):$Version" -ForegroundColor White
    }
}

Write-Host ""
