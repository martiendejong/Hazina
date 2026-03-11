# Tailscale HTTPS Setup voor Hazina Orchestration Demo

Deze guide helpt je om de Hazina Agentic Orchestration Demo toegankelijk te maken via Tailscale met HTTPS beveiliging.

## 📋 Vereisten

1. **Tailscale geïnstalleerd en ingelogd**
   ```bash
   # Check of Tailscale actief is
   tailscale status
   ```

2. **.NET 9.0 SDK**
   ```bash
   dotnet --version
   ```

## 🚀 Quick Start

### Optie 1: Automatische Setup (Aanbevolen)

```powershell
# Stap 1: Run setup script (eenmalig)
.\setup-tailscale-https.ps1

# Stap 2: Start de app
.\start-tailscale.ps1
```

### Optie 2: Handmatige Setup

#### Stap 1: Haal Tailscale certificaten op

```powershell
tailscale cert desktop-ecbaunu.tailca9ff1.ts.net
```

Dit genereert twee bestanden:
- `desktop-ecbaunu.tailca9ff1.ts.net.crt` (certificaat)
- `desktop-ecbaunu.tailca9ff1.ts.net.key` (private key)

#### Stap 2: Kopieer certificaten

```powershell
# Maak certs directory aan
mkdir certs

# Kopieer de certificaat bestanden
cp desktop-ecbaunu.tailca9ff1.ts.net.crt certs/
cp desktop-ecbaunu.tailca9ff1.ts.net.key certs/
```

#### Stap 3: Update wachtwoord (optioneel maar aanbevolen)

Edit `appsettings.Tailscale.json` en wijzig:
```json
"Authentication": {
  "Enabled": true,
  "Username": "admin",
  "Password": "changeme"  // ← Verander dit!
}
```

#### Stap 4: Start de applicatie

```powershell
$env:ASPNETCORE_ENVIRONMENT="Tailscale"
dotnet run
```

## 🌐 Toegang

Na het starten is de app beschikbaar op:

- **Via Tailscale:** https://desktop-ecbaunu.tailca9ff1.ts.net:5123/
- **Lokaal:** https://localhost:5123/

### Login Credentials

- **Username:** `admin`
- **Password:** `changeme` (of je eigen wachtwoord na setup)

## 📁 Bestanden Overzicht

```
Hazina.Demo.AgenticOrchestration/
├── appsettings.Tailscale.json      # Tailscale HTTPS configuratie
├── setup-tailscale-https.ps1       # Setup script (eenmalig)
├── start-tailscale.ps1             # Quick launcher
├── TAILSCALE-SETUP.md              # Deze guide
└── certs/                          # Certificaten directory
    ├── desktop-ecbaunu.tailca9ff1.ts.net.crt
    └── desktop-ecbaunu.tailca9ff1.ts.net.key
```

## 🔧 Configuratie Details

### Kestrel Endpoints

```json
"Kestrel": {
  "Endpoints": {
    "Https": {
      "Url": "https://*:5123",
      "Certificate": {
        "Path": "certs/desktop-ecbaunu.tailca9ff1.ts.net.crt",
        "KeyPath": "certs/desktop-ecbaunu.tailca9ff1.ts.net.key"
      }
    }
  }
}
```

- `https://*:5123` bindt op **alle interfaces** (inclusief Tailscale)
- Certificaat paden zijn relatief aan de app directory

### Authentication

Basic Authentication is standaard ingeschakeld voor veiligheid:
- Username: `admin`
- Password: configureerbaar in appsettings
- Realm: `Hazina Agentic Orchestration`

## ❗ Troubleshooting

### Certificate Not Found Error

**Probleem:** App geeft "Certificate file not found" error

**Oplossing:**
```powershell
# Check of certificaten bestaan
ls certs/

# Als niet: run setup opnieuw
.\setup-tailscale-https.ps1
```

### Tailscale Not Running

**Probleem:** `tailscale cert` faalt

**Oplossing:**
```powershell
# Check status
tailscale status

# Start Tailscale
# (Via systray icon of system services)
```

### Port Already in Use

**Probleem:** Port 5123 is al in gebruik

**Oplossing:**
```powershell
# Find process using port 5123
netstat -ano | findstr :5123

# Stop het proces
taskkill /PID <PID> /F
```

### HTTPS Certificate Trust Issues

**Probleem:** Browser waarschuwt over certificaat

**Oplossing:**
- Tailscale certificaten zijn geldig voor Tailscale netwerk
- Lokale browser moet mogelijk exception maken
- Of gebruik de Tailscale hostname vanaf een ander Tailscale device

## 🔐 Security Notes

1. **Wijzig het default password!**
   - Default `changeme` is **niet veilig** voor productie
   - Gebruik een sterk wachtwoord

2. **Certificaten zijn privé**
   - Voeg `certs/` toe aan `.gitignore`
   - Deel nooit je `.key` bestanden

3. **Tailscale netwerk toegang**
   - Alleen devices in je Tailscale netwerk kunnen de app bereiken
   - Configureer Tailscale ACLs voor extra beveiliging

## 📚 Meer Info

- **Tailscale HTTPS:** https://tailscale.com/kb/1153/enabling-https/
- **Hazina Orchestration Docs:** [Link naar docs]
- **ASP.NET Core Kestrel:** https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel

## 🐛 Debugging

Voor debug logging, run met:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Tailscale"
$env:Logging__LogLevel__Default="Debug"
dotnet run
```

## 📞 Support

Bij problemen:
1. Check de logs in `logs/`
2. Verify Tailscale status: `tailscale status`
3. Check certificaat geldigheid: `openssl x509 -in certs/*.crt -text -noout`
