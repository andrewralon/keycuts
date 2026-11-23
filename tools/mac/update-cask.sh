#!/bin/bash
# Opens a pull request in andrewralon/homebrew-tap that sets the keycuts cask to a release (Phase 8; issue #106). The release workflow
# runs it with GH_TOKEN set to the tap's token. It writes Casks/keycuts.rb from the two .pkg files' hashes.
#
#   tools/mac/update-cask.sh <version> <arm64 .pkg> <x64 .pkg>
set -euo pipefail

version="${1:?version}"
arm_pkg="${2:?arm64 pkg}"
x64_pkg="${3:?x64 pkg}"
tap="andrewralon/homebrew-tap"
work="$(mktemp -d)"
# shasum on a Mac, sha256sum on the Linux runner the release's cask job uses
sha256() { if command -v shasum >/dev/null 2>&1; then shasum -a 256 "$1"; else sha256sum "$1"; fi | cut -d' ' -f1; }
arm_sha="$(sha256 "$arm_pkg")"
x64_sha="$(sha256 "$x64_pkg")"

gh repo clone "$tap" "$work/tap" -- --depth 1
mkdir -p "$work/tap/Casks"
cat > "$work/tap/Casks/keycuts.rb" <<RUBY
cask "keycuts" do
  arch arm: "arm64", intel: "x64"

  version "$version"
  sha256 arm:   "$arm_sha",
         intel: "$x64_sha"

  url "https://github.com/andrewralon/keycuts/releases/download/v#{version}/keycuts-setup-#{arch}.pkg"
  name "keycuts"
  desc "Open anything by nickname: from Terminal, or from the Run box"
  homepage "https://github.com/andrewralon/keycuts"

  depends_on macos: ">= :ventura"

  pkg "keycuts-setup-#{arch}.pkg"

  # The CLI (keycuts, and keypaste through its shim) is linked onto PATH
  binary "#{appdir}/keycuts.app/Contents/MacOS/keycuts"

  uninstall quit:    "com.teamralon.keycuts",
            pkgutil: "com.teamralon.keycuts"

  zap trash: [
    "~/Library/Application Support/keycuts",
    "~/Library/Services/keycut this!.workflow",
  ]
end
RUBY

cd "$work/tap"
git config user.name "keycuts release"
git config user.email "actions@users.noreply.github.com"
branch="keycuts-$version"
git checkout -b "$branch"
git add Casks/keycuts.rb
if git diff --cached --quiet; then
  echo "The cask is already at $version."
  exit 0
fi
git commit -m "keycuts $version"
# gh cloned with its token, but a plain git push has no credentials (the release run failed with "could not read Username"): give
# this one push gh's helper, from GH_TOKEN or the gh login, without touching the git config
git -c credential.helper= -c 'credential.helper=!gh auth git-credential' push origin "$branch"
gh pr create --repo "$tap" --head "$branch" --title "keycuts $version" --body "Sets the keycuts cask to v$version (written by the keycuts release workflow)."
