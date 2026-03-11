# Clean reinstall of Orchestration MSI
$installDir = "C:\Program Files (x86)\Hazina Orchestration"
$msi = "C:\Projects\hazina\apps\Demos\Hazina.Demo.AgenticOrchestration.Installer\bin\Release2\HazinaOrchestrationSetup.msi"

Write-Host "=== STEP 1: Kill running process ===" -ForegroundColor Cyan
Get-Process HazinaOrchestration -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 2

Write-Host "=== STEP 2: Uninstall old MSI ===" -ForegroundColor Cyan
# Find installed product by name
$product = Get-WmiObject Win32_Product -Filter "Name='Hazina Agentic Orchestration'" -ErrorAction SilentlyContinue
if ($product) {
    Write-Host "Found installed product: $($product.IdentifyingNumber)" -ForegroundColor Yellow
    Start-Process msiexec.exe -ArgumentList "/x $($product.IdentifyingNumber) /qn" -Wait -Verb RunAs
    Write-Host "Uninstalled" -ForegroundColor Green
    Start-Sleep 2
} else {
    Write-Host "No installed product found, trying msiexec /x on MSI..." -ForegroundColor Yellow
    Start-Process msiexec.exe -ArgumentList "/x `"$msi`" /qn" -Wait -Verb RunAs -ErrorAction SilentlyContinue
    Start-Sleep 2
}

Write-Host "=== STEP 3: Clean install directory ===" -ForegroundColor Cyan
if (Test-Path $installDir) {
    Remove-Item $installDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Cleaned $installDir" -ForegroundColor Green
} else {
    Write-Host "Install dir already clean" -ForegroundColor Green
}

Write-Host "=== STEP 4: Fresh install ===" -ForegroundColor Cyan
$proc = Start-Process msiexec.exe -ArgumentList "/i `"$msi`" /qn" -Wait -PassThru -Verb RunAs
if ($proc.ExitCode -eq 0) {
    Write-Host "MSI installed successfully!" -ForegroundColor Green
} else {
    Write-Host "Install failed: exit code $($proc.ExitCode)" -ForegroundColor Red
    exit 1
}

Write-Host "=== STEP 5: Apply machine config ===" -ForegroundColor Cyan

# Production config for this PC
$prodConfig = @'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://0.0.0.0:5123",
        "Certificate": {
          "Path": "tailscale.crt",
          "KeyPath": "tailscale.key"
        }
      }
    }
  },
  "Authentication": {
    "Enabled": true,
    "Username": "bosi",
    "Password": "Th1s1sSp4rt4!"
  },
  "AgenticOrchestration": {
    "DatabasePath": "C:\\scripts\\_machine\\agent-activity.db",
    "LogsPath": "C:\\scripts\\logs",
    "Terminal": {
      "DefaultCommand": "C:\\scripts\\claude_agent.bat",
      "DefaultWorkingDirectory": "C:\\scripts"
    },
    "SessionLogging": {
      "Enabled": true,
      "BasePath": "C:\\scripts\\logs\\agent-sessions"
    }
  },
  "Swagger": {
    "Enabled": true
  }
}
'@
Set-Content "$installDir\appsettings.Production.json" $prodConfig -Encoding UTF8 -Force
Write-Host "Production config written" -ForegroundColor Green

# Copy Tailscale certs
Copy-Item "C:\stores\orchestration\tailscale.crt" "$installDir\" -Force
Copy-Item "C:\stores\orchestration\tailscale.key" "$installDir\" -Force
Write-Host "Certs copied" -ForegroundColor Green

Write-Host "=== STEP 6: Verify wwwroot ===" -ForegroundColor Cyan
$assets = Get-ChildItem "$installDir\wwwroot\assets" -ErrorAction SilentlyContinue
if ($assets) {
    foreach ($a in $assets) { Write-Host "  $($a.Name) ($([math]::Round($a.Length/1KB,1)) KB)" -ForegroundColor Gray }
} else {
    Write-Host "WARNING: No assets found!" -ForegroundColor Red
}

Write-Host "=== STEP 7: Start ===" -ForegroundColor Cyan
Start-Process "$installDir\HazinaOrchestration.exe" -WorkingDirectory $installDir
Start-Sleep 3

$listening = netstat -ano | Select-String ":5123.*LISTENING"
if ($listening) {
    Write-Host "Orchestration running on https://localhost:5123" -ForegroundColor Green
} else {
    Write-Host "WARNING: Not listening on 5123 yet" -ForegroundColor Yellow
}

Write-Host "`n=== DONE ===" -ForegroundColor Green
