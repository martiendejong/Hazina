#!/bin/bash
# install-linux.sh - Universal Linux installer for Hazina Orchestration
#
# Works on: Ubuntu, Debian, RHEL, Fedora, CentOS, Arch, openSUSE, and derivatives
#
# Usage:
#   curl -sSL https://get.hazina.dev | sudo bash
#
# Or download and run locally:
#   chmod +x install-linux.sh
#   sudo ./install-linux.sh

set -e

VERSION="${VERSION:-1.0.0}"
INSTALL_DIR="/opt/hazina-orchestration"
SERVICE_FILE="/etc/systemd/system/hazina-orchestration.service"
SERVICE_USER="hazina"
DOWNLOAD_URL="https://github.com/martiendejong/Hazina/releases/download/v${VERSION}/hazina-orchestration-linux-x64.tar.gz"

echo "╔════════════════════════════════════════════════════════════════╗"
echo "║   Hazina Orchestration Installer for Linux                    ║"
echo "║   Version: ${VERSION}                                              ║"
echo "╚════════════════════════════════════════════════════════════════╝"
echo ""

# Check if running as root
if [[ $EUID -ne 0 ]]; then
   echo "❌ Error: This installer must be run as root."
   echo "   Please run: sudo $0"
   exit 1
fi

# Detect Linux distribution
if [ -f /etc/os-release ]; then
    . /etc/os-release
    DISTRO=$ID
    DISTRO_VERSION=$VERSION_ID
    echo "Detected: $PRETTY_NAME"
else
    echo "⚠️  Warning: Unable to detect Linux distribution."
    DISTRO="unknown"
fi

# Check for systemd
if ! command -v systemctl &> /dev/null; then
    echo "❌ Error: systemd is required but not found."
    echo "   This installer only supports systemd-based distributions."
    exit 1
fi

# Check for required tools
if ! command -v curl &> /dev/null; then
    echo "❌ Error: curl is required but not installed."
    echo ""
    echo "Install it with:"
    case $DISTRO in
        ubuntu|debian)
            echo "  sudo apt-get update && sudo apt-get install -y curl"
            ;;
        fedora|rhel|centos)
            echo "  sudo dnf install -y curl"
            ;;
        arch)
            echo "  sudo pacman -S curl"
            ;;
        opensuse*)
            echo "  sudo zypper install curl"
            ;;
    esac
    exit 1
fi

echo ""
echo "📥 Downloading Hazina Orchestration v${VERSION}..."
TMP_DIR=$(mktemp -d)
trap "rm -rf $TMP_DIR" EXIT

curl -L "$DOWNLOAD_URL" -o "$TMP_DIR/hazina.tar.gz" --progress-bar

if [ ! -f "$TMP_DIR/hazina.tar.gz" ]; then
    echo "❌ Download failed. Please check your internet connection."
    exit 1
fi

echo "📦 Extracting files..."
mkdir -p "$INSTALL_DIR"
tar -xzf "$TMP_DIR/hazina.tar.gz" -C "$INSTALL_DIR"

# Make executable
chmod +x "$INSTALL_DIR/HazinaOrchestration"

echo "👤 Creating service user..."
# Create service user if it doesn't exist
if ! id "$SERVICE_USER" &>/dev/null; then
    useradd --system --no-create-home --shell /bin/false "$SERVICE_USER"
    echo "   Created user: $SERVICE_USER"
else
    echo "   User already exists: $SERVICE_USER"
fi

# Set ownership
chown -R "$SERVICE_USER:$SERVICE_USER" "$INSTALL_DIR"

echo "⚙️  Creating configuration files..."

# Create appsettings.Production.json if it doesn't exist
if [ ! -f "$INSTALL_DIR/appsettings.Production.json" ]; then
    cat > "$INSTALL_DIR/appsettings.Production.json" << 'EOF'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5123"
      }
    }
  },
  "AllowedHosts": "*"
}
EOF
    chown "$SERVICE_USER:$SERVICE_USER" "$INSTALL_DIR/appsettings.Production.json"
fi

# Create systemd service
echo "🚀 Setting up systemd service..."
cat > "$SERVICE_FILE" << EOF
[Unit]
Description=Hazina Agentic Orchestration
Documentation=https://github.com/martiendejong/Hazina
After=network-online.target
Wants=network-online.target

[Service]
Type=notify
User=$SERVICE_USER
Group=$SERVICE_USER
WorkingDirectory=$INSTALL_DIR
ExecStart=$INSTALL_DIR/HazinaOrchestration
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=hazina-orchestration
TimeoutStopSec=30

# Security hardening
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=$INSTALL_DIR

# Environment
Environment="ASPNETCORE_ENVIRONMENT=Production"
Environment="DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false"

[Install]
WantedBy=multi-user.target
EOF

# Reload systemd
echo "🔄 Reloading systemd daemon..."
systemctl daemon-reload

# Enable and start service
echo "▶️  Starting service..."
systemctl enable hazina-orchestration
systemctl start hazina-orchestration

# Wait a moment for service to start
sleep 2

# Check service status
if systemctl is-active --quiet hazina-orchestration; then
    SERVICE_STATUS="✅ Running"
else
    SERVICE_STATUS="❌ Failed to start"
fi

echo ""
echo "╔════════════════════════════════════════════════════════════════╗"
echo "║                 Installation Complete!                         ║"
echo "╚════════════════════════════════════════════════════════════════╝"
echo ""
echo "Service Status: $SERVICE_STATUS"
echo ""
echo "Installation Details:"
echo "  Location:     $INSTALL_DIR"
echo "  User:         $SERVICE_USER"
echo "  Service:      hazina-orchestration.service"
echo "  URL:          http://localhost:5123"
echo ""
echo "Management Commands:"
echo "  Status:       sudo systemctl status hazina-orchestration"
echo "  Start:        sudo systemctl start hazina-orchestration"
echo "  Stop:         sudo systemctl stop hazina-orchestration"
echo "  Restart:      sudo systemctl restart hazina-orchestration"
echo "  Enable:       sudo systemctl enable hazina-orchestration"
echo "  Disable:      sudo systemctl disable hazina-orchestration"
echo "  Logs:         sudo journalctl -u hazina-orchestration -f"
echo "  Recent logs:  sudo journalctl -u hazina-orchestration -n 50"
echo ""
echo "Uninstall:"
echo "  sudo systemctl stop hazina-orchestration"
echo "  sudo systemctl disable hazina-orchestration"
echo "  sudo rm -f $SERVICE_FILE"
echo "  sudo rm -rf $INSTALL_DIR"
echo "  sudo userdel $SERVICE_USER"
echo ""
echo "Access the web interface at:"
echo "  http://localhost:5123"
echo ""

# Show service status
echo "Current service status:"
systemctl status hazina-orchestration --no-pager || true
echo ""
