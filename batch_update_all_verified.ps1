# Batch update all verified complete tasks to REVIEW status

$verifiedTasks = @(
    '869cabf4y', '869cabf50', '869cabf4u', '869cabf4q', '869cabf4n',
    '869cabf3x', '869cabf3c', '869cabf3b', '869cabf2r', '869cabf5a',
    '869cabf36', '869cabf3t', '869cabf3p', '869cabf3f', '869cabf3d',
    '869ceq3aj', '869cfzy8b'
)

$successCount = 0
$failCount = 0

foreach ($taskId in $verifiedTasks) {
    Write-Host "Updating $taskId..."
    try {
        $result = & C:/scripts/tools/clickup-sync.ps1 -Action update -TaskId $taskId -Status review 2>&1
        if ($result -match "updated to status") {
            $successCount++
            Write-Host "  [OK] $taskId"
        } else {
            $failCount++
            Write-Host "  [FAIL] $taskId"
        }
    } catch {
        $failCount++
        Write-Host "  [ERROR] $taskId - $($_.Exception.Message)"
    }
}

Write-Host "`n=== Summary ==="
Write-Host "Success: $successCount"
Write-Host "Failed: $failCount"
Write-Host "Total: $($verifiedTasks.Count)"
