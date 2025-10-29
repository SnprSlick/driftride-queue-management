#!/bin/bash

# DriftRide Linting and Code Quality Script
# This script runs code analysis, linting, and quality checks

set -e

echo "🚀 DriftRide Code Quality Check"
echo "================================"

# Navigate to solution directory
cd "$(dirname "$0")/.."

echo "📦 Restoring packages..."
dotnet restore

echo "🔨 Building solution..."
dotnet build --no-restore --verbosity minimal

echo "🔍 Running code analysis..."
dotnet build --no-restore --verbosity normal --configuration Release

echo "🧪 Running tests..."
dotnet test --no-build --verbosity minimal --configuration Release

echo "✅ All checks completed successfully!"
echo "📊 Code quality metrics:"
echo "  - All projects built without errors"
echo "  - Code analysis rules applied"
echo "  - StyleCop rules enforced"
echo "  - Tests passing"