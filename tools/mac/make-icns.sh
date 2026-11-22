#!/bin/bash
# Writes the bundle's own icon (Porcelain, which stays fixed; the Dock icon follows Settings at runtime) as an .icns from assets/icons.
#
#   tools/mac/make-icns.sh <output.icns>
set -euo pipefail

out="${1:?usage: make-icns.sh <output.icns>}"
repo="$(cd "$(dirname "$0")/../.." && pwd)"
icons="$repo/assets/icons/porcelain"
set="$(mktemp -d)/keycuts.iconset"
mkdir -p "$set"

cp "$icons/porcelain-16.png" "$set/icon_16x16.png"
cp "$icons/porcelain-32.png" "$set/icon_16x16@2x.png"
cp "$icons/porcelain-32.png" "$set/icon_32x32.png"
cp "$icons/porcelain-64.png" "$set/icon_32x32@2x.png"
sips -z 128 128 "$icons/porcelain-256.png" --out "$set/icon_128x128.png" >/dev/null
cp "$icons/porcelain-256.png" "$set/icon_128x128@2x.png"
cp "$icons/porcelain-256.png" "$set/icon_256x256.png"
sips -z 512 512 "$icons/porcelain-1024.png" --out "$set/icon_256x256@2x.png" >/dev/null
cp "$set/icon_256x256@2x.png" "$set/icon_512x512.png"
cp "$icons/porcelain-1024.png" "$set/icon_512x512@2x.png"
mkdir -p "$(dirname "$out")"
iconutil -c icns "$set" -o "$out"
