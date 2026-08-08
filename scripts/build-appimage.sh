#!/usr/bin/env sh
# Builds a single-file, self-contained AppImage for Linux x64.
#
# The AppImage embeds the .NET runtime and every game resource, so users only
# need one file: download -> chmod +x -> run. No .NET install required.
#
# Requirements (build machine only):
#   - dotnet SDK 8+
#   - curl (to fetch appimagetool on first run)
#   - squashfs tools are embedded in appimagetool; FUSE is NOT required because
#     we invoke appimagetool with --appimage-extract-and-run.
#
# Usage: scripts/build-appimage.sh
# Output: artifacts/Honeycomb-<version>-x86_64.AppImage
set -e

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/artifacts"
TOOLS="$OUT/tools"
RID="linux-x64"

APPIMAGETOOL_URL="${APPIMAGETOOL_URL:-https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage}"
# Pinned checksum of the release this script was tested against (2025-12-04).
# 'continuous' moves; if the download no longer matches we warn but continue.
APPIMAGETOOL_SHA256="a6d71e2b6cd66f8e8d16c37ad164658985e0cf5fcaa950c90a482890cb9d13e0"

export MSBUILDDISABLENODEREUSE=1
VERSION="$(grep -oE '<Version>[^<]+' "$ROOT/src/Honeycomb.App/Honeycomb.App.csproj" 2>/dev/null | sed 's/<Version>//' || true)"
if [ -z "$VERSION" ]; then
    VERSION="1.0.0"
fi

# ---- 1. Self-contained publish ----
echo "Publishing self-contained build for $RID ..."
dotnet publish "$ROOT/src/Honeycomb.App/Honeycomb.App.csproj" \
    -c Release -r "$RID" --self-contained true -p:PublishSingleFile=false -m:1 \
    -o "$OUT/appimage-stage/publish"

# ---- 2. Fetch appimagetool ----
APPIMAGETOOL="$TOOLS/appimagetool-x86_64.AppImage"
if [ ! -x "$APPIMAGETOOL" ]; then
    echo "Downloading appimagetool ..."
    mkdir -p "$TOOLS"
    curl -sL -o "$APPIMAGETOOL" "$APPIMAGETOOL_URL"
    chmod +x "$APPIMAGETOOL"
fi
SUM="$(sha256sum "$APPIMAGETOOL" | awk '{print $1}')"
if [ "$SUM" != "$APPIMAGETOOL_SHA256" ]; then
    echo "WARNING: appimagetool checksum changed ($SUM). Continuing with the downloaded copy."
fi

# ---- 3. Assemble AppDir ----
APPDIR="$OUT/appimage-stage/AppDir"
rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin" "$APPDIR/usr/share/applications" "$APPDIR/usr/share/icons/hicolor/scalable/apps"

# All publish output (apphost + dlls + resources) goes into usr/bin; the app
# resolves bundled resources relative to AppContext.BaseDirectory.
cp -r "$OUT/appimage-stage/publish/." "$APPDIR/usr/bin/"
chmod +x "$APPDIR/usr/bin/Honeycomb"

cp "$ROOT/packaging/linux/honeycomb.desktop" "$APPDIR/usr/share/applications/honeycomb.desktop"
cp "$ROOT/packaging/linux/honeycomb.svg" "$APPDIR/usr/share/icons/hicolor/scalable/apps/honeycomb.svg"

# AppImage root copies of desktop entry + icon (required by the spec)
cp "$ROOT/packaging/linux/honeycomb.desktop" "$APPDIR/honeycomb.desktop"
cp "$ROOT/packaging/linux/honeycomb.svg" "$APPDIR/honeycomb.svg"

cat > "$APPDIR/AppRun" <<'EOF'
#!/bin/sh
# Launcher for the Honeycomb AppImage. APPDIR is set by the AppImage runtime
# (or by --appimage-extract-and-run); the bundled resources resolve relative
# to the executable, so no working-directory assumptions are made.
exec "$APPDIR/usr/bin/Honeycomb" "$@"
EOF
chmod +x "$APPDIR/AppRun"

# ---- 4. Build the AppImage ----
echo "Building AppImage ..."
cd "$OUT/appimage-stage"
ARCH=x86_64 "$APPIMAGETOOL" --appimage-extract-and-run --no-appstream "$APPDIR" >/dev/null

RESULT="$OUT/Honeycomb-$VERSION-x86_64.AppImage"
if [ -f "$OUT/appimage-stage/Honeycomb-x86_64.AppImage" ]; then
    mv "$OUT/appimage-stage/Honeycomb-x86_64.AppImage" "$RESULT"
elif [ -f "$RESULT" ]; then
    :
else
    # appimagetool may drop it under a different name; find it
    FOUND="$(find "$OUT/appimage-stage" -maxdepth 1 -name '*.AppImage' ! -name 'appimagetool*' | head -1)"
    if [ -n "$FOUND" ]; then
        mv "$FOUND" "$RESULT"
    else
        echo "ERROR: AppImage was not produced." >&2
        exit 1
    fi
fi

chmod +x "$RESULT"
echo "AppImage: $RESULT"
ls -lh "$RESULT"
