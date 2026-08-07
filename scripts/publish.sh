#!/usr/bin/env sh
# Publishes Honeycomb for a target runtime and produces a ready-to-distribute
# package. Requires: dotnet SDK 8+, tar, (for linux) tar + gzip.
#
# Usage:
#   scripts/publish.sh <linux-x64|win-x64|linux-arm64> [self-contained]
# Examples:
#   scripts/publish.sh linux-x64          # framework-dependent
#   scripts/publish.sh win-x64            # framework-dependent
#   scripts/publish.sh linux-x64 self     # self-contained
set -e

RID="${1:?usage: publish.sh <linux-x64|win-x64|linux-arm64> [self-contained]}"
SELF_CONTAINED="${2:-}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/artifacts"

# Single-node MSBuild is slightly slower but avoids node-communication failures in
# restricted environments (containers, sandboxes). Harmless on regular machines.
export MSBUILDDISABLENODEREUSE=1

echo "Publishing Honeycomb for $RID ..."
SC=""
if [ "$SELF_CONTAINED" = "self" ]; then
    SC="--self-contained true"
fi

dotnet publish "$ROOT/src/Honeycomb.App/Honeycomb.App.csproj" \
    -c Release -r "$RID" $SC -m:1 -o "$OUT/publish-$RID"

if [ "$RID" = "win-x64" ]; then
    echo "Creating Windows zip..."
    (cd "$OUT" && tar -czf "honeycomb-$RID.tar.gz" "publish-$RID")
    echo "Windows package: $OUT/honeycomb-$RID.tar.gz"
    exit 0
fi

# ---- Linux packaging: .tar.gz + desktop entry + icon ----
PKG_DIR="$OUT/honeycomb-linux-$RID"
rm -rf "$PKG_DIR"
mkdir -p "$PKG_DIR"
cp -r "$OUT/publish-$RID"/. "$PKG_DIR/"
install -D -m 0644 "$ROOT/packaging/linux/honeycomb.desktop" "$PKG_DIR/honeycomb.desktop"
install -D -m 0644 "$ROOT/packaging/linux/honeycomb.svg" "$PKG_DIR/honeycomb.svg"

(cd "$OUT" && tar -czf "honeycomb-$RID.tar.gz" "honeycomb-linux-$RID")
echo "Linux package: $OUT/honeycomb-$RID.tar.gz"
echo "Contents:"
tar -tzf "$OUT/honeycomb-$RID.tar.gz" | head -8
