# PowerShell script to pack and publish all Hazina NuGet packages
# Usage: .\scripts\publish-nuget.ps1 -ApiKey "your-nuget-api-key" [-Version "1.0.0"]

param(
    [Parameter(Mandatory=$true)]
    [string]$ApiKey,

    [Parameter(Mandatory=$false)]
    [string]$Version = "1.0.0",

    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",

    [Parameter(Mandatory=$false)]
    [switch]$DryRun = $false
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Hazina NuGet Package Publisher" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Dry Run: $DryRun" -ForegroundColor Yellow
Write-Host ""

# Define packages to publish (order matters for dependencies)
$packages = @(
    # Core LLM packages (foundation)
    "src\Core\LLMs\Hazina.LLMs.Classes",
    "src\Core\LLMs\Hazina.LLMs.Client",
    "src\Core\LLMs\Hazina.LLMs.Helpers",
    "src\Core\LLMs\Hazina.LLMs.Tools",
    "src\Core\LLMs\Hazina.LLMClientTools",

    # LLM Providers
    "src\Core\LLMs.Providers\Hazina.LLMs.OpenAI",
    "src\Core\LLMs.Providers\Hazina.LLMs.Anthropic",
    "src\Core\LLMs.Providers\Hazina.LLMs.Gemini",
    "src\Core\LLMs.Providers\Hazina.LLMs.GoogleADK",
    "src\Core\LLMs.Providers\Hazina.LLMs.Mistral",
    "src\Core\LLMs.Providers\Hazina.LLMs.HuggingFace",
    "src\Core\LLMs.Providers\Hazina.LLMs.Ollama",
    "src\Core\LLMs.Providers\Hazina.LLMs.SemanticKernel",

    # Storage
    "src\Core\Storage\Hazina.Store.EmbeddingStore",
    "src\Core\Storage\Hazina.Store.DocumentStore",

    # AI Core (depends on LLMs)
    "src\Core\AI\Hazina.AI.Providers",
    "src\Core\AI\Hazina.AI.FaultDetection",
    "src\Core\AI\Hazina.AI.Orchestration",
    "src\Core\AI\Hazina.AI.FluentAPI",
    "src\Core\AI\Hazina.Neurochain.Core",
    "src\Core\AI\Hazina.CodeIntelligence",
    "src\Core\AI\Hazina.AI.RAG",
    "src\Core\AI\Hazina.AI.Agents",
    "src\Core\AI\Hazina.AI.Memory",
    "src\Core\AI\Hazina.Evals",

    # Security
    "src\Core\Security\Hazina.Security.Core",
    "src\Core\Security\Hazina.Security.AspNetCore",

    # Observability
    "src\Core\Observability\Hazina.Observability.Core",
    "src\Core\Observability\Hazina.Observability.AspNetCore",
    "src\Core\Observability\Hazina.Observability.LLMLogs",

    # Enterprise
    "src\Core\Enterprise\Hazina.Enterprise.Core",

    # Code Generation
    "src\Core\CodeGeneration\Hazina.CodeGeneration.Core",

    # Agents
    "src\Core\Agents\Hazina.AgentFactory",
    "src\Core\Agents\Hazina.DynamicAPI",
    "src\Core\Agents\Hazina.Generator",
    "src\Hazina.Agents.Coding",
    "src\Hazina.Agents.Tools",

    # UI
    "src\Core\UI\Hazina.ChatShared",

    # Tools Foundation
    "src\Tools\Common\Hazina.Tools.Common.Models",
    "src\Tools\Common\Hazina.Tools.Common.Infrastructure.AspNetCore",
    "src\Tools\Foundation\Hazina.Tools.Models",
    "src\Tools\Foundation\Hazina.Tools.Extensions",
    "src\Tools\Foundation\Hazina.Tools.Core",
    "src\Tools\Foundation\Hazina.Tools.TextExtraction",
    "src\Tools\Foundation\Hazina.Tools.Data",
    "src\Tools\Foundation\Hazina.Tools.AI.Agents",

    # Tools Services
    "src\Tools\Services\Hazina.Tools.Services",
    "src\Tools\Services\Hazina.Tools.Services.Embeddings",
    "src\Tools\Services\Hazina.Tools.Services.Store",
    "src\Tools\Services\Hazina.Tools.Services.FileOps",
    "src\Tools\Services\Hazina.Tools.Services.Web",
    "src\Tools\Services\Hazina.Tools.Services.Chat",
    "src\Tools\Services\Hazina.Tools.Services.Images",
    "src\Tools\Services\Hazina.Tools.Services.Database",
    "src\Tools\Services\Hazina.Tools.Services.Social",
    "src\Tools\Services\Hazina.Tools.Services.ContentRetrieval",
    "src\Tools\Services\Hazina.Tools.Services.DataGathering",
    "src\Tools\Services\Hazina.Tools.Services.Intake",
    "src\Tools\Services\Hazina.Tools.Services.Prompts",
    "src\Tools\Services\Hazina.Tools.Services.BigQuery",
    "src\Tools\Services\Hazina.Tools.Services.WordPress",

    # Production
    "src\Tools\Production\Hazina.Production.Monitoring"
)

$outputDir = "nupkgs"
$successCount = 0
$failCount = 0
$skippedCount = 0

# Create output directory
if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

Write-Host "Step 1: Building solution..." -ForegroundColor Cyan
dotnet build Hazina.sln --configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Build succeeded!" -ForegroundColor Green
Write-Host ""

Write-Host "Step 2: Packing NuGet packages..." -ForegroundColor Cyan
Write-Host ""

foreach ($package in $packages) {
    $projectPath = Join-Path $PSScriptRoot "..\$package"
    $csprojFile = Get-ChildItem -Path $projectPath -Filter "*.csproj" | Select-Object -First 1

    if ($null -eq $csprojFile) {
        Write-Host "⚠ Skipped: $package (no .csproj found)" -ForegroundColor Yellow
        $skippedCount++
        continue
    }

    $packageName = [System.IO.Path]::GetFileNameWithoutExtension($csprojFile.Name)
    Write-Host "Packing: $packageName" -ForegroundColor White

    try {
        dotnet pack "$($csprojFile.FullName)" `
            --configuration $Configuration `
            --no-build `
            --output $outputDir `
            /p:Version=$Version `
            /p:PackageVersion=$Version

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Packed: $packageName $Version" -ForegroundColor Green
            $successCount++
        } else {
            Write-Host "✗ Failed to pack: $packageName" -ForegroundColor Red
            $failCount++
        }
    } catch {
        Write-Host "✗ Error packing $packageName : $_" -ForegroundColor Red
        $failCount++
    }

    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Packing Summary:" -ForegroundColor Cyan
Write-Host "  Success: $successCount" -ForegroundColor Green
Write-Host "  Failed: $failCount" -ForegroundColor Red
Write-Host "  Skipped: $skippedCount" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($failCount -gt 0) {
    Write-Host "Some packages failed to pack. Aborting publish." -ForegroundColor Red
    exit 1
}

if ($DryRun) {
    Write-Host "DRY RUN: Packages packed to $outputDir but not published" -ForegroundColor Yellow
    Write-Host "To publish, run without -DryRun flag" -ForegroundColor Yellow
    exit 0
}

Write-Host "Step 3: Publishing to NuGet.org..." -ForegroundColor Cyan
Write-Host ""

$publishCount = 0
$publishFailCount = 0

$nupkgFiles = Get-ChildItem -Path $outputDir -Filter "*.nupkg" | Where-Object { $_.Name -notlike "*.symbols.nupkg" }

foreach ($nupkg in $nupkgFiles) {
    Write-Host "Publishing: $($nupkg.Name)" -ForegroundColor White

    try {
        dotnet nuget push "$($nupkg.FullName)" `
            --api-key $ApiKey `
            --source https://api.nuget.org/v3/index.json `
            --skip-duplicate

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Published: $($nupkg.Name)" -ForegroundColor Green
            $publishCount++
        } else {
            Write-Host "✗ Failed to publish: $($nupkg.Name)" -ForegroundColor Red
            $publishFailCount++
        }
    } catch {
        Write-Host "✗ Error publishing $($nupkg.Name): $_" -ForegroundColor Red
        $publishFailCount++
    }

    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Publishing Summary:" -ForegroundColor Cyan
Write-Host "  Published: $publishCount" -ForegroundColor Green
Write-Host "  Failed: $publishFailCount" -ForegroundColor Red
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($publishFailCount -gt 0) {
    Write-Host "⚠ Some packages failed to publish" -ForegroundColor Yellow
    exit 1
}

Write-Host "✓ All packages published successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "View your packages at: https://www.nuget.org/profiles/HazinaTeam" -ForegroundColor Cyan
