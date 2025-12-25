# PowerShell script to replace all remaining DevGPT references with Hazina in .cs files
# This script performs systematic renaming across the Hazina repository

$ErrorActionPreference = "Stop"

# Change to the repository root
Set-Location "C:\projects\Hazina"

Write-Host "Starting DevGPT to Hazina renaming process..." -ForegroundColor Green

# Get all .cs files that still contain "DevGPT"
$filesWithDevGPT = Get-ChildItem -Path . -Include *.cs -Recurse -File |
    Where-Object { $_.FullName -notlike "*\obj\*" -and $_.FullName -notlike "*\bin\*" } |
    Where-Object { (Get-Content $_.FullName -Raw) -match "DevGPT" }

Write-Host "Found $($filesWithDevGPT.Count) files with DevGPT references" -ForegroundColor Yellow

$processedFiles = 0
$totalReplacements = 0

foreach ($file in $filesWithDevGPT) {
    Write-Host "Processing: $($file.FullName)" -ForegroundColor Cyan

    $content = Get-Content $file.FullName -Raw
    $originalContent = $content

    # Perform replacements in order of specificity (most specific first)
    $content = $content -replace 'DevGPTChatToolCall', 'HazinaChatToolCall'
    $content = $content -replace 'DevGPTChatTool', 'HazinaChatTool'
    $content = $content -replace 'DevGPTChatMessage', 'HazinaChatMessage'
    $content = $content -replace 'DevGPTMessageRole', 'HazinaMessageRole'
    $content = $content -replace 'DevGPTGeneratedImage', 'HazinaGeneratedImage'
    $content = $content -replace 'DevGPTChatResponseFormat', 'HazinaChatResponseFormat'
    $content = $content -replace 'DevGPTAgent', 'HazinaAgent'
    $content = $content -replace 'DevGPTFlow', 'HazinaFlow'
    $content = $content -replace 'DevGPTSemanticKernelExtensions', 'HazinaSemanticKernelExtensions'
    $content = $content -replace 'DevGPTOpenAIExtensions', 'HazinaOpenAIExtensions'
    $content = $content -replace 'DevGPTHuggingFaceExtensions', 'HazinaHuggingFaceExtensions'

    # Namespace replacements
    $content = $content -replace 'namespace DevGPT\.LLMs\.Anthropic', 'namespace Hazina.LLMs.Anthropic'
    $content = $content -replace 'namespace DevGPT\.LLMs\.Gemini', 'namespace Hazina.LLMs.Gemini'
    $content = $content -replace 'namespace DevGPT\.LLMs\.Mistral', 'namespace Hazina.LLMs.Mistral'
    $content = $content -replace 'namespace DevGPT\.LLMs\.HuggingFace', 'namespace Hazina.LLMs.HuggingFace'
    $content = $content -replace 'namespace DevGPT\.LLMs\.Plugins', 'namespace Hazina.LLMs.Plugins'
    $content = $content -replace 'namespace DevGPT\.LLMs\.Tools', 'namespace Hazina.LLMs.Tools'
    $content = $content -replace 'namespace DevGPT\.LLMs', 'namespace Hazina.LLMs'
    $content = $content -replace 'namespace DevGPT\.DynamicAPI', 'namespace Hazina.DynamicAPI'
    $content = $content -replace 'namespace DevGPT', 'namespace Hazina'

    # Using statements
    $content = $content -replace 'using DevGPT\.LLMs\.Anthropic', 'using Hazina.LLMs.Anthropic'
    $content = $content -replace 'using DevGPT\.LLMs\.Gemini', 'using Hazina.LLMs.Gemini'
    $content = $content -replace 'using DevGPT\.LLMs\.Mistral', 'using Hazina.LLMs.Mistral'
    $content = $content -replace 'using DevGPT\.LLMs\.HuggingFace', 'using Hazina.LLMs.HuggingFace'
    $content = $content -replace 'using DevGPT\.LLMs\.Plugins', 'using Hazina.LLMs.Plugins'
    $content = $content -replace 'using DevGPT\.LLMs\.Tools', 'using Hazina.LLMs.Tools'
    $content = $content -replace 'using DevGPT\.LLMs', 'using Hazina.LLMs'
    $content = $content -replace 'using DevGPT\.DynamicAPI', 'using Hazina.DynamicAPI'
    $content = $content -replace 'using DevGPT', 'using Hazina'

    # Comments and documentation
    $content = $content -replace 'DevGPT types', 'Hazina types'
    $content = $content -replace 'DevGPT interfaces', 'Hazina interfaces'
    $content = $content -replace 'DevGPT agents', 'Hazina agents'
    $content = $content -replace 'for DevGPT', 'for Hazina'
    $content = $content -replace 'devgptlogs', 'hazinalogs'

    # Generic DevGPT references (be careful - this is last to avoid breaking specific replacements)
    $content = $content -replace '\bDevGPT\b(?!\.)', 'Hazina'

    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        $replacementCount = (($originalContent -split "DevGPT").Count - 1)
        $totalReplacements += $replacementCount
        $processedFiles++
        Write-Host "  Updated ($replacementCount replacements)" -ForegroundColor Green
    } else {
        Write-Host "  No changes needed" -ForegroundColor Gray
    }
}

Write-Host "`nRenaming complete!" -ForegroundColor Green
Write-Host "Processed files: $processedFiles" -ForegroundColor Yellow
Write-Host "Total replacements: $totalReplacements" -ForegroundColor Yellow

Write-Host "`nVerifying remaining DevGPT references..." -ForegroundColor Cyan
$remaining = Get-ChildItem -Path . -Include *.cs -Recurse -File |
    Where-Object { $_.FullName -notlike "*\obj\*" -and $_.FullName -notlike "*\bin\*" } |
    Where-Object { (Get-Content $_.FullName -Raw) -match "DevGPT" }

if ($remaining.Count -gt 0) {
    Write-Host "WARNING: $($remaining.Count) files still contain DevGPT references" -ForegroundColor Red
    $remaining | ForEach-Object { Write-Host "  $($_.FullName)" -ForegroundColor Red }
} else {
    Write-Host "SUCCESS: All DevGPT references have been replaced with Hazina" -ForegroundColor Green
}
