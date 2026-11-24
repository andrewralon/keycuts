#!/bin/bash
# Prepares a branch that brings main back into develop (.github/workflows/backmerge.yml). Only git, and only this clone: it pushes nothing.
#
#   tools/backmerge.sh <main> <develop> <branch>      for example: tools/backmerge.sh origin/main origin/develop backmerge/abc1234
#
# Prints one word and, unless it's "none", leaves <branch> checked out:
#   none       develop already has all of main
#   history    main has no content develop lacks (a release that was squashed or rebased into main): a merge that keeps develop's files
#   clean      an ordinary merge
#   conflict   main's own commit, for a person to resolve; the files in conflict follow, one to a line
set -euo pipefail

main="${1:?usage: backmerge.sh <main> <develop> <branch>}"
develop="${2:?usage: backmerge.sh <main> <develop> <branch>}"
branch="${3:?usage: backmerge.sh <main> <develop> <branch>}"

if git merge-base --is-ancestor "$main" "$develop"; then
  echo none
  exit 0
fi

git checkout -q --no-track -B "$branch" "$develop"
sha="$(git rev-parse --short "$main")"

# Asked before merging: the copies of develop's commits that a rebase leaves on main either conflict or, worse, merge quietly and bring back
# what develop has since taken out. Only commits after the merge base count, so a revert made on main is still merged.
tree="$(git rev-parse "$main^{tree}")"
if git log --format=%T "$(git merge-base "$main" "$develop")..$develop" | grep -x "$tree" >/dev/null; then
  git merge -q --no-ff -s ours "$main" -m "Merge main ($sha) into develop (history only)"
  echo history
elif git merge -q --no-ff "$main" -m "Merge main ($sha) into develop" >/dev/null 2>&1; then
  echo clean
else
  files="$(git diff --name-only --diff-filter=U)"
  git merge --abort
  git reset -q --hard "$main"
  printf 'conflict\n%s\n' "$files"
fi
