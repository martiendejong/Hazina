$msi = "C:\Projects\hazina\apps\Demos\Hazina.Demo.AgenticOrchestration.Installer\bin\Release2\HazinaOrchestrationSetup.msi"

# Kill any running instance first
Get-Process HazinaOrchestration -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 2

Write-Host "Installing MSI: $msi" -ForegroundColor Cyan

# Install silently
$proc = Start-Process msiexec.exe -ArgumentList "/i `"$msi`" /qn REINSTALLMODE=amus REINSTALL=ALL" -Wait -PassThru -Verb RunAs
if ($proc.ExitCode -eq 0) {
    Write-Host "MSI installed successfully!" -ForegroundColor Green
} elseif ($proc.ExitCode -eq 1603) {
    Write-Host "Install failed (1603) - trying fresh install..." -ForegroundColor Yellow
    $proc2 = Start-Process msiexec.exe -ArgumentList "/i `"$msi`" /qn" -Wait -PassThru -Verb RunAs
    if ($proc2.ExitCode -eq 0) {
        Write-Host "Fresh install succeeded!" -ForegroundColor Green
    } else {
        Write-Host "Install failed with exit code: $($proc2.ExitCode)" -ForegroundColor Red
    }
} else {
    Write-Host "Install failed with exit code: $($proc.ExitCode)" -ForegroundColor Red
}
