#!/bin/bash

echo "========================================"
echo "  Momo Memory Plugin Build Script"
echo "========================================"
echo ""

# Check if npm is available
if ! command -v npm &> /dev/null; then
    echo "[ERROR] npm is not installed or not in PATH"
    exit 1
fi

# Install dependencies
echo "[1/4] Installing dependencies..."
npm install
if [ $? -ne 0 ]; then
    echo "[ERROR] Failed to install dependencies"
    exit 1
fi

# Compile TypeScript
echo ""
echo "[2/4] Compiling TypeScript..."
npm run compile
if [ $? -ne 0 ]; then
    echo "[ERROR] Failed to compile TypeScript"
    exit 1
fi

# Check if vsce is installed
echo ""
echo "[3/4] Checking vsce..."
if ! command -v vsce &> /dev/null; then
    echo "[INFO] vsce not found, installing globally..."
    npm install -g @vscode/vsce
    if [ $? -ne 0 ]; then
        echo "[ERROR] Failed to install vsce"
        exit 1
    fi
fi

# Package extension
echo ""
echo "[4/4] Packaging extension..."
vsce package --allow-missing-repository
if [ $? -ne 0 ]; then
    echo "[ERROR] Failed to package extension"
    exit 1
fi

echo ""
echo "========================================"
echo "  Build completed successfully!"
echo "========================================"
echo ""
echo "VSIX file generated. You can install it in VS Code:"
echo "  1. Open VS Code"
echo "  2. Press Ctrl+Shift+P"
echo "  3. Type 'Install from VSIX'"
echo "  4. Select the generated .vsix file"
echo ""

ls -la *.vsix 2>/dev/null
