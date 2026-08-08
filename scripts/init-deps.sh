#!/usr/bin/env sh
# Initializes the pinned GH-Toolkit submodule and applies the PenguinComb cross-platform
# patches. Run this once after cloning (or after `git submodule update --init`).
#
# Usage: scripts/init-deps.sh
set -e

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TOOLKIT="$ROOT/external/GH-Toolkit"
PATCH="$ROOT/patches/gh-toolkit-crossplatform.patch"

if [ ! -d "$TOOLKIT/.git" ]; then
    echo "Initializing GH-Toolkit submodule..."
    git -C "$ROOT" submodule update --init --recursive
fi

if [ ! -f "$TOOLKIT/GH-Toolkit-Core.csproj" ]; then
    echo "ERROR: GH-Toolkit checkout is missing. Run: git submodule update --init --recursive"
    exit 1
fi

# Detect whether the patch is already applied (check for a marker line we introduced).
if grep -q "PenguinComb patch" "$TOOLKIT/Methods/GlobalVariables.cs" 2>/dev/null; then
    echo "GH-Toolkit patches already applied."
    exit 0
fi

echo "Applying PenguinComb cross-platform patches to GH-Toolkit..."
git -C "$TOOLKIT" apply --check "$PATCH"
git -C "$TOOLKIT" apply "$PATCH"
echo "Done. GH-Toolkit is patched and ready to build."
