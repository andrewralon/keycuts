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
mkdir -p "$app/Contents/MacOS"
cp -R "$repo/keycuts.App/bin/Release/net10.0/$rid/publish/." "$app/Contents/MacOS/"

cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleIdentifier</key><string>com.teamralon.keycuts.dev</string>
  <key>CFBundleName</key><string>keycuts</string>
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
