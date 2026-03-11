$installDir = "C:\Program Files (x86)\Hazina Orchestration"

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

# Also ensure appsettings.json doesn't have Kestrel (clean base)
# The installed one may still have old config from WiX not overwriting
$baseConfig = Get-Content "$installDir\appsettings.json" -Raw | ConvertFrom-Json
if ($baseConfig.Kestrel) {
    Write-Host "Base appsettings.json has Kestrel section - Production.json will override it" -ForegroundColor Yellow
}

# Copy certs
$certSource = "C:\stores\orchestration"
if (Test-Path "$certSource\tailscale.crt") {
    Copy-Item "$certSource\tailscale.crt" "$installDir\" -Force
    Copy-Item "$certSource\tailscale.key" "$installDir\" -Force
    Write-Host "Certs copied" -ForegroundColor Green
} else {
    Write-Host "No certs found at $certSource" -ForegroundColor Yellow
}

# Start the app
Write-Host "Starting HazinaOrchestration..." -ForegroundColor Cyan
Start-Process "$installDir\HazinaOrchestration.exe" -WorkingDirectory $installDir
Start-Sleep 3
Write-Host "Started! Web UI: https://localhost:5123" -ForegroundColor Green
