#!/bin/bash
# Publish NuGet packages for all projects
# Usage: ./publish-nuget.sh
# Requires: NUGET_API_KEY environment variable

set -e

OUTPUT_DIR="./nupkgs"

echo "========================================"
echo "DevGPT Tools - NuGet Package Publisher"
echo "========================================"
echo ""

# Get API key from environment
API_KEY="${NUGET_API_KEY}"
SOURCE="https://api.nuget.org/v3/index.json"

if [ -z "$API_KEY" ]; then
    echo "WARNING: NUGET_API_KEY environment variable not set"
    echo "Packages will be built but NOT published to NuGet.org"
    echo "To set the API key, run: export NUGET_API_KEY='your-api-key'"
    echo ""
    PUSH_TO_NUGET=false
else
    echo "NuGet API Key found - packages will be published to nuget.org"
    echo ""
    PUSH_TO_NUGET=true
fi

# Find all project files (excluding obj and bin directories)
mapfile -t projects < <(find . -name "*.csproj" -not -path "*/obj/*" -not -path "*/bin/*")

echo "Found ${#projects[@]} projects to package"
echo ""

# Create output directory if it doesn't exist
if [ ! -d "$OUTPUT_DIR" ]; then
    mkdir -p "$OUTPUT_DIR"
    echo "Created output directory: $OUTPUT_DIR"
fi

# Clean previous packages
echo "Cleaning previous packages..."
rm -f "$OUTPUT_DIR"/*.nupkg
echo ""

# Build and pack each project
SUCCESS_COUNT=0
FAILURE_COUNT=0
FAILED_PROJECTS=()

for project in "${projects[@]}"; do
    project_name=$(basename "$project")
    echo "Processing: $project_name"
    echo "  Path: $(dirname "$project")"

    if dotnet restore "$project" --verbosity quiet && \
       dotnet build "$project" -c Release --no-restore --verbosity quiet && \
       dotnet pack "$project" -c Release --no-build -o "$OUTPUT_DIR" --verbosity quiet; then
        echo "  SUCCESS"
        ((SUCCESS_COUNT++))
    else
        echo "  FAILED"
        ((FAILURE_COUNT++))
        FAILED_PROJECTS+=("$project_name")
    fi
    echo ""
done

# Summary
echo "========================================"
echo "Build Summary"
echo "========================================"
echo "Successful: $SUCCESS_COUNT"
echo "Failed: $FAILURE_COUNT"

if [ $FAILURE_COUNT -gt 0 ]; then
    echo ""
    echo "Failed projects:"
    for failed in "${FAILED_PROJECTS[@]}"; do
        echo "  - $failed"
    done
    echo ""
    echo "Skipping NuGet publish due to build failures"
    exit 1
fi

# List created packages
echo ""
echo "Created packages:"
for package in "$OUTPUT_DIR"/*.nupkg; do
    if [ -f "$package" ]; then
        echo "  - $(basename "$package")"
    fi
done

# Push to NuGet automatically if API key is set
if [ "$PUSH_TO_NUGET" = true ]; then
    echo ""
    echo "========================================"
    echo "Publishing packages to NuGet.org"
    echo "========================================"
    echo ""

    PUSH_SUCCESS_COUNT=0
    PUSH_FAILURE_COUNT=0

    for package in "$OUTPUT_DIR"/*.nupkg; do
        if [ -f "$package" ]; then
            echo "Pushing: $(basename "$package")..."
            if dotnet nuget push "$package" --source "$SOURCE" --api-key "$API_KEY" --skip-duplicate; then
                echo "  SUCCESS"
                ((PUSH_SUCCESS_COUNT++))
            else
                echo "  FAILED"
                ((PUSH_FAILURE_COUNT++))
            fi
        fi
    done

    echo ""
    echo "========================================"
    echo "Publish Summary"
    echo "========================================"
    echo "Successfully published: $PUSH_SUCCESS_COUNT"
    echo "Failed to publish: $PUSH_FAILURE_COUNT"
else
    echo ""
    echo "Packages built successfully but NOT published (NUGET_API_KEY not set)"
fi

echo ""
echo "Done!"
