#!/bin/bash
# Drives the real keycuts app on a Mac and checks what the headless tests can't (tools/mac/README.md).
#
#   tools/mac/check-app.sh [menus] [drops] [browse] [contrast] [dock]      (no argument: all of them)
#
# It builds a throwaway bundle (new-test-bundle.sh), hides your other apps and minimizes your Finder windows while it runs
# and puts them back (desktop.sh), and flips Increase contrast in System Settings for a few seconds and back.
# Needs: Accessibility and Screen Recording for the terminal running it, cliclick (brew install cliclick), the Debug build.
# Coordinates are for macOS 26 (Tahoe); another version may need them adjusted (the constants below).
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
checks=("$@")
[ ${#checks[@]} -eq 0 ] && checks=(menus drops browse contrast dock)

sandbox="$(mktemp -d "${TMPDIR:-/tmp}/keycuts-ui.XXXXXX")"
sandbox="$(cd -P "$sandbox" && pwd)"
drop="$sandbox/drop"
state="$sandbox/desktop-state.txt"
pass=0
fail=0

# macOS 26 layout: the keycuts window at WIN_X,WIN_Y; Finder's list rows; the Increase contrast switch in System Settings
WIN_X=760; WIN_Y=150; DROP_X=1000; DROP_Y=450
CONTRAST_SWITCH="678,411"

say() { printf '%s\n' "$*"; }
ok() { pass=$((pass + 1)); say "PASS  $1"; }
bad() { fail=$((fail + 1)); say "FAIL  $1"; }
check() { if [ "$2" = "$3" ]; then ok "$1"; else bad "$1 (expected '$3', got '$2')"; fi; }

cleanup()
{
  [ -n "${pid:-}" ] && kill "$pid" 2>/dev/null
  osascript -e 'tell application "Safari" to quit' >/dev/null 2>&1 || true
  # never leave the system's Increase contrast on
  [ "$("$sandbox/contrast")" = "true" ] 2>/dev/null && set_contrast false
  "$here/desktop.sh" restore "$state"
}
trap cleanup EXIT

front() { osascript -e "tell application \"System Events\" to tell (first process whose unix id is $pid) to set frontmost to true"; sleep 1; }
read_app() { front; osascript "$here/read.applescript" "$pid"; }
field() { read_app | sed -n "s/^FIELD $1 = //p" | head -1; }
find_at() { osascript "$here/find.applescript" "$pid" "$1" "$2"; }

# waits up to ~8 s for the Target field to show the expected text
wait_target()
{
  local want="$1" got=""
  for _ in 1 2 3 4; do
    got="$(field Target)"
    [ "$got" = "$want" ] && break
    sleep 1.5
  done
  echo "$got"
}

# the drag recipe that works for Finder and Safari: pause after it starts (Safari's lift animation), then move in steps
drag() { cliclick -w 120 dd:"$1","$2" dm:$(($1 + 7)),$(($2 + 6)) dm:$(($1 + 17)),$(($2 + 14)) w:1500 dm:400,300 dm:700,380 dm:"$DROP_X","$DROP_Y" dm:$((DROP_X + 4)),$((DROP_Y + 2)) w:700 du:$((DROP_X + 4)),$((DROP_Y + 2)); }

set_contrast()
{
  open "x-apple.systempreferences:com.apple.Accessibility-Settings.extension?Display"; sleep 3
  [ "$("$sandbox/contrast")" != "$1" ] && { cliclick c:"$CONTRAST_SWITCH"; sleep 3; }
}

black_pixels()
{
  front
  IFS=, read -r x y w h <<<"$(osascript -e "tell application \"System Events\" to tell (first process whose unix id is $pid) to return (position of window 1 as list) & (size of window 1 as list)" | tr -d ' ')"
  screencapture -x -R"$x","$((y + 60))","$w","$((h - 60))" "$sandbox/shot.png"
  sips -s format bmp "$sandbox/shot.png" --out "$sandbox/shot.bmp" >/dev/null
  python3 -I - "$sandbox/shot.bmp" <<'PY'
import struct, sys
d = open(sys.argv[1], 'rb').read()
off = struct.unpack('<I', d[10:14])[0]
bpp = struct.unpack('<H', d[28:30])[0] // 8
print(sum(1 for i in range(off, len(d) - bpp, bpp) if d[i] <= 3 and d[i + 1] <= 3 and d[i + 2] <= 3))
PY
}

dock_tile()
{
  IFS=, read -r x y w h <<<"$(osascript -e 'tell application "System Events" to tell process "Dock" to tell UI element "keycuts" of list 1 to return (position as list) & (size as list)' | tr -d ' ')"
  screencapture -x -R"$x","$y","$w","$h" "$sandbox/dock-$1.png"
  md5 -q "$sandbox/dock-$1.png"
}

say "sandbox: $sandbox"
swiftc -O "$here/contrast.swift" -o "$sandbox/contrast" 2>/dev/null
app="$("$here/new-test-bundle.sh" "$sandbox" 2>/dev/null | tail -1)"
[ -d "$app" ] || { say "could not build the bundle"; exit 2; }

# fixtures for the drops: a file, a folder, an app, a Finder alias, a .webloc
mkdir -p "$drop/Stuff"
echo hi > "$drop/note.txt"
cp -R /System/Applications/Calculator.app "$drop/"
python3 -I -c "import plistlib,sys; plistlib.dump({'URL':'https://example.com/'}, open(sys.argv[1],'wb'))" "$drop/site.webloc"
osascript -e "tell application \"Finder\" to make new alias file at (POSIX file \"$drop\" as alias) to (POSIX file \"/System/Applications/Utilities/Terminal.app\" as alias) with properties {name:\"Term alias\"}" >/dev/null
osascript -e "tell application \"Finder\"
  set w to make new Finder window to (POSIX file \"$drop\" as alias)
  set current view of w to list view
  set bounds of w to {20, 60, 660, 400}
end tell" >/dev/null

"$here/desktop.sh" clear "$state" drop keycuts.GUI
(HOME="$sandbox/home" KEYCUTS_OUTPUT_FOLDER="$sandbox/Shortcuts" nohup "$app/Contents/MacOS/keycuts.GUI" >"$sandbox/app.log" 2>&1 &)
sleep 6
pid="$(pgrep -f "$app/Contents/MacOS/keycuts.GUI" | head -1)"
[ -n "$pid" ] || { say "keycuts did not start"; exit 2; }
osascript -e "tell application \"System Events\" to tell (first process whose unix id is $pid) to set position of window 1 to {$WIN_X, $WIN_Y}" >/dev/null
front; osascript -e 'tell application "System Events" to keystroke "1" using command down'

for name in "${checks[@]}"; do
  say "--- $name"
  case "$name" in
    menus)
      app_menu="$(osascript -e "tell application \"System Events\" to tell (first process whose unix id is $pid) to return name of every menu item of menu 1 of menu bar item 2 of menu bar 1")"
      [[ "$app_menu" == *"About keycuts"* && "$app_menu" == *"Settings…"* ]] && ok "app menu: About keycuts, Settings…" || bad "app menu: $app_menu"
      tray="$(osascript -e "tell application \"System Events\" to tell (first process whose unix id is $pid) to click menu bar item 1 of menu bar 2
delay 1
tell application \"System Events\" to tell (first process whose unix id is $pid) to return name of every menu item of menu 1 of menu bar item 1 of menu bar 2")"
      osascript -e 'tell application "System Events" to key code 53' >/dev/null
      [[ "$tray" == *"Run…"* && "$tray" == *"Quit keycuts"* ]] && ok "menu bar icon: Run…, New keycut…, Manager, Settings…, Quit keycuts" || bad "menu bar icon: $tray"
      ;;
    drops)
      rows=("Stuff" "Calculator.app" "note.txt" "site.webloc" "Term alias")
      expected=("$drop/Stuff" "$drop/Calculator.app" "$drop/note.txt" "https://example.com/" "$drop/Term alias")
      for i in 0 1 2 3 4; do
        drag 310 $((155 + 20 * i))
        check "Finder drop: ${rows[$i]}" "$(wait_target "${expected[$i]}")" "${expected[$i]}"
      done
      read_app | grep -q "That target type isn’t supported." && ok "Finder drop: alias is reported as not supported" || bad "Finder drop: alias is not reported"
      osascript -e 'tell application "Safari"
        activate
        close every window
        make new document with properties {URL:"https://example.com/"}
        delay 3
        set bounds of front window to {20, 60, 700, 420}
      end tell' >/dev/null 2>&1
      sleep 2
      drag 318 86
      check "Safari drop: the address bar URL" "$(wait_target "https://example.com/")" "https://example.com/"
      osascript -e 'tell application "Safari" to quit' >/dev/null 2>&1
      ;;
    browse)
      front; osascript -e 'tell application "System Events" to keystroke "1" using command down'
      at="$(find_at AXButton Browse)"
      cliclick c:"$at"; sleep 1.5
      cliclick c:$((${at%,*} - 117)),$((${at#*,} + 52)); sleep 2
      osascript -e 'tell application "System Events" to keystroke "g" using {command down, shift down}'; sleep 1
      osascript -e "tell application \"System Events\" to keystroke \"$drop/note.txt\""; sleep 1
      osascript -e 'tell application "System Events" to key code 36'; sleep 1
      osascript -e 'tell application "System Events" to key code 36'; sleep 2
      check "Browse → File…: the chosen file" "$(wait_target "$drop/note.txt")" "$drop/note.txt"
      ;;
    contrast)
      front; osascript -e 'tell application "System Events" to keystroke "3" using command down'
      cliclick c:"$(find_at AXRadioButton "System theme")"; sleep 2
      off="$(black_pixels)"
      set_contrast true; sleep 4; on="$(black_pixels)"
      set_contrast false; sleep 4; again="$(black_pixels)"
      [ "$off" = 0 ] && [ "$on" -gt 1000 ] && [ "$again" = 0 ] && ok "Increase contrast: black pixels $off → $on → $again" || bad "Increase contrast: black pixels $off → $on → $again"
      ;;
    dock)
      front; osascript -e 'tell application "System Events" to keystroke "3" using command down'
      before="$(dock_tile before)"
      cliclick c:"$(find_at AXButton "Icon:*")"; sleep 1.5
      cliclick c:"$(find_at AXRow Cobalt)"; sleep 2
      osascript -e 'tell application "System Events" to key code 53'; sleep 2
      cobalt="$(dock_tile cobalt)"
      cliclick c:"$(find_at AXButton "Icon:*")"; sleep 1.5
      cliclick c:"$(find_at AXRow Onyx)"; sleep 2
      osascript -e 'tell application "System Events" to key code 53'; sleep 2
      onyx="$(dock_tile onyx)"
      [ "$before" != "$cobalt" ] && [ "$cobalt" != "$onyx" ] && ok "Dock icon follows the picker (changed at each pick)" || bad "Dock icon did not change: $before $cobalt $onyx"
      ;;
    *) say "unknown check: $name" ;;
  esac
done

say "---"
say "$pass passed, $fail failed (sandbox: $sandbox)"
[ "$fail" -eq 0 ]
