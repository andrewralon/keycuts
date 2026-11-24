#!/bin/bash
# Builds keycuts.app for real use on this Mac: a self-contained Release publish (no .NET needed) in a bundle that, unlike
# new-test-bundle.sh, has the real bundle id (com.teamralon.keycuts, which the Finder Quick Action opens) and uses your real settings and ~/Shortcuts. Ad-hoc signed, not notarized: for you, not for sharing.
# The proper installer (signing, notarization, .pkg) is Phase 8 of docs/macos-port.md.
#
#   tools/mac/new-dev-bundle.sh [output folder]      default ~/Desktop; prints the path of keycuts.app
set -euo pipefail

out="${1:-$HOME/Desktop}"
repo="$(cd "$(dirname "$0")/../.." && pwd)"
arch="$(uname -m)"; rid="osx-arm64"; [ "$arch" = "x86_64" ] && rid="osx-x64"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$repo/Directory.Build.props" | head -1)"
work="$(mktemp -d)"   # built and signed here: a synced folder such as the Desktop keeps adding attributes that codesign refuses
app="$work/keycuts.app"

rm -rf "$app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
# The app and the CLI side by side, as in the package: the app only writes the shims, the folder pointer and the Quick Action when the
# CLI is beside it
for project in keycuts.App keycuts.CLI; do
  dotnet publish "$repo/$project/$project.csproj" -c Release -r "$rid" --self-contained -p:EnableWindowsTargeting=true --nologo -v q -o "$app/Contents/MacOS" >&2
done

# The Spotlight shortcut (#328): the committed, signed file, so Settings' Spotlight row is offered
"$repo/tools/mac/sign-spotlight-shortcut.sh" "$app/Contents/Resources"

# The bundle's own icon (Finder, Desktop, Login Items) is Porcelain and stays fixed; the Dock icon follows Settings at runtime.
"$repo/tools/mac/make-icns.sh" "$app/Contents/Resources/keycuts.icns"

cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleIdentifier</key><string>com.teamralon.keycuts</string>
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

xattr -cr "$app"
codesign --force --deep --sign - "$app" >&2
rm -rf "$out/keycuts.app"
mv "$app" "$out/keycuts.app"
echo "$out/keycuts.app"
