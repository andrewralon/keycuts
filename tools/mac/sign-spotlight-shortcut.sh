#!/bin/bash
# The Spotlight shortcut (issue #328). Two modes:
#
#   tools/mac/sign-spotlight-shortcut.sh <Contents/Resources folder>
#       Packaging (Build-MacPackage.sh, new-dev-bundle.sh, so CI too): copies the committed, signed installer/macos/Run keycut.shortcut to
#       <Resources>/Spotlight/. Never fails the build: without the file the app hides its Spotlight row. Warns when the certificate is near its end.
#
#   tools/mac/sign-spotlight-shortcut.sh --refresh
#       On a Mac signed into iCloud (CI's runner isn't: "you must be signed into iCloud"): signs installer/macos/Run keycut.plist with
#       `shortcuts sign --mode anyone` into installer/macos/Run keycut.shortcut, and writes Run keycut.shortcut.info (the plist's SHA-256, which a
#       test checks so an edited plist can't ship unsigned, and the certificate's end). Commit both. The certificate is issued at signing and
#       lasts about 13 months, so run this before each release (CLAUDE.md, Versioning and releases).
#
# `shortcuts sign` needs the .shortcut extension on its input, and the name Shortcuts shows is the file's.
set -uo pipefail

repo="$(cd "$(dirname "$0")/../.." && pwd)"
template="$repo/installer/macos/Run keycut.plist"
signed="$repo/installer/macos/Run keycut.shortcut"
info="$signed.info"

if [ "${1:-}" = "--refresh" ]; then
  work="$(mktemp -d)"
  trap 'rm -rf "$work"' EXIT
  cp "$template" "$work/Run keycut.shortcut"

  shortcuts sign --mode anyone --input "$work/Run keycut.shortcut" --output "$signed" || { echo "couldn't sign (is this Mac signed into iCloud?)" >&2; exit 1; }
  chmod 644 "$signed"   # the signer writes 600

  # The leaf certificate sits in the archive's auth data, a plist after the 12-byte header
  expires="$(python3 - "$signed" <<'PY'
import plistlib, subprocess, sys
data = open(sys.argv[1], 'rb').read()
start = data.find(b'bplist00')
chain = None
for end in range(len(data), start, -1):
    try:
        chain = plistlib.loads(data[start:end])['SigningCertificateChain']
        break
    except Exception:
        pass
out = subprocess.run(['openssl', 'x509', '-inform', 'der', '-noout', '-enddate'], input=chain[0], capture_output=True).stdout.decode()
print(out.strip().split('=', 1)[1])
PY
)"
  expires="$(date -j -f '%b %e %T %Y %Z' "$expires" '+%Y-%m-%d' 2>/dev/null || echo "$expires")"
  # Line endings don't count, so a Windows checkout's CRLF hashes the same
  hash="$(tr -d '\r' < "$template" | shasum -a 256 | cut -d' ' -f1)"
  printf 'plist-sha256=%s\nexpires=%s\n' "$hash" "$expires" > "$info"
  echo "signed $signed (certificate until $expires); commit it and $info" >&2
  exit 0
fi

resources="${1:?usage: sign-spotlight-shortcut.sh <Contents/Resources folder> | --refresh}"

if [ ! -f "$signed" ]; then
  echo "warning: $signed is missing (run tools/mac/sign-spotlight-shortcut.sh --refresh on a Mac signed into iCloud); the app will leave its Spotlight row out" >&2
  exit 0
fi

mkdir -p "$resources/Spotlight"
cp "$signed" "$resources/Spotlight/Run keycut.shortcut"
chmod 644 "$resources/Spotlight/Run keycut.shortcut"
echo "added the Spotlight shortcut: $resources/Spotlight/Run keycut.shortcut" >&2

expires="$(sed -n 's/^expires=//p' "$info" 2>/dev/null)"
soon="$(date -u -j -v+60d '+%Y-%m-%d')"   # ISO dates compare as text
if [ -n "$expires" ] && [[ "$expires" < "$soon" ]]; then
  echo "warning: the Spotlight shortcut's certificate ends $expires; run tools/mac/sign-spotlight-shortcut.sh --refresh on a Mac and commit the result" >&2
fi
exit 0
