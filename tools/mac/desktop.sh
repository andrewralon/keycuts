#!/bin/bash
# Clears the desktop for a UI run and puts it back (tools/mac/README.md). Hides every visible app except the ones named
# and minimizes the Finder windows other than the one named; `restore` undoes exactly that, from a state file.
#
#   desktop.sh clear <state file> <Finder window name> <app name>...
#   desktop.sh restore <state file>
set -euo pipefail

mode="${1:?clear|restore}"
state="${2:?state file}"

case "$mode" in
  clear)
    keep_window="${3:?Finder window name}"
    shift 3
    keep_apps="$(printf '%s\n' Finder "$@")"
    : > "$state"
    osascript - "$state" "$keep_window" "$keep_apps" <<'APPLESCRIPT'
on run argv
  set stateFile to item 1 of argv
  set keepWindow to item 2 of argv
  set keepApps to paragraphs of (item 3 of argv)
  set acc to {}
  tell application "System Events"
    set ns to name of every process whose background only is false and visible is true
    repeat with n in ns
      if (n as text) is not in keepApps then
        set end of acc to "app|" & (n as text)
        set visible of process (n as text) to false
      end if
    end repeat
    tell process "Finder"
      repeat with i from (count of windows) to 1 by -1
        try
          set wn to name of window i
          if wn is not keepWindow and (value of attribute "AXMinimized" of window i) is false then
            set end of acc to "win|" & wn
            set value of attribute "AXMinimized" of window i to true
          end if
        end try
      end repeat
    end tell
  end tell
  set AppleScript's text item delimiters to linefeed
  do shell script "printf '%s\n' " & quoted form of (acc as text) & " > " & quoted form of stateFile
end run
APPLESCRIPT
    ;;
  restore)
    [ -s "$state" ] || exit 0
    while IFS='|' read -r kind value; do
      case "$kind" in
        win) osascript -e "tell application \"System Events\" to tell process \"Finder\" to try
set value of attribute \"AXMinimized\" of (first window whose name is \"$value\" and value of attribute \"AXMinimized\" is true) to false
end try" >/dev/null 2>&1 || true ;;
        app) osascript -e "tell application \"System Events\" to try
set visible of process \"$value\" to true
end try" >/dev/null 2>&1 || true ;;
      esac
    done < "$state"
    : > "$state"
    ;;
esac
