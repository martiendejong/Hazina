#!/bin/bash
# Hazina CLI Installer for Linux
# Usage: curl -fsSL https://martiendejong.nl/hazina/install.sh | bash
#
# This script downloads and installs Hazina CLI to ~/.hazina
# and creates a symlink in ~/.local/bin

set -e

# Configuration
VERSION="1.0.0"
GITHUB_REPO="martiendejong/hazina"
INSTALL_DIR="${HOME}/.hazina"
BIN_DIR="${HOME}/.local/bin"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo ""
echo -e "${BLUE}================================================${NC}"
echo -e "${BLUE}  Hazina CLI Installer${NC}"
echo -e "${BLUE}  Version: ${VERSION}${NC}"
echo -e "${BLUE}================================================${NC}"
echo ""

# Detect architecture
ARCH=$(uname -m)
case $ARCH in
    x86_64)
        PLATFORM="linux-x64"
        ;;
    aarch64|arm64)
        PLATFORM="linux-arm64"
        echo -e "${YELLOW}Warning: ARM64 builds may not be available yet${NC}"
        ;;
    *)
        echo -e "${RED}Error: Unsupported architecture: $ARCH${NC}"
        exit 1
        ;;
esac

echo -e "${GREEN}Detected platform: ${PLATFORM}${NC}"
echo ""

# Check for required tools
check_command() {
    if ! command -v "$1" &> /dev/null; then
        echo -e "${RED}Error: $1 is required but not installed${NC}"
        exit 1
    fi
}

check_command "unzip"

# Create directories
echo "Creating directories..."
mkdir -p "$INSTALL_DIR"
mkdir -p "$BIN_DIR"

# Determine download URL
# First try to get the latest release version from GitHub API
if command -v curl &> /dev/null; then
    LATEST_VERSION=$(curl -s "https://api.github.com/repos/${GITHUB_REPO}/releases/latest" | grep '"tag_name":' | sed -E 's/.*"v([^"]+)".*/\1/' 2>/dev/null || echo "")
elif command -v wget &> /dev/null; then
    LATEST_VERSION=$(wget -qO- "https://api.github.com/repos/${GITHUB_REPO}/releases/latest" | grep '"tag_name":' | sed -E 's/.*"v([^"]+)".*/\1/' 2>/dev/null || echo "")
fi

# Use latest version if available, otherwise fall back to configured version
if [ -n "$LATEST_VERSION" ]; then
    VERSION="$LATEST_VERSION"
    echo -e "${GREEN}Latest version: ${VERSION}${NC}"
fi

DOWNLOAD_URL="https://github.com/${GITHUB_REPO}/releases/download/v${VERSION}/hazinacoder-${VERSION}-${PLATFORM}.zip"
TEMP_FILE="/tmp/hazinacoder-${VERSION}.zip"

echo ""
echo "Downloading Hazina CLI..."
echo -e "  ${BLUE}URL: ${DOWNLOAD_URL}${NC}"
echo ""

# Download
if command -v curl &> /dev/null; then
    curl -fsSL "$DOWNLOAD_URL" -o "$TEMP_FILE" --progress-bar
elif command -v wget &> /dev/null; then
    wget -q "$DOWNLOAD_URL" -O "$TEMP_FILE" --show-progress
else
    echo -e "${RED}Error: curl or wget required${NC}"
    exit 1
fi

# Verify download
if [ ! -f "$TEMP_FILE" ] || [ ! -s "$TEMP_FILE" ]; then
    echo -e "${RED}Error: Download failed or file is empty${NC}"
    exit 1
fi

echo ""
echo "Extracting..."
unzip -o "$TEMP_FILE" -d "$INSTALL_DIR" > /dev/null

echo "Setting permissions..."
chmod +x "$INSTALL_DIR/hazinacoder"

echo "Creating symlink..."
ln -sf "$INSTALL_DIR/hazinacoder" "$BIN_DIR/hazinacoder"

# Cleanup
rm -f "$TEMP_FILE"

echo ""
echo -e "${GREEN}================================================${NC}"
echo -e "${GREEN}  Installation Complete!${NC}"
echo -e "${GREEN}================================================${NC}"
echo ""
echo -e "Hazina CLI installed to: ${BLUE}${INSTALL_DIR}${NC}"
echo -e "Symlink created at: ${BLUE}${BIN_DIR}/hazinacoder${NC}"
echo ""

# Check if bin is in PATH
if [[ ":$PATH:" != *":$BIN_DIR:"* ]]; then
    echo -e "${YELLOW}NOTE: ${BIN_DIR} is not in your PATH${NC}"
    echo ""
    echo "Add the following to your shell profile (~/.bashrc, ~/.zshrc, etc.):"
    echo ""
    echo -e "  ${BLUE}export PATH=\"${BIN_DIR}:\$PATH\"${NC}"
    echo ""
    echo "Then restart your terminal or run:"
    echo ""
    echo -e "  ${BLUE}source ~/.bashrc${NC}  # or ~/.zshrc"
    echo ""
fi

echo "To get started, run:"
echo ""
echo -e "  ${BLUE}hazinacoder --help${NC}"
echo ""
echo "Or start an interactive session:"
echo ""
echo -e "  ${BLUE}hazinacoder${NC}"
echo ""
echo -e "${GREEN}Happy coding! 🚀${NC}"
echo ""
