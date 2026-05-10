# Apply machine-specific config to installed MSI (DESKTOP-ECBAUNU)
$installDir = "C:\Program Files (x86)\Hazina Orchestration"

if (-not (Test-Path "$installDir\HazinaOrchestration.exe")) {
    $installDir = "C:\Program Files\Hazina Orchestration"
}

if (-not (Test-Path "$installDir\HazinaOrchestration.exe")) {
    Write-Host "ERROR: HazinaOrchestration.exe not found in either Program Files location" -ForegroundColor Red
    exit 1
}

Write-Host "Install dir: $installDir" -ForegroundColor Cyan

# Copy Tailscale certs
$certSource = "C:\stores\orchestration"
if (Test-Path "$certSource\tailscale.crt") {
    Copy-Item "$certSource\tailscale.crt" "$installDir\" -Force
    Copy-Item "$certSource\tailscale.key" "$installDir\" -Force
    Write-Host "Certs copied" -ForegroundColor Green
}

# Write Production config with machine-specific settings
$prodConfig = @{
    Kestrel = @{
        Endpoints = @{
            Https = @{
                Url = "https://0.0.0.0:5123"
                Certificate = @{
                    Path = "tailscale.crt"
                    KeyPath = "tailscale.key"
                }
            }
        }
    }
    Authentication = @{
        Enabled = $true
        Username = "bosi"
        Password = "Th1s1sSp4rt4!"
    }
    AgenticOrchestration = @{
        DatabasePath = "C:\scripts\_machine\agent-activity.db"
        LogsPath = "C:\scripts\logs"
        Terminal = @{
            DefaultCommand = "C:\projects\jengo\jengo-system-private\jengo.bat"
            DefaultWorkingDirectory = "C:\scripts"
        }
        SessionLogging = @{
            Enabled = $true
            BasePath = "C:\scripts\logs\agent-sessions"
        }
    }
} | ConvertTo-Json -Depth 10

Set-Content "$installDir\appsettings.Production.json" $prodConfig -Encoding UTF8
Write-Host "Production config written" -ForegroundColor Green

# Start the app
Write-Host "Starting HazinaOrchestration..." -ForegroundColor Cyan
Start-Process "$installDir\HazinaOrchestration.exe" -WorkingDirectory $installDir
Write-Host "Started! Web UI: https://localhost:5123" -ForegroundColor Green
