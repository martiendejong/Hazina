#!/bin/bash
#
# Build Hazina Agentic Orchestration executables for Windows and Linux
#
# Usage:
#   ./build-release.sh                    # Build for all platforms
#   ./build-release.sh --platform linux   # Build only for Linux
#   ./build-release.sh --platform windows # Build only for Windows
#   ./build-release.sh --archive          # Create archives after build
#   ./build-release.sh --skip-frontend    # Skip React build
#

set -e

# Default values
PLATFORM="all"
CONFIGURATION="Release"
OUTPUT_DIR="./publish"
SKIP_FRONTEND=false
CREATE_ARCHIVE=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --platform|-p)
            PLATFORM="$2"
            shift 2
            ;;
        --config|-c)
            CONFIGURATION="$2"
            shift 2
            ;;
        --output|-o)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        --skip-frontend)
            SKIP_FRONTEND=true
            shift
            ;;
        --archive|-a)
            CREATE_ARCHIVE=true
            shift
            ;;
        --help|-h)
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  --platform, -p    Target platform: all, windows, linux (default: all)"
            echo "  --config, -c      Build configuration: Release, Debug (default: Release)"
            echo "  --output, -o      Output directory (default: ./publish)"
            echo "  --skip-frontend   Skip building React frontend"
            echo "  --archive, -a     Create zip/tar.gz archives"
            echo "  --help, -h        Show this help"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_FILE="$SCRIPT_DIR/Hazina.Demo.AgenticOrchestration.csproj"
CLIENT_APP_DIR="$SCRIPT_DIR/ClientApp"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Banner
echo ""
echo -e "${CYAN}╔═══════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║      HAZINA AGENTIC ORCHESTRATION - Release Builder               ║${NC}"
echo -e "${CYAN}╠═══════════════════════════════════════════════════════════════════╣${NC}"
echo -e "${CYAN}║  Platform: $(printf '%-54s' "$PLATFORM")║${NC}"
echo -e "${CYAN}║  Configuration: $(printf '%-49s' "$CONFIGURATION")║${NC}"
echo -e "${CYAN}║  Output: $(printf '%-56s' "$OUTPUT_DIR")║${NC}"
echo -e "${CYAN}╚═══════════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Create output directory
OUTPUT_DIR="$(cd "$(dirname "$OUTPUT_DIR")" 2>/dev/null && pwd)/$(basename "$OUTPUT_DIR")" || OUTPUT_DIR="$SCRIPT_DIR/publish"
mkdir -p "$OUTPUT_DIR"

# Step 1: Build React frontend
if [ "$SKIP_FRONTEND" = false ]; then
    echo -e "${YELLOW}📦 Building React frontend...${NC}"

    cd "$CLIENT_APP_DIR"

    echo "   Installing npm dependencies..."
    npm install --silent 2>/dev/null

    echo "   Building production bundle..."
    npm run build 2>/dev/null

    echo -e "${GREEN}   ✓ Frontend built successfully${NC}"

    cd "$SCRIPT_DIR"
else
    echo -e "\033[90m⏭️  Skipping frontend build (using existing wwwroot)${NC}"
fi

# Function to publish for a specific runtime
publish_platform() {
    local RUNTIME_ID="$1"
    local FRIENDLY_NAME="$2"

    echo ""
    echo -e "${YELLOW}🔨 Building for $FRIENDLY_NAME ($RUNTIME_ID)...${NC}"

    local PLATFORM_OUTPUT_DIR="$OUTPUT_DIR/$RUNTIME_ID"

    # Clean previous build
    rm -rf "$PLATFORM_OUTPUT_DIR"

    # Publish
    dotnet publish "$PROJECT_FILE" \
        --configuration "$CONFIGURATION" \
        --runtime "$RUNTIME_ID" \
        --output "$PLATFORM_OUTPUT_DIR" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:EnableCompressionInSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:DebugType=none \
        -p:DebugSymbols=false \
        2>&1 | grep -E "(error|warning)" || true

    # Get output file info
    local EXE_NAME
    if [[ "$RUNTIME_ID" == win* ]]; then
        EXE_NAME="HazinaOrchestration.exe"
    else
        EXE_NAME="HazinaOrchestration"
    fi

    local EXE_PATH="$PLATFORM_OUTPUT_DIR/$EXE_NAME"

    if [ -f "$EXE_PATH" ]; then
        local SIZE_MB=$(du -m "$EXE_PATH" | cut -f1)
        echo -e "${GREEN}   ✓ Built: $EXE_NAME ($SIZE_MB MB)${NC}"

        # Make executable on Linux/Mac
        if [[ "$RUNTIME_ID" != win* ]]; then
            chmod +x "$EXE_PATH"
        fi

        # Create archive if requested
        if [ "$CREATE_ARCHIVE" = true ]; then
            local ARCHIVE_NAME="HazinaOrchestration-$RUNTIME_ID"

            if [[ "$RUNTIME_ID" == win* ]]; then
                local ARCHIVE_PATH="$OUTPUT_DIR/$ARCHIVE_NAME.zip"
                echo "   📁 Creating archive: $ARCHIVE_NAME.zip"
                cd "$PLATFORM_OUTPUT_DIR"
                zip -rq "$ARCHIVE_PATH" .
                cd "$SCRIPT_DIR"
            else
                local ARCHIVE_PATH="$OUTPUT_DIR/$ARCHIVE_NAME.tar.gz"
                echo "   📁 Creating archive: $ARCHIVE_NAME.tar.gz"
                tar -czf "$ARCHIVE_PATH" -C "$PLATFORM_OUTPUT_DIR" .
            fi

            local ARCHIVE_SIZE_MB=$(du -m "$ARCHIVE_PATH" | cut -f1)
            echo -e "${GREEN}   ✓ Archive created ($ARCHIVE_SIZE_MB MB)${NC}"
        fi
    else
        echo -e "${RED}❌ Expected executable not found: $EXE_PATH${NC}"
        exit 1
    fi
}

# Step 2: Build for selected platforms
if [ "$PLATFORM" = "all" ] || [ "$PLATFORM" = "windows" ]; then
    publish_platform "win-x64" "Windows x64"
fi

if [ "$PLATFORM" = "all" ] || [ "$PLATFORM" = "linux" ]; then
    publish_platform "linux-x64" "Linux x64"
fi

# Summary
echo ""
echo -e "${GREEN}╔═══════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║                     ✅ BUILD COMPLETE                             ║${NC}"
echo -e "${GREEN}╠═══════════════════════════════════════════════════════════════════╣${NC}"
echo -e "${GREEN}║  Output directory: $(printf '%-46s' "$OUTPUT_DIR")║${NC}"
echo -e "${GREEN}╚═══════════════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${CYAN}📂 Contents:${NC}"

find "$OUTPUT_DIR" -type f \( -name "HazinaOrchestration*" -o -name "*.zip" -o -name "*.tar.gz" \) | while read -r file; do
    local REL_PATH="${file#$OUTPUT_DIR/}"
    local SIZE_MB=$(du -m "$file" | cut -f1)
    echo "   $REL_PATH ($SIZE_MB MB)"
done

echo ""
echo -e "${YELLOW}🚀 To run:${NC}"
echo "   Windows: ./publish/win-x64/HazinaOrchestration.exe"
echo "   Linux:   ./publish/linux-x64/HazinaOrchestration"
echo ""
