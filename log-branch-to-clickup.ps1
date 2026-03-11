$config = Get-Content 'C:\scripts\_machine\clickup-config.json' | ConvertFrom-Json
$headers = @{
    'Authorization' = $config.api_key
    'Content-Type' = 'application/json'
}

# Add branch comment
$commentBody = @{
    comment_text = "Branch: agent-002-agent-routing-service`nWorktree: C:\Projects\worker-agents\agent-002\hazina`nStarted: 2026-02-17"
} | ConvertTo-Json

Invoke-RestMethod -Uri 'https://api.clickup.com/api/v2/task/869c5q7qe/comment' -Method Post -Headers $headers -Body $commentBody | Out-Null

# Update status to busy
$statusBody = @{ status = 'busy' } | ConvertTo-Json
Invoke-RestMethod -Uri 'https://api.clickup.com/api/v2/task/869c5q7qe' -Method Put -Headers $headers -Body $statusBody | Out-Null

Write-Output '[OK] Branch logged in ClickUp task 869c5q7qe'
