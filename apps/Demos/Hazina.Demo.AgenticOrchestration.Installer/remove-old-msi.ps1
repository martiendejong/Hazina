$msi = "C:\Projects\hazina\apps\Demos\Hazina.Demo.AgenticOrchestration.Installer\bin\Release\HazinaOrchestrationSetup.msi"
if (Test-Path $msi) {
    try {
        Remove-Item $msi -Force -ErrorAction Stop
        Write-Host "Old MSI removed"
    } catch {
        Write-Host "MSI locked, checking for msiexec..."
        Get-Process msiexec -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Sleep 2
        Remove-Item $msi -Force
        Write-Host "MSI removed after killing msiexec"
    }
} else {
    Write-Host "No old MSI found"
}
