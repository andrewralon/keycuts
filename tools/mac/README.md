# Checking the real app on a Mac

`check-app.sh` drives the real keycuts app, the things the headless tests can't reach (a bundled app, Finder and Safari drags, the native
file panel, the Dock, system settings). It builds a throwaway bundle with its own identifier (`com.teamralon.keycuts.uitest`) whose HOME
and output folder are in a temp sandbox, so it never touches your settings, `~/Shortcuts` or an installed keycuts.

```sh
tools/mac/check-app.sh                      # everything
tools/mac/check-app.sh drops contrast       # some checks
```

| Check | What it does |
|---|---|
| `menus` | The app menu has About keycuts and Settings…; the menu bar icon has Run…, New keycut…, Manager, Settings…, Quit keycuts |
| `drops` | Real drags with `cliclick`: from a Finder window (file, folder, `.app`, `.webloc`, Finder alias → "not supported") and from Safari's address bar onto the creator; reads the Target field |
| `browse` | Browse → File…, drives the native panel with ⌘⇧G, reads the Target |
| `contrast` | Switches Increase contrast on in System Settings, counts the pure-black pixels of the window (high contrast text is exactly black), switches it off |
| `dock` | Picks two icons in Settings and compares the Dock tile's pixels after each pick |

**What it changes on your Mac while it runs:** hides your other apps and minimizes your other Finder windows (`desktop.sh` puts them back, also
on failure), opens a Finder window, Safari (needs the network, example.com) and System Settings, and flips Increase contrast for a few seconds.
A banner across the top of the screen says what it is doing and has **Pause** (it waits before its next click or keystroke) and **Stop** (it ends and puts everything back).
Don't use the machine while it runs; it moves the pointer.

**Needs:** Accessibility and Screen Recording for the app running it (System Settings → Privacy & Security), `cliclick` (`brew install cliclick`), `swift`.
The screen coordinates (Finder's list rows, the System Settings switch, the Browse menu) are for macOS 26 and the constants at the top of
`check-app.sh`; another macOS version may need them changed.

**What it can't check:** a real login (Open at login across a restart), VoiceOver, Narrator (Windows), and decisions such as the Safari.app symlink.

Accessibility notes from writing it: System Events only sees the app's controls reliably through the argv-style scripts here
(`read.applescript`, `find.applescript`); Safari's drag stalls unless the pointer waits after it starts; Finder's own accessibility tree is empty,
so its rows are found by position in list view.
