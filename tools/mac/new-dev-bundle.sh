#!/bin/bash
# Builds keycuts.app for real use on this Mac: a self-contained Release publish (no .NET needed) in a bundle that, unlike
# new-test-bundle.sh, uses your real settings and ~/Shortcuts. Ad-hoc signed, not notarized: for you, not for sharing.
# The proper installer (signing, notarization, .pkg) is Phase 8 of docs/macos-port.md.
#
#   tools/mac/new-dev-bundle.sh [output folder]      default ~/Desktop; prints the path of keycuts.app
set -euo pipefail

out="${1:-$HOME/Desktop}"
repo="$(cd "$(dirname "$0")/../.." && pwd)"
arch="$(uname -m)"; rid="osx-arm64"; [ "$arch" = "x86_64" ] && rid="osx-x64"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$repo/Directory.Build.props" | head -1)"
app="$out/keycuts.app"

dotnet publish "$repo/keycuts.App" -c Release -r "$rid" --self-contained -p:EnableWindowsTargeting=true --nologo -v q >&2
rm -rf "$app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp -R "$repo/keycuts.App/bin/Release/net10.0/$rid/publish/." "$app/Contents/MacOS/"

# The bundle's own icon (Finder, Desktop, Login Items) is Porcelain and stays fixed; the Dock icon follows Settings at runtime.
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
iconutil -c icns "$set" -o "$app/Contents/Resources/keycuts.icns"

cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleIdentifier</key><string>com.teamralon.keycuts.dev</string>
  <key>CFBundleName</key><string>keycuts</string>
  <key>CFBundleIconFile</key><string>keycuts</string>
  <key>CFBundleExecutable</key><string>keycuts.GUI</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>$version</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>LSMinimumSystemVersion</key><string>13.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST

codesign --force --deep --sign - "$app" >&2
echo "$app"
