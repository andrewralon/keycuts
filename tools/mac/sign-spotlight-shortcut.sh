#!/bin/bash
# Signs installer/macos/Run keycut.plist (the Spotlight shortcut, issue #328) into <resources>/Spotlight/Run keycut.shortcut, so Settings can
# add it to Shortcuts. Signed at package time: `shortcuts sign --mode anyone` gets a fresh Apple certificate (about 13 months), so a file
# committed once would stop importing. It needs the .shortcut extension on its input, and the name Shortcuts shows is the file's.
# It never fails the build: without the file the app hides the Spotlight row.
#
#   tools/mac/sign-spotlight-shortcut.sh <the app's Contents/Resources folder>
set -uo pipefail

resources="${1:?usage: sign-spotlight-shortcut.sh <Contents/Resources folder>}"
repo="$(cd "$(dirname "$0")/../.." && pwd)"
work="$(mktemp -d)"
out="$resources/Spotlight"

cp "$repo/installer/macos/Run keycut.plist" "$work/Run keycut.shortcut"
mkdir -p "$out"

if shortcuts sign --mode anyone --input "$work/Run keycut.shortcut" --output "$out/Run keycut.shortcut" >&2; then
  chmod 644 "$out/Run keycut.shortcut"   # the signer writes 600, which an installed (root-owned) copy couldn't be read from
  echo "signed the Spotlight shortcut: $out/Run keycut.shortcut" >&2
else
  rm -rf "$out"
  echo "warning: couldn't sign the Spotlight shortcut; the app will leave its Spotlight row out" >&2
fi

rm -rf "$work"
exit 0
