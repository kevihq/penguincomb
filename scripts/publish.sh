#!/usr/bin/env sh
# Publishes PenguinComb for a target runtime and produces a ready-to-distribute
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

echo "Publishing PenguinComb for $RID ..."
SC=""
if [ "$SELF_CONTAINED" = "self" ]; then
    SC="--self-contained true"
fi

dotnet publish "$ROOT/src/PenguinComb.App/PenguinComb.App.csproj" \
    -c Release -r "$RID" $SC -m:1 -o "$OUT/publish-$RID"

if [ "$RID" = "win-x64" ]; then
    echo "Creating Windows zip..."
    (cd "$OUT" && tar -czf "penguincomb-$RID.tar.gz" "publish-$RID")
    echo "Windows package: $OUT/penguincomb-$RID.tar.gz"
    exit 0
fi

# ---- Linux packaging: .tar.gz + desktop entry + icon ----
PKG_DIR="$OUT/penguincomb-linux-$RID"
rm -rf "$PKG_DIR"
mkdir -p "$PKG_DIR"
cp -r "$OUT/publish-$RID"/. "$PKG_DIR/"
install -D -m 0644 "$ROOT/packaging/linux/penguincomb.desktop" "$PKG_DIR/penguincomb.desktop"
install -D -m 0644 "$ROOT/packaging/linux/penguincomb.svg" "$PKG_DIR/penguincomb.svg"

(cd "$OUT" && tar -czf "penguincomb-$RID.tar.gz" "penguincomb-linux-$RID")
echo "Linux package: $OUT/penguincomb-$RID.tar.gz"
echo "Contents:"
tar -tzf "$OUT/penguincomb-$RID.tar.gz" | head -8
