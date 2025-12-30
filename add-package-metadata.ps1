# Add NuGet package metadata to all library projects
$ErrorActionPreference = "Stop"

# Package descriptions based on project paths/names
$descriptions = @{
    # Core AI
    "Hazina.AI.Agents" = "Autonomous AI agents with tool calling, workflows, and multi-agent coordination"
    "Hazina.AI.FaultDetection" = "AI-powered fault detection and validation"
    "Hazina.AI.FluentAPI" = "Fluent API for AI operations"
    "Hazina.AI.Orchestration" = "AI task orchestration and management"
    "Hazina.AI.Providers" = "Multi-provider AI orchestration (OpenAI, Anthropic, Google, etc.)"
    "Hazina.AI.RAG" = "Retrieval-Augmented Generation (RAG) with vector search and embeddings"
    "Hazina.CodeIntelligence" = "AI-powered code analysis, refactoring, and pattern learning"
    "Hazina.Neurochain.Core" = "Multi-layer AI reasoning with self-improving failure analysis"

    # Agents
    "Hazina.AgentFactory" = "Factory for creating and managing AI agents"
    "Hazina.DynamicAPI" = "Dynamic API generation and management"
    "Hazina.Generator" = "Code generation utilities"

    # LLM Providers
    "Hazina.LLMs.Anthropic" = "Claude (Anthropic) LLM provider"
    "Hazina.LLMs.Gemini" = "Google Gemini LLM provider"
    "Hazina.LLMs.GoogleADK" = "Google AI Development Kit integration"
    "Hazina.LLMs.HuggingFace" = "HuggingFace LLM provider"
    "Hazina.LLMs.Mistral" = "Mistral AI LLM provider"
    "Hazina.LLMs.OpenAI" = "OpenAI (GPT) LLM provider"
    "Hazina.LLMs.SemanticKernel" = "Microsoft Semantic Kernel integration"

    # LLM Core
    "Hazina.LLMClientTools" = "Tools and utilities for LLM clients"
    "Hazina.LLMs.Classes" = "Core LLM classes and models"
    "Hazina.LLMs.Client" = "Unified LLM client interface"
    "Hazina.LLMs.Helpers" = "Helper utilities for LLM operations"
    "Hazina.LLMs.Tools" = "LLM tool calling and function execution"

    # Storage
    "Hazina.Store.DocumentStore" = "Document storage with chunking and metadata"
    "Hazina.Store.EmbeddingStore" = "Vector embedding storage with similarity search"

    # Production
    "Hazina.Production.Monitoring" = "Production monitoring with metrics, profiling, and diagnostics"

    # Tools Foundation
    "Hazina.Tools.AI.Agents" = "AI agent tools and utilities"
    "Hazina.Tools.Core" = "Core tools and configuration"
    "Hazina.Tools.Data" = "Data access and storage tools"
    "Hazina.Tools.Extensions" = "Extension methods and utilities"
    "Hazina.Tools.Models" = "Data models and DTOs"
    "Hazina.Tools.TextExtraction" = "Text extraction from various document formats"

    # Tools Common
    "Hazina.Tools.Common.Infrastructure.AspNetCore" = "ASP.NET Core infrastructure"
    "Hazina.Tools.Common.Models" = "Common models and data structures"

    # Tools Services
    "Hazina.Tools.Services" = "Core service implementations"
    "Hazina.Tools.Services.BigQuery" = "Google BigQuery integration"
    "Hazina.Tools.Services.Chat" = "Chat service implementations"
    "Hazina.Tools.Services.ContentRetrieval" = "Content retrieval and fetching"
    "Hazina.Tools.Services.DataGathering" = "Data gathering and aggregation"
    "Hazina.Tools.Services.Embeddings" = "Embedding generation services"
    "Hazina.Tools.Services.FileOps" = "File operations and management"
    "Hazina.Tools.Services.Intake" = "Data intake and ingestion"
    "Hazina.Tools.Services.Prompts" = "Prompt management and templates"
    "Hazina.Tools.Services.Social" = "Social media integration"
    "Hazina.Tools.Services.Store" = "Store service implementations"
    "Hazina.Tools.Services.Web" = "Web service utilities"
    "Hazina.Tools.Services.WordPress" = "WordPress integration"

    # UI
    "Hazina.ChatShared" = "Shared chat UI components"
}

Write-Host "Adding package metadata to projects..." -ForegroundColor Cyan

$projects = Get-ChildItem -Path "src" -Filter "*.csproj" -Recurse |
    Where-Object {
        $_.FullName -notmatch "Tests" -and
        $_.FullName -notmatch "\\apps\\" -and
        $_.FullName -notmatch "\\Apps\\"
    }

$updatedCount = 0

foreach ($project in $projects) {
    $projectName = $project.BaseName
    $description = $descriptions[$projectName]

    if ([string]::IsNullOrEmpty($description)) {
        Write-Host "Warning: No description for $projectName" -ForegroundColor Yellow
        continue
    }

    Write-Host "Updating: $projectName" -ForegroundColor Green

    # Read project file
    [xml]$xml = Get-Content $project.FullName

    # Check if Description already exists
    $hasDescription = $false
    foreach ($pg in $xml.Project.PropertyGroup) {
        if ($pg.Description) {
            $hasDescription = $true
            break
        }
    }

    if ($hasDescription) {
        Write-Host "  Already has Description, skipping..." -ForegroundColor Yellow
        continue
    }

    # Add Description to first PropertyGroup
    $firstPropGroup = $xml.Project.PropertyGroup[0]
    if ($firstPropGroup) {
        $descNode = $xml.CreateElement("Description")
        $descNode.InnerText = $description
        $firstPropGroup.AppendChild($descNode) | Out-Null

        $xml.Save($project.FullName)
        $updatedCount++
        Write-Host "  Added description" -ForegroundColor Green
    } else {
        Write-Host "  Could not find PropertyGroup" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Updated $updatedCount projects" -ForegroundColor Cyan
