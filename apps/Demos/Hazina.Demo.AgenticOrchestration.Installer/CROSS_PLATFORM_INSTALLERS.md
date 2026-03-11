# Cross-Platform Installers for Hazina Orchestration

Complete installation solutions for Windows, macOS, and Linux.

---

## 📦 Installation Methods

### **Windows** (MSI Installer)

**Method 1: MSI Installer** (Recommended)
```powershell
# Download from GitHub Releases
Invoke-WebRequest -Uri "https://github.com/martiendejong/Hazina/releases/latest/download/HazinaOrchestration.msi" -OutFile "$env:TEMP\HazinaOrchestration.msi"

# Install
msiexec /i "$env:TEMP\HazinaOrchestration.msi" /qb
```

**Method 2: Winget** (Coming Soon)
```powershell
winget install Hazina.Orchestration
```

**Features:**
- ✅ Windows Service registration
- ✅ Firewall configuration
- ✅ Start menu integration
- ✅ Interactive credential configuration
- ✅ Auto-start on boot

**Documentation:** See [INSTALLATION_INSTRUCTIONS.md](./INSTALLATION_INSTRUCTIONS.md)

---

### **macOS** (Homebrew + Shell Script)

**Method 1: Homebrew** (Recommended)
```bash
# Add tap (one-time setup)
brew tap martiendejong/hazina

# Install
brew install hazina-orchestration

# Start service
brew services start hazina-orchestration
```

**Method 2: Shell Script**
```bash
# Download and run installer
curl -sSL https://raw.githubusercontent.com/martiendejong/Hazina/main/apps/Demos/Hazina.Demo.AgenticOrchestration.Installer/install-macos.sh | bash
```

**Features:**
- ✅ LaunchAgent auto-start
- ✅ Automatic updates via `brew upgrade`
- ✅ Clean uninstall
- ✅ Log file management

**Management:**
```bash
# Service control
brew services start hazina-orchestration
brew services stop hazina-orchestration
brew services restart hazina-orchestration

# View logs
tail -f ~/Library/Logs/hazina-orchestration.log

# Uninstall
brew uninstall hazina-orchestration
```

---

### **Linux** (Universal Shell Script)

**Installation:**
```bash
# One-line install (works on all distros)
curl -sSL https://get.hazina.dev | sudo bash
```

**Or download and run:**
```bash
curl -O https://raw.githubusercontent.com/martiendejong/Hazina/main/apps/Demos/Hazina.Demo.AgenticOrchestration.Installer/install-linux.sh
chmod +x install-linux.sh
sudo ./install-linux.sh
```

**Supported Distributions:**
- ✅ Ubuntu / Debian
- ✅ RHEL / Fedora / CentOS
- ✅ Arch Linux
- ✅ openSUSE
- ✅ Any systemd-based distro

**Features:**
- ✅ systemd service integration
- ✅ Dedicated service user (`hazina`)
- ✅ Security hardening (NoNewPrivileges, PrivateTmp)
- ✅ Auto-restart on failure
- ✅ journald logging

**Management:**
```bash
# Service control
sudo systemctl start hazina-orchestration
sudo systemctl stop hazina-orchestration
sudo systemctl restart hazina-orchestration
sudo systemctl status hazina-orchestration

# Enable/disable auto-start
sudo systemctl enable hazina-orchestration
sudo systemctl disable hazina-orchestration

# View logs
sudo journalctl -u hazina-orchestration -f
sudo journalctl -u hazina-orchestration -n 50

# Uninstall
sudo systemctl stop hazina-orchestration
sudo systemctl disable hazina-orchestration
sudo rm /etc/systemd/system/hazina-orchestration.service
sudo rm -rf /opt/hazina-orchestration
sudo userdel hazina
```

---

## 🔧 Post-Installation Configuration

### All Platforms

**1. Configure credentials:**

Edit `appsettings.Production.json`:
- **Windows:** `C:\Program Files (x86)\Hazina Orchestration\appsettings.Production.json`
- **macOS (Homebrew):** `/usr/local/Cellar/hazina-orchestration/<version>/appsettings.Production.json`
- **macOS (Script):** `/usr/local/hazina-orchestration/appsettings.Production.json`
- **Linux:** `/opt/hazina-orchestration/appsettings.Production.json`

**2. Access web interface:**
- **Windows:** `https://localhost:5123`
- **macOS/Linux:** `http://localhost:5123` (or `https://` if certificate configured)

**3. Set up Claude CLI integration:**

See [README.md](../../src/Hazina.AgenticOrchestration/README.md) for Claude CLI integration details.

---

## 🚀 Building Release Artifacts

### Prerequisites

- .NET 9.0 SDK
- PowerShell (for Windows MSI)
- tar and gzip (for macOS/Linux)

### Build Commands

**Windows MSI:**
```powershell
cd apps/Demos/Hazina.Demo.AgenticOrchestration.Installer
.\Build-MSI-Complete.ps1
```
Output: `bin/Release/HazinaOrchestration.msi`

**macOS Archive:**
```bash
dotnet publish src/Hazina.AgenticOrchestration/Hazina.AgenticOrchestration.csproj \
  -c Release \
  -r osx-x64 \
  --self-contained \
  -o publish/osx-x64

cd publish/osx-x64
tar -czf ../../hazina-orchestration-osx-x64.tar.gz *
```
Output: `hazina-orchestration-osx-x64.tar.gz`

**Linux Archive:**
```bash
dotnet publish src/Hazina.AgenticOrchestration/Hazina.AgenticOrchestration.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained \
  -o publish/linux-x64

cd publish/linux-x64
tar -czf ../../hazina-orchestration-linux-x64.tar.gz *
```
Output: `hazina-orchestration-linux-x64.tar.gz`

---

## 📋 Release Checklist

When creating a new release:

1. **Build all artifacts:**
   - [ ] Windows MSI
   - [ ] macOS tar.gz
   - [ ] Linux tar.gz

2. **Compute SHA256 hashes:**
   ```bash
   # macOS/Linux
   shasum -a 256 hazina-orchestration-osx-x64.tar.gz
   shasum -a 256 hazina-orchestration-linux-x64.tar.gz

   # Windows
   Get-FileHash HazinaOrchestration.msi -Algorithm SHA256
   ```

3. **Update Homebrew formula:**
   - [ ] Update `url` with new version
   - [ ] Update `sha256` with computed hash
   - [ ] Update `version`

4. **Create GitHub Release:**
   ```bash
   git tag -a v1.0.0 -m "Release v1.0.0"
   git push origin v1.0.0

   gh release create v1.0.0 \
     HazinaOrchestration.msi \
     hazina-orchestration-osx-x64.tar.gz \
     hazina-orchestration-linux-x64.tar.gz \
     --title "Hazina Orchestration v1.0.0" \
     --notes-file RELEASE_NOTES.md
   ```

5. **Test installation on each platform:**
   - [ ] Windows 10/11
   - [ ] macOS (Intel)
   - [ ] macOS (Apple Silicon)
   - [ ] Ubuntu 22.04+
   - [ ] Debian 11+
   - [ ] RHEL 8+

6. **Update documentation:**
   - [ ] Installation instructions in main README
   - [ ] Breaking changes (if any)
   - [ ] Migration guide (if needed)

---

## 🔐 Security Considerations

### Windows
- Service runs as LocalSystem (configurable to custom account)
- Firewall rules auto-configured for port 5123
- HTTPS with self-signed certificate (can be replaced)

### macOS
- LaunchAgent runs as current user
- Files owned by user, not root
- Logs in user's Library folder

### Linux
- Dedicated service user (`hazina`) with minimal privileges
- systemd security hardening:
  - `NoNewPrivileges=true`
  - `PrivateTmp=true`
  - `ProtectSystem=strict`
  - `ProtectHome=true`
- Only read/write access to install directory

---

## 🐛 Troubleshooting

### Common Issues

**"Permission denied" on macOS/Linux:**
```bash
# Make script executable
chmod +x install-macos.sh  # or install-linux.sh

# Run with appropriate privileges
sudo ./install-linux.sh    # Linux requires sudo
./install-macos.sh         # macOS runs as user
```

**Service fails to start:**
```bash
# Check logs
# Windows
Get-EventLog -LogName Application -Source "HazinaOrchestration" -Newest 10

# macOS
tail -100 ~/Library/Logs/hazina-orchestration-error.log

# Linux
sudo journalctl -u hazina-orchestration -n 50
```

**Port 5123 already in use:**

Edit configuration file:
```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:8080"  // Change port
      }
    }
  }
}
```

Restart service after config change.

**macOS security prompt blocking execution:**
1. Go to System Settings > Privacy & Security
2. Find "HazinaOrchestration was blocked"
3. Click "Allow Anyway"
4. Restart service: `brew services restart hazina-orchestration`

---

## 📚 Additional Resources

- [Main README](../../src/Hazina.AgenticOrchestration/README.md) - Feature documentation
- [Windows Installation Guide](./INSTALLATION_INSTRUCTIONS.md) - Detailed Windows setup
- [Build Guide](./BUILD_README.md) - MSI building instructions
- [Deployment Guide](./DEPLOYMENT_GUIDE.md) - Production deployment
- [GitHub Releases](https://github.com/martiendejong/Hazina/releases) - Download installers

---

## 🤝 Contributing

Found a bug or have an improvement?

1. Check [existing issues](https://github.com/martiendejong/Hazina/issues)
2. Open a new issue with:
   - Platform (Windows/macOS/Linux)
   - Installation method used
   - Error messages and logs
   - Steps to reproduce

---

## 📝 License

Part of Hazina framework - see main repository [LICENSE](../../../../LICENSE)
