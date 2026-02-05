#!/bin/bash
# Iteration 17: Test automation script

set -e

echo "🧪 Running HazinaCoder Tests"
echo "=============================="

# Navigate to test directory
cd "$(dirname "$0")/../"

# Restore packages
echo "📦 Restoring packages..."
dotnet restore

# Build
echo "🔨 Building..."
dotnet build --no-restore

# Run tests
echo "🧪 Running tests..."
dotnet test --no-build --logger "console;verbosity=normal"

# Coverage (optional)
if [ "$1" == "--coverage" ]; then
    echo "📊 Generating coverage report..."
    dotnet test /p:CollectCoverage=true \
                /p:CoverletOutputFormat=opencover \
                /p:CoverletOutput=./coverage/

    echo "✅ Coverage report generated at ./coverage/"
fi

echo ""
echo "✅ All tests passed!"
