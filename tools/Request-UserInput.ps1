<#
.SYNOPSIS
    Request user input via Hazina web interface

.DESCRIPTION
    Allows Claude CLI to request user input through the Hazina Agentic Orchestration
    web dashboard. User receives notification and responds via browser.

.PARAMETER Prompt
    The question or prompt to show the user

.PARAMETER Type
    Type of input: 'question' (free text), 'confirmation' (yes/no), 'choice' (multiple options)

.PARAMETER Options
    Array of options for 'choice' type interactions

.PARAMETER HazinaUrl
    Base URL of Hazina API (default: https://localhost:7001)

.EXAMPLE
    Request-UserInput -Prompt "Should I create PR now?" -Type "confirmation"

.EXAMPLE
    Request-UserInput -Prompt "Which approach?" -Type "choice" -Options @("Option A", "Option B", "Cancel")

.EXAMPLE
    $answer = Request-UserInput -Prompt "What is the feature name?" -Type "question"
#>

param(
    [Parameter(Mandatory)]
    [string]$Prompt,

    [Parameter(Mandatory=$false)]
    [ValidateSet('question', 'confirmation', 'choice')]
    [string]$Type = 'question',

    [Parameter(Mandatory=$false)]
    [string[]]$Options = @(),

    [Parameter(Mandatory=$false)]
    [string]$HazinaUrl = 'https://localhost:7001'
)

$ErrorActionPreference = 'Stop'

# Get current session ID
$SessionIdFile = "C:\scripts\_machine\.current_session_id"
if (-not (Test-Path $SessionIdFile)) {
    throw "No active session found. Run agent-session.ps1 -Action start first."
}

$SessionId = (Get-Content $SessionIdFile -Raw).Trim()

Write-Host "📬 Requesting user input via Hazina web interface..." -ForegroundColor Cyan
Write-Host "   Session: $SessionId" -ForegroundColor Gray
Write-Host "   Prompt: $Prompt" -ForegroundColor Gray

# Prepare request body
$Body = @{
    Type = $Type
    Prompt = $Prompt
    Options = $Options
} | ConvertTo-Json -Compress

try {
    # Notify Hazina that we're waiting for input
    $Response = Invoke-RestMethod `
        -Uri "$HazinaUrl/api/agentic/instances/$SessionId/await-input" `
        -Method POST `
        -Body $Body `
        -ContentType "application/json" `
        -ErrorAction Stop

    $InteractionId = $Response.InteractionId

    Write-Host "✅ Notification sent! Interaction ID: $InteractionId" -ForegroundColor Green
    Write-Host "⏳ Waiting for user response..." -ForegroundColor Yellow
    Write-Host "   (User will respond via web dashboard)" -ForegroundColor Gray
    Write-Host ""

    # Poll for response
    $PollInterval = 2 # seconds
    $MaxWaitTime = 3600 # 1 hour
    $ElapsedTime = 0

    while ($ElapsedTime -lt $MaxWaitTime) {
        Start-Sleep -Seconds $PollInterval
        $ElapsedTime += $PollInterval

        try {
            $ResponseCheck = Invoke-RestMethod `
                -Uri "$HazinaUrl/api/agentic/instances/$SessionId/interactions/$InteractionId/response" `
                -Method GET `
                -ErrorAction Stop

            if ($ResponseCheck.status -eq "responded") {
                Write-Host "✅ User responded: $($ResponseCheck.response)" -ForegroundColor Green
                Write-Host "   Responded by: $($ResponseCheck.respondedBy)" -ForegroundColor Gray
                Write-Host "   Responded at: $($ResponseCheck.respondedAt)" -ForegroundColor Gray
                return $ResponseCheck.response
            }
        }
        catch {
            # Continue polling on error
        }

        # Show waiting indicator
        if ($ElapsedTime % 10 -eq 0) {
            Write-Host "   Still waiting... ($ElapsedTime seconds elapsed)" -ForegroundColor DarkGray
        }
    }

    throw "Timeout: User did not respond within $MaxWaitTime seconds"
}
catch {
    Write-Host "❌ Failed to request user input: $_" -ForegroundColor Red
    throw
}
