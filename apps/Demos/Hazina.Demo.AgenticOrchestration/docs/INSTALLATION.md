# Installation Guide - Hazina Agentic Orchestration

Complete step-by-step guide to install and deploy the Hazina Agentic Orchestration system.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [System Requirements](#system-requirements)
3. [Installation Steps](#installation-steps)
4. [First-Time Setup](#first-time-setup)
5. [Verification](#verification)
6. [Troubleshooting](#troubleshooting)
7. [Next Steps](#next-steps)

---

## Prerequisites

### Required Software

1. **.NET 9.0 SDK or later**
   - Download: https://dotnet.microsoft.com/download
   - Verify: `dotnet --version` (should show 9.0.x or higher)

2. **Git** (for source control)
   - Download: https://git-scm.com/downloads
   - Verify: `git --version`

3. **Claude Code CLI** (for AI agent orchestration)
   - Installation: See Anthropic documentation
   - Verify: `claude --version`

### Optional but Recommended

- **Visual Studio 2022** or **VS Code** (for development)
- **Docker Desktop** (for containerized deployment)
- **Postman** or **curl** (for API testing)

---

## System Requirements

### Minimum Requirements
- **OS**: Windows 10/11, Linux (Ubuntu 20.04+), macOS 12+
- **RAM**: 4 GB
- **Disk**: 500 MB free space
- **CPU**: 2 cores

### Recommended for Production
- **OS**: Windows Server 2022 or Linux Server
- **RAM**: 8 GB+
- **Disk**: 2 GB+ free space (for logs and database)
- **CPU**: 4+ cores
- **Network**: Static IP or domain name for remote access

---

## Installation Steps

### Step 1: Clone the Repository

```bash
# Clone Hazina framework
git clone https://github.com/martiendejong/Hazina.git
cd Hazina/apps/Demos/Hazina.Demo.AgenticOrchestration
```

### Step 2: Restore Dependencies

```bash
# Restore NuGet packages
dotnet restore
```

### Step 3: Configure Application Settings

Create `appsettings.Secrets.json` (never commit this file):

```bash
# Copy template
cp appsettings.json appsettings.Secrets.json
```

Edit `appsettings.Secrets.json` with your settings (see [CONFIGURATION.md](./CONFIGURATION.md) for details):

```json
{
  "Authentication": {
    "Enabled": true,
    "Username": "your-username",
    "Password": "your-secure-password",
    "Jwt": {
      "SecretKey": "YOUR-SECURE-32-CHARACTER-KEY-HERE-CHANGE-THIS"
    }
  },
  "OpenAI": {
    "ApiKey": "your-openai-api-key-here"
  }
}
```

**⚠️ Security:** Never commit `appsettings.Secrets.json` to version control!

### Step 4: Set Up Directory Structure

The application needs write access to these directories:

```bash
# On Windows
mkdir C:\scripts\_machine
mkdir C:\scripts\logs
mkdir C:\scripts\logs\agent-sessions
mkdir C:\scripts\uploads

# On Linux/macOS
mkdir -p ~/scripts/_machine
mkdir -p ~/scripts/logs/agent-sessions
mkdir -p ~/scripts/uploads
```

Update paths in `appsettings.json` if you use custom locations.

### Step 5: Initialize Database

The SQLite database is created automatically on first run. Default location:
- Windows: `C:\scripts\_machine\agent-activity.db`
- Linux/macOS: `~/scripts/_machine/agent-activity.db`

No manual database setup required!

### Step 6: Build the Application

```bash
# Development build
dotnet build

# Release build (for production)
dotnet build --configuration Release
```

### Step 7: Run the Application

**Development mode** (with hot reload):
```bash
dotnet run
```

**Production mode**:
```bash
dotnet run --configuration Release
```

The application starts on **https://localhost:5123** by default.

---

## First-Time Setup

### 1. Verify Application is Running

Open your browser and navigate to:
- **Swagger UI**: https://localhost:5123/swagger
- **Health Check**: https://localhost:5123/health

You should see:
```json
{
  "status": "Healthy",
  "timestamp": "2026-03-29T00:00:00Z"
}
```

### 2. Test Authentication

If authentication is enabled, test the `/api/auth/login` endpoint:

```bash
curl -X POST https://localhost:5123/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "your-username",
    "password": "your-password"
  }'
```

Response:
```json
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "...",
  "expiresIn": 3600
}
```

Save the `accessToken` for subsequent API calls.

### 3. Create Your First Agent Instance

```bash
curl -X POST https://localhost:5123/api/agentic/instances \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "test-session-001",
    "status": "active"
  }'
```

### 4. Test SignalR Connection (Optional)

If you're building a frontend, test the SignalR hub:

```javascript
import { HubConnectionBuilder } from '@microsoft/signalr';

const connection = new HubConnectionBuilder()
    .withUrl('https://localhost:5123/hubs/agentic')
    .build();

await connection.start();
console.log('Connected to SignalR hub!');
```

---

## Verification

### Quick Verification Checklist

Run through this checklist to ensure everything is working:

- [ ] Application starts without errors
- [ ] Swagger UI loads at https://localhost:5123/swagger
- [ ] Health check returns "Healthy"
- [ ] Database file exists at configured path
- [ ] Logs directory exists and is writable
- [ ] Authentication works (if enabled)
- [ ] Can create agent instances via API
- [ ] SignalR hub is accessible (if enabled)

### Run Automated Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal
```

All tests should pass ✅

---

## Troubleshooting

### Common Issues

#### 1. Port Already in Use

**Error:** `System.Net.Sockets.SocketException: Address already in use`

**Solution:**
```bash
# Change port in appsettings.json
"Kestrel": {
  "Endpoints": {
    "Https": {
      "Url": "https://localhost:5124"  # Different port
    }
  }
}
```

#### 2. Database Permission Denied

**Error:** `SQLite Error: unable to open database file`

**Solution:**
- Ensure directory exists: `mkdir -p C:\scripts\_machine`
- Check write permissions
- On Linux: `chmod 755 ~/scripts/_machine`

#### 3. Authentication Fails

**Error:** `401 Unauthorized`

**Solutions:**
- Verify username/password in `appsettings.Secrets.json`
- Check JWT secret key is at least 32 characters
- Ensure `Authentication.Enabled` is `true`
- Try disabling authentication temporarily for testing

#### 4. Claude CLI Not Found

**Error:** `The system cannot find the file specified: C:\scripts\claude_agent.bat`

**Solution:**
- Install Claude Code CLI
- Update `Terminal.DefaultCommand` in config
- Verify path: `where claude` (Windows) or `which claude` (Linux)

#### 5. OpenAI API Errors

**Error:** `401 Unauthorized` from OpenAI

**Solution:**
- Verify API key in `appsettings.Secrets.json`
- Check API key hasn't expired
- Ensure key has correct permissions

### Getting Help

If you encounter issues not covered here:

1. **Check logs**: `C:\scripts\logs\` (or `~/scripts/logs/`)
2. **Enable detailed logging**:
   ```json
   "Logging": {
     "LogLevel": {
       "Default": "Debug"
     }
   }
   ```
3. **GitHub Issues**: https://github.com/martiendejong/Hazina/issues
4. **Documentation**: [CONFIGURATION.md](./CONFIGURATION.md)

---

## Next Steps

Now that installation is complete:

1. **Read Configuration Guide**: [CONFIGURATION.md](./CONFIGURATION.md)
2. **Explore API**: Open Swagger UI and try endpoints
3. **Build a Frontend**: Use SignalR for real-time updates
4. **Deploy to Production**: See [Deployment Guide](#deployment-for-production)
5. **Integrate with ClickUp**: Pull tasks automatically

---

## Deployment for Production

### Option 1: Windows Service

```powershell
# Publish for Windows
dotnet publish -c Release -r win-x64 --self-contained

# Install as Windows Service (requires sc.exe or NSSM)
sc create "HazinaOrchestration" binPath="C:\path\to\Hazina.Demo.AgenticOrchestration.exe"
sc start "HazinaOrchestration"
```

### Option 2: Linux systemd Service

```bash
# Publish for Linux
dotnet publish -c Release -r linux-x64 --self-contained

# Create systemd service file
sudo nano /etc/systemd/system/hazina-orchestration.service
```

Service file:
```ini
[Unit]
Description=Hazina Agentic Orchestration
After=network.target

[Service]
Type=notify
WorkingDirectory=/opt/hazina-orchestration
ExecStart=/opt/hazina-orchestration/Hazina.Demo.AgenticOrchestration
Restart=always
User=www-data

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl enable hazina-orchestration
sudo systemctl start hazina-orchestration
```

### Option 3: Docker Container

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY publish/ .
EXPOSE 5123
ENTRYPOINT ["dotnet", "Hazina.Demo.AgenticOrchestration.dll"]
```

Build and run:
```bash
docker build -t hazina-orchestration .
docker run -d -p 5123:5123 \
  -v /data/orchestration:/scripts \
  hazina-orchestration
```

### Option 4: Cloud Deployment

**Azure App Service:**
```bash
az webapp up --name hazina-orchestration --runtime "DOTNETCORE:9.0"
```

**AWS Elastic Beanstalk:**
```bash
eb init -p "64bit Amazon Linux 2 v2.5.0 running .NET Core" hazina-orchestration
eb create hazina-production
```

---

## Security Checklist for Production

Before deploying to production, verify:

- [ ] `appsettings.Secrets.json` is NOT in version control
- [ ] JWT secret key is 32+ characters and randomly generated
- [ ] Authentication is enabled
- [ ] Strong passwords are configured
- [ ] HTTPS/TLS is enabled (don't use HTTP in production)
- [ ] Firewall rules restrict access to API
- [ ] Database backups are configured
- [ ] Log rotation is enabled
- [ ] Sensitive data is not logged
- [ ] CORS policy is properly configured
- [ ] Rate limiting is enabled (if using reverse proxy)

---

## Support

- **Documentation**: [README.md](../README.md) | [CONFIGURATION.md](./CONFIGURATION.md)
- **GitHub**: https://github.com/martiendejong/Hazina
- **Issues**: https://github.com/martiendejong/Hazina/issues
- **License**: MIT

---

**Installation Complete!** 🎉

You're now ready to orchestrate AI agents with Hazina.
