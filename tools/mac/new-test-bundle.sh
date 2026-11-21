#!/bin/bash
# Builds a throwaway keycuts.app from the Debug build so the real app can be driven on a Mac (see tools/mac/README.md).
# The bundle has its own identifier (com.teamralon.keycuts.uitest), so it never shares login items or settings with an
# installed keycuts, and runs with HOME and the output folder inside the sandbox folder you pass.
#
#   tools/mac/new-test-bundle.sh <sandbox folder>      prints the path of the .app
set -euo pipefail

sandbox="${1:?usage: new-test-bundle.sh <sandbox folder>}"
repo="$(cd "$(dirname "$0")/../.." && pwd)"
app="$sandbox/keycuts.app"
build="$repo/keycuts.App/bin/Debug/net10.0"

dotnet build "$repo/keycuts.App" -p:EnableWindowsTargeting=true --nologo -v q >&2

rm -rf "$app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources" "$sandbox/home" "$sandbox/Shortcuts"
cp -R "$build/." "$app/Contents/MacOS/"

cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleIdentifier</key><string>com.teamralon.keycuts.uitest</string>
  <key>CFBundleName</key><string>keycuts</string>
  <key>CFBundleExecutable</key><string>keycuts.GUI</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>0.0.0</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>LSMinimumSystemVersion</key><string>13.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>LSEnvironment</key>
  <dict>
    <key>DOTNET_ROOT</key><string>$(dirname "$(readlink -f "$(command -v dotnet)")")</string>
    <key>HOME</key><string>$sandbox/home</string>
    <key>KEYCUTS_OUTPUT_FOLDER</key><string>$sandbox/Shortcuts</string>
  </dict>
</dict>
</plist>
PLIST

codesign --force --deep --sign - "$app" >&2
echo "$app"
