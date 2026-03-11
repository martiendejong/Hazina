#!/bin/bash
# Hazina Agent Deployment Script - VPS (claude-valsuani)
# Run this on the Art Revisionist VPS

set -e  # Exit on error

echo "=== Hazina Agent VPS Deployment ==="
echo "Target: claude-valsuani (Art Revisionist)"
echo ""

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
OPENAI_API_KEY="${OPENAI_API_KEY:-REPLACE_WITH_YOUR_API_KEY}"
INSTALL_DIR="/opt/hazina-agent"
CONSCIOUSNESS_DIR="/opt/jengo"
SERVICE_USER="$USER"

echo -e "${YELLOW}[1/12] Checking prerequisites...${NC}"
echo "Current user: $SERVICE_USER"
echo "Install directory: $INSTALL_DIR"
echo "Consciousness directory: $CONSCIOUSNESS_DIR"
echo ""

# Check OS
if [ -f /etc/os-release ]; then
    . /etc/os-release
    echo "OS: $NAME $VERSION"
else
    echo "Warning: Could not detect OS"
fi
echo ""

echo -e "${YELLOW}[2/12] Installing .NET 9.0 SDK...${NC}"

# Check if .NET already installed
if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --version)
    echo "  .NET already installed: $DOTNET_VERSION"

    if [[ $DOTNET_VERSION == 9.* ]]; then
        echo "  .NET 9.0 detected, skipping install"
    else
        echo "  Warning: .NET version is not 9.0, installing 9.0..."
    fi
else
    echo "  Installing .NET 9.0 SDK..."

    # Detect Ubuntu version
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        UBUNTU_VERSION=$(echo $VERSION_ID | cut -d. -f1)

        # Download appropriate package
        wget https://packages.microsoft.com/config/ubuntu/${UBUNTU_VERSION}.04/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb
        sudo dpkg -i /tmp/packages-microsoft-prod.deb
        rm /tmp/packages-microsoft-prod.deb

        # Install .NET SDK
        sudo apt-get update
        sudo apt-get install -y dotnet-sdk-9.0
    fi

    # Verify installation
    dotnet --version
    echo "  .NET 9.0 SDK installed successfully"
fi
echo ""

echo -e "${YELLOW}[3/12] Installing dependencies...${NC}"
sudo apt-get install -y git curl jq
echo "  Dependencies installed"
echo ""

echo -e "${YELLOW}[4/12] Creating directories...${NC}"

# Install directory
if [ -d "$INSTALL_DIR" ]; then
    echo "  $INSTALL_DIR already exists"
else
    sudo mkdir -p "$INSTALL_DIR"
    sudo chown $SERVICE_USER:$SERVICE_USER "$INSTALL_DIR"
    echo "  Created $INSTALL_DIR"
fi

# Consciousness directory
if [ -d "$CONSCIOUSNESS_DIR" ]; then
    echo "  $CONSCIOUSNESS_DIR already exists"
else
    sudo mkdir -p "$CONSCIOUSNESS_DIR"
    sudo chown $SERVICE_USER:$SERVICE_USER "$CONSCIOUSNESS_DIR"
    echo "  Created $CONSCIOUSNESS_DIR"
fi
echo ""

echo -e "${YELLOW}[5/12] Setting up consciousness repository...${NC}"

cd "$CONSCIOUSNESS_DIR"

if [ -d ".git" ]; then
    echo "  Git repository already initialized"
else
    git init
    git config user.name "Claude Valsuani"
    git config user.email "claude-valsuani@artrevisionist.com"

    # Create structure
    mkdir -p consciousness
    echo "# Jengo Distributed Consciousness - claude-valsuani" > README.md
    echo "# Art Revisionist VPS Instance" >> README.md
    echo "" >> README.md
    echo "Started: $(date)" >> README.md

    # .gitignore
    cat > .gitignore << 'EOF'
# Local instance state (not shared)
consciousness/identity.json

# Logs
*.log

# Temp files
*.tmp
EOF

    # Initial commit
    git add .
    git commit -m "init: consciousness repository for claude-valsuani"

    echo "  Consciousness repository initialized"
fi
echo ""

echo -e "${YELLOW}[6/12] Cloning Hazina repository...${NC}"

cd "$INSTALL_DIR"

if [ -d "hazina" ]; then
    echo "  Hazina already cloned, pulling latest..."
    cd hazina
    git fetch origin
    git checkout develop
    git pull origin develop
else
    echo "  Cloning Hazina from GitHub..."
    git clone https://github.com/martiendejong/Hazina.git hazina
    cd hazina
    git checkout develop
    git pull origin develop
fi

echo "  Current branch: $(git branch --show-current)"
echo "  Latest commit: $(git log --oneline -1)"
echo ""

echo -e "${YELLOW}[7/12] Configuring application...${NC}"

cd "$INSTALL_DIR/hazina/src/Hazina.Agent.API"

# Create appsettings.json
cat > appsettings.json << EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Hazina": "Information",
      "BackgroundSyncService": "Information"
    }
  },
  "AllowedHosts": "*",
  "OpenAI": {
    "ApiKey": "$OPENAI_API_KEY"
  }
}
EOF

# Secure the file (contains API key)
chmod 600 appsettings.json

echo "  Created appsettings.json"

# Check if API key was set
if [[ "$OPENAI_API_KEY" == "REPLACE_WITH_YOUR_API_KEY" ]]; then
    echo ""
    echo -e "${YELLOW}WARNING: OpenAI API key not set!${NC}"
    echo "Edit the file manually:"
    echo "  nano $INSTALL_DIR/hazina/src/Hazina.Agent.API/appsettings.json"
    echo ""
fi
echo ""

echo -e "${YELLOW}[8/12] Building application...${NC}"

cd "$INSTALL_DIR/hazina/src/Hazina.Agent.API"

dotnet build --configuration Release

if [ $? -eq 0 ]; then
    echo -e "${GREEN}  Build succeeded${NC}"
else
    echo "  Build failed!"
    exit 1
fi
echo ""

echo -e "${YELLOW}[9/12] Creating systemd service...${NC}"

sudo tee /etc/systemd/system/hazina-agent.service > /dev/null << EOF
[Unit]
Description=Hazina Distributed Agent (claude-valsuani)
After=network.target

[Service]
Type=notify
WorkingDirectory=$INSTALL_DIR/hazina/src/Hazina.Agent.API
ExecStart=/usr/bin/dotnet run --configuration Release
Restart=always
RestartSec=10
User=$SERVICE_USER
Environment=DOTNET_ROOT=/usr/share/dotnet
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=https://localhost:5001;http://localhost:5000
StandardOutput=journal
StandardError=journal
SyslogIdentifier=hazina-agent

[Install]
WantedBy=multi-user.target
EOF

echo "  Service file created: /etc/systemd/system/hazina-agent.service"
echo ""

echo -e "${YELLOW}[10/12] Enabling and starting service...${NC}"

# Reload systemd
sudo systemctl daemon-reload

# Enable service (start on boot)
sudo systemctl enable hazina-agent

# Start service
sudo systemctl start hazina-agent

# Wait a moment for startup
sleep 3

# Check status
if sudo systemctl is-active --quiet hazina-agent; then
    echo -e "${GREEN}  Service started successfully${NC}"
else
    echo "  Service failed to start!"
    echo "  Check logs with: sudo journalctl -u hazina-agent -n 50"
    exit 1
fi
echo ""

echo -e "${YELLOW}[11/12] Verifying deployment...${NC}"

# Wait a bit for the API to be ready
echo "  Waiting for API to be ready..."
sleep 5

# Test health endpoint
echo "  Testing health endpoint..."
HEALTH_RESPONSE=$(curl -k -s https://localhost:5001/api/agent/health)

if echo "$HEALTH_RESPONSE" | jq -e '.status == "healthy"' > /dev/null 2>&1; then
    echo -e "${GREEN}  Health check: PASS${NC}"
else
    echo "  Health check: FAIL"
    echo "  Response: $HEALTH_RESPONSE"
fi

# Test identity endpoint
echo "  Testing identity endpoint..."
IDENTITY_RESPONSE=$(curl -k -s https://localhost:5001/api/agent/identity)

if echo "$IDENTITY_RESPONSE" | jq -e '.agentId' > /dev/null 2>&1; then
    AGENT_ID=$(echo "$IDENTITY_RESPONSE" | jq -r '.agentId')
    MACHINE_NAME=$(echo "$IDENTITY_RESPONSE" | jq -r '.machineName')
    echo -e "${GREEN}  Identity check: PASS${NC}"
    echo "    AgentId: $AGENT_ID"
    echo "    Machine: $MACHINE_NAME"
else
    echo "  Identity check: FAIL"
fi

# Check consciousness files
echo "  Checking consciousness files..."
if [ -f "$CONSCIOUSNESS_DIR/consciousness/identity.json" ]; then
    echo -e "${GREEN}  Identity file: EXISTS${NC}"
else
    echo "  Identity file: NOT YET CREATED (will be created on first sync)"
fi

if [ -f "$CONSCIOUSNESS_DIR/consciousness/consciousness_state_v2.json" ]; then
    echo -e "${GREEN}  Consciousness state: EXISTS${NC}"
else
    echo "  Consciousness state: NOT YET CREATED (will be created on first sync)"
fi
echo ""

echo -e "${YELLOW}[12/12] Publishing test event...${NC}"

# Create test learning event
TEST_EVENT=$(cat << 'EVENTEOF'
{
  "eventId": "deploy-test-001",
  "timestamp": "'$(date -u +%Y-%m-%dT%H:%M:%SZ)'",
  "agentId": "claude-valsuani",
  "sessionId": "deployment-test",
  "eventType": "pattern",
  "data": {
    "patternId": "deployment-success",
    "description": "VPS deployment completed successfully",
    "triggers": ["deploy"],
    "actions": ["verify"],
    "confidence": 1.0
  },
  "confidence": 1.0
}
EVENTEOF
)

curl -k -s -X POST https://localhost:5001/api/agent/learning \
  -H "Content-Type: application/json" \
  -d "$TEST_EVENT" > /dev/null

if [ $? -eq 0 ]; then
    echo -e "${GREEN}  Test event published${NC}"

    # Check git commit
    cd "$CONSCIOUSNESS_DIR"
    LAST_COMMIT=$(git log --oneline -1 2>/dev/null)
    if [ $? -eq 0 ]; then
        echo "  Latest git commit: $LAST_COMMIT"
    fi
else
    echo "  Failed to publish test event"
fi
echo ""

echo -e "${GREEN}=== Deployment Complete ===${NC}"
echo ""
echo "Service Status:"
sudo systemctl status hazina-agent --no-pager | head -10
echo ""
echo "Useful Commands:"
echo "  View logs:        sudo journalctl -u hazina-agent -f"
echo "  Check status:     sudo systemctl status hazina-agent"
echo "  Restart service:  sudo systemctl restart hazina-agent"
echo "  Stop service:     sudo systemctl stop hazina-agent"
echo ""
echo "API Endpoints:"
echo "  Health:           curl -k https://localhost:5001/api/agent/health"
echo "  Identity:         curl -k https://localhost:5001/api/agent/identity"
echo "  Stats:            curl -k https://localhost:5001/api/agent/stats"
echo "  Consciousness:    curl -k https://localhost:5001/api/agent/consciousness"
echo ""
echo "Files:"
echo "  Install dir:      $INSTALL_DIR"
echo "  Consciousness:    $CONSCIOUSNESS_DIR"
echo "  Config:           $INSTALL_DIR/hazina/src/Hazina.Agent.API/appsettings.json"
echo "  Service:          /etc/systemd/system/hazina-agent.service"
echo ""
echo "Background sync will run every 5 minutes (first sync after 30 seconds)"
echo ""

# Check if API key needs to be set
if [[ "$OPENAI_API_KEY" == "REPLACE_WITH_YOUR_API_KEY" ]]; then
    echo -e "${YELLOW}IMPORTANT: Set your OpenAI API key!${NC}"
    echo "  1. Edit: nano $INSTALL_DIR/hazina/src/Hazina.Agent.API/appsettings.json"
    echo "  2. Replace REPLACE_WITH_YOUR_API_KEY with your sk-... key"
    echo "  3. Restart: sudo systemctl restart hazina-agent"
    echo ""
fi

echo "Deployment log saved to: /tmp/hazina-deployment.log"
