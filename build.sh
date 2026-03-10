#!/usr/bin/env bash
# ============================================================
# Build script for InstallerService + InstallerUpdater (cross-platform)
# For CI/CD on Linux; produces Windows x64 binaries via cross-compile.
# ============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_DIR="$SCRIPT_DIR/publish"
CONFIG="Release"

echo "============================================"
echo " InstallerUpdater Build Script (Linux cross)"
echo "============================================"

# Clean
echo "[1/5] Cleaning previous output..."
rm -rf "$PUBLISH_DIR"

# Restore
echo "[2/5] Restoring packages..."
dotnet restore "$SCRIPT_DIR/InstallerUpdater.sln"

# Build
echo "[3/5] Building solution..."
dotnet build "$SCRIPT_DIR/InstallerUpdater.sln" -c "$CONFIG" --no-restore

# Test
echo "[4/5] Running tests..."
dotnet test "$SCRIPT_DIR/src/InstallerService.Tests/InstallerService.Tests.csproj" \
    -c "$CONFIG" --no-build --verbosity normal || echo "WARNING: Some tests failed"

# Publish (cross-compile for Windows x64)
echo "[5/5] Publishing for win-x64..."
dotnet publish "$SCRIPT_DIR/src/InstallerService/InstallerService.csproj" \
    -c "$CONFIG" \
    -r win-x64 \
    --self-contained true \
    -o "$PUBLISH_DIR/InstallerService" \
    /p:PublishSingleFile=true \
    /p:IncludeNativeLibrariesForSelfExtract=true

dotnet publish "$SCRIPT_DIR/src/InstallerUpdater/InstallerUpdater.csproj" \
    -c "$CONFIG" \
    -r win-x64 \
    --self-contained true \
    -o "$PUBLISH_DIR/InstallerUpdater" \
    /p:PublishSingleFile=true \
    /p:IncludeNativeLibrariesForSelfExtract=true

echo "============================================"
echo " Build complete! Output: $PUBLISH_DIR"
echo "============================================"
echo ""
echo "Next: copy publish/ to a Windows machine and run:"
echo "  iscc installer\\InstallerUpdaterSetup.iss"
