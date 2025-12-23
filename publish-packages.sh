#!/bin/bash
# Publish all NuGet packages to NuGet.org
# Usage: ./publish-packages.sh <your-api-key>

if [ -z "$1" ]; then
    echo "Error: API key required"
    echo "Usage: ./publish-packages.sh <your-api-key>"
    exit 1
fi

API_KEY="$1"
PACKAGES_DIR="./nupkgs"

echo "Publishing NuGet packages from $PACKAGES_DIR"
echo ""

count=0
success=0
failed=0

for package in "$PACKAGES_DIR"/*.nupkg; do
    if [ -f "$package" ]; then
        count=$((count + 1))
        echo "[$count] Publishing $(basename "$package")..."

        dotnet nuget push "$package" \
            --api-key "$API_KEY" \
            --source https://api.nuget.org/v3/index.json \
            --skip-duplicate

        if [ $? -eq 0 ]; then
            echo "✓ Success"
            success=$((success + 1))
        else
            echo "✗ Failed"
            failed=$((failed + 1))
        fi
        echo ""
    fi
done

echo "========================================="
echo "Summary:"
echo "  Total:   $count"
echo "  Success: $success"
echo "  Failed:  $failed"
echo "========================================="
