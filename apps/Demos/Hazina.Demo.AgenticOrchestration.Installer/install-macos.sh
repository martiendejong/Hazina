#!/bin/bash
# install-macos.sh - macOS installer for Hazina Orchestration
#
# Usage:
#   curl -sSL https://raw.githubusercontent.com/martiendejong/Hazina/main/apps/Demos/Hazina.Demo.AgenticOrchestration.Installer/install-macos.sh | bash
#
# Or download and run locally:
#   chmod +x install-macos.sh
#   ./install-macos.sh

set -e

VERSION="${VERSION:-1.0.0}"
INSTALL_DIR="/usr/local/hazina-orchestration"
LAUNCH_AGENTS_DIR="$HOME/Library/LaunchAgents"
PLIST_FILE="$LAUNCH_AGENTS_DIR/com.hazina.orchestration.plist"
DOWNLOAD_URL="https://github.com/martiendejong/Hazina/releases/download/v${VERSION}/hazina-orchestration-osx-x64.tar.gz"

echo "╔════════════════════════════════════════════════════════════════╗"
echo "║   Hazina Orchestration Installer for macOS                    ║"
echo "║   Version: ${VERSION}                                              ║"
echo "╚════════════════════════════════════════════════════════════════╝"
echo ""

# Check if running on macOS
if [[ "$OSTYPE" != "darwin"* ]]; then
  echo "❌ Error: This installer is for macOS only."
  echo "   For Linux, use: curl -sSL https://get.hazina.dev/linux | sudo bash"
  exit 1
fi

# Check for required tools
if ! command -v curl &> /dev/null; then
  echo "❌ Error: curl is required but not installed."
  exit 1
fi

if ! command -v tar &> /dev/null; then
  echo "❌ Error: tar is required but not installed."
  exit 1
fi

echo "📥 Downloading Hazina Orchestration v${VERSION}..."
TMP_DIR=$(mktemp -d)
trap "rm -rf $TMP_DIR" EXIT

curl -L "$DOWNLOAD_URL" -o "$TMP_DIR/hazina.tar.gz" --progress-bar

if [ ! -f "$TMP_DIR/hazina.tar.gz" ]; then
  echo "❌ Download failed. Please check your internet connection."
  exit 1
fi

echo "📦 Extracting files..."
sudo mkdir -p "$INSTALL_DIR"
sudo tar -xzf "$TMP_DIR/hazina.tar.gz" -C "$INSTALL_DIR"

# Make executable
sudo chmod +x "$INSTALL_DIR/HazinaOrchestration"

echo "⚙️  Creating configuration files..."

# Create appsettings.Production.json if it doesn't exist
if [ ! -f "$INSTALL_DIR/appsettings.Production.json" ]; then
  cat > "$TMP_DIR/appsettings.Production.json" << 'EOF'
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
        "Url": "https://localhost:5123",
        "Certificate": {
          "Path": "certificate.pfx",
          "Password": ""
        }
      }
    }
  },
  "AllowedHosts": "*"
}
EOF
  sudo mv "$TMP_DIR/appsettings.Production.json" "$INSTALL_DIR/"
fi

# Create LaunchAgent plist for auto-start
echo "🚀 Setting up auto-start service..."
mkdir -p "$LAUNCH_AGENTS_DIR"

cat > "$TMP_DIR/com.hazina.orchestration.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.hazina.orchestration</string>

    <key>ProgramArguments</key>
    <array>
        <string>$INSTALL_DIR/HazinaOrchestration</string>
    </array>

    <key>WorkingDirectory</key>
    <string>$INSTALL_DIR</string>

    <key>RunAtLoad</key>
    <true/>

    <key>KeepAlive</key>
    <true/>

    <key>StandardOutPath</key>
    <string>$HOME/Library/Logs/hazina-orchestration.log</string>

    <key>StandardErrorPath</key>
    <string>$HOME/Library/Logs/hazina-orchestration-error.log</string>

    <key>EnvironmentVariables</key>
    <dict>
        <key>ASPNETCORE_ENVIRONMENT</key>
        <string>Production</string>
        <key>DOTNET_SYSTEM_GLOBALIZATION_INVARIANT</key>
        <string>false</string>
    </dict>
</dict>
</plist>
EOF

mv "$TMP_DIR/com.hazina.orchestration.plist" "$PLIST_FILE"
chmod 644 "$PLIST_FILE"

# Load the LaunchAgent
launchctl unload "$PLIST_FILE" 2>/dev/null || true
launchctl load "$PLIST_FILE"

echo ""
echo "✅ Installation complete!"
echo ""
echo "Service Status:"
echo "  Location: $INSTALL_DIR"
echo "  Service:  LaunchAgent (auto-starts on login)"
echo "  Logs:     $HOME/Library/Logs/hazina-orchestration.log"
echo ""
echo "Management Commands:"
echo "  Start:    launchctl load $PLIST_FILE"
echo "  Stop:     launchctl unload $PLIST_FILE"
echo "  Status:   launchctl list | grep hazina"
echo "  Logs:     tail -f $HOME/Library/Logs/hazina-orchestration.log"
echo ""
echo "Access the web interface at:"
echo "  https://localhost:5123"
echo ""
echo "Note: First run may require accepting a security prompt."
echo "      Go to System Settings > Privacy & Security if blocked."
echo ""
