#!/bin/bash
# Builds keycuts.app and the macOS .pkg installers (docs/macos-port.md, Phase 8; issue #106).
#
#   tools/Build-MacPackage.sh [-v x.y.z] [-a arm64|x64|both]      default: the version in Directory.Build.props, both
#
# Output: artifacts/installer/osx-<arch>/keycuts-setup-<arch>.pkg, matching the two MSIs.
#
# Signing and notarizing are on only when the identities are in the environment; without them the .pkg is ad-hoc signed and
# unsigned, fine for trying it, and Gatekeeper will object to it on another Mac:
#   APPLE_APP_IDENTITY        "Developer ID Application: Name (TEAMID)"   signs the app inside-out, with the hardened runtime
#   APPLE_INSTALLER_IDENTITY  "Developer ID Installer: Name (TEAMID)"     signs the .pkg
#   APPLE_NOTARY_KEY_PATH, APPLE_NOTARY_KEY_ID, APPLE_NOTARY_ISSUER_ID    the App Store Connect API key notarytool uses
# The release workflow (release.yml) imports the certificates from its secrets and sets these.
set -euo pipefail

repo="$(cd "$(dirname "$0")/.." && pwd)"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$repo/Directory.Build.props" | head -1)"
arch="both"

while getopts "v:a:" option; do
  case "$option" in
    v) version="$OPTARG" ;;
    a) arch="$OPTARG" ;;
    *) echo "usage: Build-MacPackage.sh [-v x.y.z] [-a arm64|x64|both]" >&2; exit 2 ;;
  esac
done

[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "version '$version' must be x.y.z" >&2; exit 2; }
archs=("$arch"); [ "$arch" = "both" ] && archs=(arm64 x64)

app_identity="${APPLE_APP_IDENTITY:-}"
installer_identity="${APPLE_INSTALLER_IDENTITY:-}"
notarize=""; [ -n "${APPLE_NOTARY_KEY_PATH:-}" ] && [ -n "${APPLE_NOTARY_KEY_ID:-}" ] && [ -n "${APPLE_NOTARY_ISSUER_ID:-}" ] && notarize=1

build_one()
{
  local arch="$1" rid="osx-$1"
  local work="$repo/artifacts/mac/$rid" out="$repo/artifacts/installer/$rid"
  local app="$work/keycuts.app" macos="$work/keycuts.app/Contents/MacOS"
  local pkg="$out/keycuts-setup-$arch.pkg"

  echo "== $rid ($version)"
  rm -rf "$work" "$out"
  mkdir -p "$macos" "$app/Contents/Resources" "$out"

  # The app and the CLI side by side, as the Windows installer does: keycuts.GUI and keycuts in Contents/MacOS
  for project in keycuts.App keycuts.CLI; do
    dotnet publish "$repo/$project/$project.csproj" -c Release -r "$rid" --self-contained true \
      -p:SatelliteResourceLanguages=en -p:Version="$version" -p:EnableWindowsTargeting=true --nologo -v q -o "$macos"
  done

  # Only code belongs in Contents/MacOS: the bundle signature treats every file there as code, and Apple's notary refuses the
  # main executable's signature when data folders sit beside it (found in Phase 8). The data goes in Resources, which
  # BundleResources reads; the .cmd shims are Windows-only.
  mv "$macos/QuickAction" "$macos/icons" "$app/Contents/Resources/"
  rm -rf "$macos/shims"

  # The Spotlight shortcut (#328): the committed, signed file (CI can't sign: no iCloud); without it the app leaves its Spotlight row out
  "$repo/tools/mac/sign-spotlight-shortcut.sh" "$app/Contents/Resources"

  sed "s/@VERSION@/$version/g" "$repo/installer/macos/Info.plist.in" > "$app/Contents/Info.plist"
  "$repo/tools/mac/make-icns.sh" "$app/Contents/Resources/keycuts.icns"
  xattr -cr "$app"

  sign_app "$app" "$macos"

  # Installs into /Applications; the app does the per-user work (the folder, the shims, the Quick Action) at its first start
  pkgbuild --component "$app" --install-location /Applications "$work/keycuts-component.pkg" >/dev/null
  productbuild --package "$work/keycuts-component.pkg" "$work/keycuts-unsigned.pkg" >/dev/null
  if [ -n "$installer_identity" ]; then
    productsign --sign "$installer_identity" --timestamp "$work/keycuts-unsigned.pkg" "$pkg" >/dev/null
  else
    cp "$work/keycuts-unsigned.pkg" "$pkg"
  fi

  if [ -n "$notarize" ] && [ -n "$installer_identity" ]; then
    xcrun notarytool submit "$pkg" --key "$APPLE_NOTARY_KEY_PATH" --key-id "$APPLE_NOTARY_KEY_ID" --issuer "$APPLE_NOTARY_ISSUER_ID" --wait
    xcrun stapler staple "$pkg"
    spctl -a -vvv -t install "$pkg"
  fi

  local signed="no" notarized="no"
  [ -n "$installer_identity" ] && signed="yes"
  [ -n "$notarize" ] && [ -n "$installer_identity" ] && notarized="yes"
  echo "   $pkg ($(du -h "$pkg" | cut -f1)), signed: $signed, notarized: $notarized"
}

# Phase 0 findings: sign every file in Contents/MacOS inside-out (the managed .dlls count as nested code), then the bundle, which
# signs the main executable and checks its siblings; no --deep. The hardened runtime needs a real identity: an ad-hoc signature
# can't use it (library validation fails on the Team ID), so a local build skips it.
sign_app()
{
  local app="$1" macos="$2" entitlements="$repo/installer/macos/entitlements.plist"
  local identity="${app_identity:--}" options=()

  # Ad-hoc ("-") still signs every nested file, or the bundle's own signature refuses the unsigned ones
  [ -n "$app_identity" ] && options=(--options runtime --timestamp --entitlements "$entitlements")

  while IFS= read -r file; do
    codesign --force ${options[@]+"${options[@]}"} --sign "$identity" "$file"
  done < <(find "$macos" -type f ! -name keycuts.GUI)

  codesign --force ${options[@]+"${options[@]}"} --sign "$identity" "$app"
  codesign --verify --strict --verbose=2 "$app"
}

for each in "${archs[@]}"; do
  build_one "$each"
done
