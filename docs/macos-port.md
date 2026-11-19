# keycuts on macOS: the plan

Written 2026-11-19 against `develop` at `3ebd431` (version 1.6.2). This is the plan for Phase 4 of `gameplan.md`: one Avalonia app for Windows and macOS, and keycuts that run on a Mac the way they run from Win+R. Work through the phases in order. Each step says what "done" means and where it has to be checked (this Windows machine, the Mac, or CI).

The rules in `CLAUDE.md` and `gameplan.md` still hold: **never move two variables in one commit**, characterization snapshots must change only in a reviewed diff, the build has 0 warnings, and tests never touch the real folder, registry, PATH, network or Trash.

## Decisions (2026-11-19, the owner)

| Question | Decision | Why |
|---|---|---|
| What replaces Win+R on a Mac | **A Run box built into keycuts** (a global hotkey opens a small box: type the name, Enter), plus **Terminal through `PATH`**. | It's the only option that works like Win+R: the bare name, no prefix, no Terminal window, no third-party app. Spotlight won't run scripts (`.command` files open a Terminal window). |
| Spotlight | **Investigate later**, after the Avalonia rewrite: one Shortcuts shortcut with a quick key (`k gmail` in ⌘-Space, macOS 26+). | Cheap if it works, but unconfirmed: it needs the typed text to reach a "Run Shell Script" step, and Spotlight-launched shortcuts have reported failures. |
| App bundle per keycut, Raycast metadata | **Parked.** | A bundle per keycut doubles the files to keep in sync, needs a native launcher on Apple Silicon (a script executable triggers a Rosetta prompt) and fills Tahoe's Apps view. Raycast needs a third-party app and per-file metadata. Either can come back as an option. |
| Script files | **No extension, mode 755, in `~/Shortcuts`.** | `gmail` must be typeable in Terminal. `~/Shortcuts` matches the Windows default (`%USERPROFILE%\Shortcuts`). `~/.local/bin` isn't on the default macOS `PATH`, and pipx and others put their own scripts there, which the Manager would then list. |
| UI framework | **Avalonia for everything.** WPF is deleted. The Avalonia app must cover all of the WPF app's behavior. Risk accepted. | One framework, one set of views and tests. |
| Mac and Apple account | The owner has a Mac and a paid Apple Developer account. | Signing, notarization and every "on the Mac" check below are possible. |

## What changed since the macOS issues were written

The macOS issues (#84, #85, #98, #100–#104, #106) date from 2026-09-22, before the redesign. Since then:

- **#90 (platform seam) was closed** on 2026-11-08: no interfaces ahead of a second implementation. This plan adds each seam when the macOS implementation needs it, with the Windows behavior moved behind it unchanged.
- **#91 settled the file header**: `REM <type>`/`<target>` lines, not the `keycuts-meta` JSON that #100 proposes.
- **#95 (settings.json) was closed**: the MSI writes the HKCU values. Windows keeps the registry; macOS needs its own store (Phase 4).
- **#94 is done**: PATH goes through `IPathUpdater`/`UserPathUpdater`, compared by whole entries.
- **#97 and #248 are done**: `keycuts paste`, and the `keycuts.cmd`/`keypaste.cmd` shims live in the keycuts folder and find `keycuts.exe` through HKCU `InstallFolder`.
- **Batmanager is gone**: one window with Creator, Manager and Settings (KC-01–KC-21, #218).
- **No Shell32 `COMReference`**: `.lnk` files are read through late-bound `WScript.Shell`, so #84's reason no longer applies.
- **Things no macOS issue covers yet**: run counting (`UsageLog.BatLines`, #187), name-conflict checks (`NameConflicts`), `PathVariables` (#31), Recycle Bin delete with Undo, migrations (#196/#207), the update check, app icons, `ScriptVariables` (#212), and the CLI verbs `list`, `rm`, `rename`, `open`.

## Target shape

### Projects

Keep today's projects and names. #85's twelve-project layout and DI container are dropped (CLAUDE.md already calls `src/Keycuts.Core` future work).

| Project | Today | After |
|---|---|---|
| `keycuts.Common` | `net10.0-windows` | `net10.0`. Windows-only classes marked `[SupportedOSPlatform("windows")]`, macOS-only ones `[SupportedOSPlatform("macos")]`, so CA1416 checks every call site. |
| `keycuts.CLI` (`keycuts.exe`) | `net10.0-windows` | `net10.0`. Assembly stays `keycuts`. |
| `keycuts.GUI` (WPF) | `net10.0-windows10.0.19041.0` | Deleted at the end of Phase 3. |
| `keycuts.App` (Avalonia) | — | `net10.0`, Avalonia 12.x. **Assembly renamed to `keycuts.GUI` at cutover**, so existing keycuts pointing at `keycuts.GUI.exe` (the welcome panel's `kc`), the context-menu verbs, Start menu shortcuts and the MSI's file names keep working. |
| `keycuts.Tests` | `net10.0-windows10.0.19041.0`, WPF | `net10.0`, runs on Windows, macOS and Linux. Platform-only tests use `[WindowsFact]`/`[MacFact]` (attributes that set `Skip` on other platforms). WPF tests are rewritten on Avalonia.Headless. |

Platform choice happens once at startup: a small `KeycutPlatform` (Windows or macOS), picked with `OperatingSystem.IsMacOS()`, which the existing `CreateDefault()` factories read. No container.

### Seams (each gets its second implementation in this plan)

| Concern | Windows (today, unchanged) | macOS (new) | Seam |
|---|---|---|---|
| Script emit and parse | `Runner.BuildShortcutLines`, `ShortcutFile` (`.bat`, `REM`) | `#!/bin/sh` script, `#` header lines, no extension, mode 755 | New `IScriptFormat` (Phase 2) |
| Settings | `RegistryStuff` (HKCU `Software\TeamRalon\keycuts`) | `~/Library/Application Support/keycuts/settings.json`, same value names | New settings backend under `RegistryStuff`'s accessors (Phase 2) |
| Open, reveal, edit, run | `IKeycutActions` (GUI), shell execute (CLI `open`) | `open`, `open -R`, `open -t`, `/bin/sh <file>` (no Terminal window) | New `IDesktop` used by both (Phase 2) |
| Delete with Undo | `RecycleBinRecycler` | Trash via `NSFileManager trashItemAtURL` (Undo already restores from kept bytes) | `IFileRecycler` (exists) |
| PATH | `UserPathUpdater` (HKCU `Environment`) | Detect `PATH`/`/etc/paths`/`/etc/paths.d`. On consent, a marked block in `~/.zprofile` (bash: `~/.bash_profile`; fish/nushell: instructions only) | `IPathUpdater` (exists) |
| Name warnings | `NameConflicts` (Win+R order, PATHEXT, App Paths) | Executables of that name in `PATH` folders, case-insensitive (APFS default) | `NameConflictEnvironment` (exists) |
| Variables in paths | `PathVariables` (`%USERPROFILE%` …) | `$HOME` only, written inside double quotes | `PathVariables` gets a macOS table |
| Run counting | `UsageLog.BatLines`, `%LOCALAPPDATA%\keycuts\usage` | sh lines, `~/Library/Application Support/keycuts/usage` | `UsageLog` lines per format |
| Command shims | `keycuts.cmd`, `keypaste.cmd`, `InstallFolder` from HKCU | `keycuts`, `keypaste` (sh, LF, 755). They read the install folder from `~/Library/Application Support/keycuts/install-folder` (one line, written by the app at start), so their bytes are the same on every Mac | `CommandShims` per platform |
| Right-click | `ContextMenuRegistrar` (HKCU `Software\Classes`) | Finder Quick Action copied to `~/Library/Services`, then `pbs -flush` (#102) | `IClassesStore`'s owner becomes a per-platform "right-click" service |
| Clipboard (CLI) | `WindowsClipboard` (Win32) | `pbcopy` | Small `IClipboard` in the CLI |
| Start the GUI from the CLI | `GuiLauncher` | `open -a keycuts` | `GuiLauncher` per platform |
| Windows-only, no macOS twin | CLSID keycuts, hosts-file type, `.lnk`/`.url` resolution, `ShortcutIcons` (.lnk icons), `KeycutMigration` (old .bat files), `ShellIdList` drops | — | Marked Windows-only; not offered on macOS |

### A keycut on macOS

`~/Shortcuts/gmail`, mode 755, UTF-8, LF:

```sh
#!/bin/sh
# keycuts 2.0.0
# <shortcut>/Users/me/Shortcuts/gmail</shortcut>
# <type>url</type>
# <target>https://mail.google.com</target>
# <description>Inbox</description>
open 'https://mail.google.com'
s=$?
u="$HOME/Library/Application Support/keycuts/usage"; { mkdir -p "$u" && echo 1 >>"$u/${0##*/}.runs"; } 2>/dev/null
exit $s
```

- Header lines mirror the `.bat` header (`REM ` becomes `# `), so the REM-line reader, `CanEditInForm`, rename (rewrites only `<shortcut>`) and description edit (rewrites only `<description>`) are shared, with a per-format comment prefix.
- Launch lines: Url, File and Folder are `open '<target>'`. File with an app is `open -a '<app>' '<file>'`. A `.app` target is a File (`open` launches it). Command is `/bin/sh -c '<command>'` (parsed; not produced by create, as on Windows).
- Quoting: single quotes, with `'` written as `'\''`. With variables on, a target under the home folder is written `"$HOME/…"`, with `\ " $` and backtick escaped.
- The run-count lines keep the launch's exit status, fail silently, and use the script's own name (`${0##*/}`), so a rename in Finder keeps counting.
- The Manager lists a file only if its first lines are `#!/bin/sh` and `# keycuts …`, never the shims. Any other script in the folder is ignored, not deleted.
- Folders and files the app uses: keycuts in `~/Shortcuts` (setting `OutputFolder`). Settings, usage, the install-folder pointer and backups go in `~/Library/Application Support/keycuts/`. Check in Phase 0 that `Environment.SpecialFolder.LocalApplicationData` returns `~/Library/Application Support` on macOS (it does from .NET 8); if so, `UsageLog`'s path code works unchanged.

### The Run box (macOS)

- keycuts stays running with an icon in the menu bar (Avalonia `TrayIcon` with a `NativeMenu`: Run…, New keycut, Manager, Settings, Quit). No Dock icon while no window is open (`MacOSPlatformOptions.ShowInDock`, switched at runtime). Closing the window doesn't quit the app.
- A global hotkey opens the box: Carbon `RegisterEventHotKey` through P/Invoke. It needs no Accessibility permission. Since macOS 15 the hotkey must include ⌘ or ⌃. Proposed default **⌃⌥Space** (⌃Space and ⌥⌘Space are taken by macOS). It can be changed in Settings, and Settings says when the hotkey can't be registered.
- The box: a small borderless window centered on the active screen with one text box. Suggestions come from the keycut list: exact name first, then prefix matches, then other matches by run count, each row showing name, type badge and target. **Enter** runs the keycut (`/bin/sh <file>`, detached, so its own lines count the run). **Tab** completes. **Esc** hides. After running, keycuts hides itself so focus goes back to the previous app. An unknown name offers "Create gmail…", which opens the Creator with the name filled in.
- Start at login: `SMAppService.mainApp` (macOS 13+), a Settings toggle, offered in the welcome panel.
- Windows has no Run box (Win+R already does this).

## Phases

Who checks each step: **W** = this Windows machine, **M** = the Mac, **CI** = GitHub Actions.

### Phase 0: prototypes on the Mac (before product code)

Throwaway projects outside the repo. Each answers a question the later phases depend on. Record the answers in this file's "Phase 0 results" section.

1. **Signing and notarization** (#106's own advice). A hello-world Avalonia 12 app, `dotnet publish -r osx-arm64` and `-r osx-x64`, self-contained. Build the `.app` (Info.plist, `.icns`). Sign every Mach-O from the inside out with `--options runtime` and the JIT entitlements (`com.apple.security.cs.allow-jit`, `allow-unsigned-executable-memory`). Then `pkgbuild` → `productbuild` → `productsign` → `notarytool submit --wait` → `stapler staple`. Done when a browser-downloaded `.pkg` installs with no Gatekeeper warning (`spctl -a -vvv`) on Apple Silicon, and on Intel if one is available. **M**
2. **Menu bar, hotkey, focus.** The same app with `ShowInDock = false`, a `TrayIcon`, a Carbon hotkey (⌃⌥Space) that shows a window and focuses its text box, then hides and returns focus to the previous app. Test it signed and notarized on macOS 26, since a report says some modifier hotkeys fail for an ad-hoc-signed process. Done when the hotkey works from every app tried (Safari, Terminal, Finder, a full-screen app). **M**
3. **Running scripts.** `/bin/sh ~/Shortcuts/x` started from the app opens a URL, a folder, a file and an app with no Terminal window. `open` on an extensionless executable opens Terminal, so confirm that's never used to run. **M**
4. **Files sent to a running app.** With the app already running, `open -a <app> <path>` (what the Quick Action will run) delivers the path. Find the Avalonia 12 API that receives it (activation or file-open event). Done when a running app receives the path. **M**
5. **Start at login** through `SMAppService.mainApp` via the Objective-C runtime (P/Invoke `objc_msgSend`). Done when the toggle shows under System Settings → General → Login Items and survives a reboot. **M**
6. **Tests on macOS.** An xUnit project with `Avalonia.Headless.XUnit` 12.x runs on a `macos-latest` runner. Find out which xUnit major it needs; if it's v3, the migration from 2.9.3 becomes Phase 2 step 1. **CI**
7. **Avalonia gaps for parity**, each answered yes/no with a workaround: a DataGrid with star columns and draggable dividers (package and license for 12.x); no maximize (`CanMaximize`) while keeping Windows edge snapping; reading the "Shell IDList Array" drop format (Recycle Bin drags, #9); live-region announcements for screen readers; a client area extended under the title bar on both platforms (own caption buttons on Windows, native traffic lights on macOS). **W** + **M**

### Phase 1: bring the GitHub issues up to date

Edit issues to match this plan before work starts (see "GitHub issue changes" below). Close the obsolete ones with a comment linking here. **W**

### Phase 2: cross-platform foundation (Windows behavior unchanged)

Every step keeps all characterization snapshots and Windows tests green with no `.verified.txt` change. Each step is its own PR.

1. **Tests build without WPF types in Common's tests** (and the xUnit v3 migration if Phase 0.6 needs it). Add `[WindowsFact]`/`[MacFact]`. **W** + **CI**
2. **Retarget `keycuts.Common` and `keycuts.CLI` to `net10.0`.** Mark Windows-only types (`RegistryStuff`, `UserPathUpdater`, `ContextMenuRegistrar`, `CurrentUserClassesStore`, `RecycleBinRecycler`, `ShortcutIcons`, `WindowsClipboard`, the `WScript.Shell` code in `Shortcut`) `[SupportedOSPlatform("windows")]`. Guard their callers with `OperatingSystem.IsWindows()`. 0 warnings with `--no-incremental`. **W**
3. **macOS CI job**: build plus the tests that can run without the GUI, on `macos-latest`. Make it required on `develop` once it's green. **CI**
4. **Settings backend seam.** `RegistryStuff`'s typed accessors call a backend (string, DWORD, multi-string). Only the registry backend exists in this step. No value name or semantics change. **W**
5. **Script format seam.** `IScriptFormat` (file extension, comment prefix, emit lines, parse, executable bit). `BatFormat` wraps `Runner.BuildShortcutLines` and `ShortcutFile` unchanged. Replace the three `.bat` literals (`Shortcut.Extension`, the `Shortcut` constructor, `ShortcutFile.Extension`) with the format's extension. The snapshots prove it's faithful. **W**
6. **Desktop seam.** `IDesktop` (open target, run keycut, reveal file, edit file), used by the CLI's `open` and by `IKeycutActions`. **W**

### Phase 3: the Avalonia app, on Windows, at full WPF parity

Built beside WPF in a new `keycuts.App` project (assembly `keycuts.App` until cutover). `develop` keeps shipping WPF until the parity checklist below is complete. UI changes follow `CLAUDE.md`: update `docs/styleguide.md` first, and get mockups approved for anything that looks different.

1. **Style guide for Avalonia**: map every token in `docs/styleguide.md` to Avalonia resources. Fonts become a platform token (Segoe UI on Windows, the system font on macOS). `StyleGuideTests` is ported to read `.axaml`. **W**
2. **Themes and tokens**: `Tokens`, `Light`, `Dark`, `HighContrast`, `Icons` and `Controls` as Avalonia styles; `ThemeManager` follows the OS theme and high contrast. **W**
3. **Custom controls**: `FolderTabShape`, `RailTabControl`, `TypeBadge`, `Icon`, `MenuButton`, `MenuPlacement`, `SectionLabel`, `SortHeaderButton`, `StarCell`, `TornCorner`, `HelperText`, `BindingProxy` (or its Avalonia replacement). **W**
4. **View models**: move the UI-free logic of the 15 `ViewModels/*` files into the new project unchanged. Isolate the `System.Windows` uses (6 files) behind small adapters (dispatcher, clipboard, dialogs). Keep the house `Mvvm` (`ObservableObject`, `RelayCommand`). **W**
5. **Shell and window**: one window, `RailView` with own caption buttons, Ctrl+1/2/3, never maximized, two sizes (`MainWindow.ApplyMode`: Creator `MainWidth` × content height without resize, Manager and Settings 768 × 470 shared and resizable, clamped to the work area, top-left corner kept), Escape handling. **W**
6. **Creator and the Manager's create row**: drop (files, folders, URLs, shell items), paste, Ctrl+V, Browse, resolve while typing, name check, Shift flipping variables, the confirm dialog, "Saved as", "Created" status. **W**
7. **Manager**: table, search, the count pill, sort, keys (Up/Down, Page, Home/End, Enter, F2, Shift+F2, Delete, Ctrl+C/E/F/Z, Ctrl+Shift+E/O, Shift+F10, Menu key), in-place rename and description edit, the row menu, delete with Undo, the Edit form, resolved targets (#212), target check, Runs and Last run, column hiding at 1040/780/720. The DataGrid traps in `docs/internals.md` are WPF-specific: rewrite that section for Avalonia. **W**
8. **Settings, welcome panel, update notice, app icon picker, dialogs** (`ConfirmWindow`, `EditWindow`), `CrashReporter`, the context-menu argument (`args[0]`), `ShortcutIcons`. **W**
9. **Accessibility**: tab list semantics, automation names, `HelperText`/`LiveRegion` announcements, keyboard-only paths; checked with Narrator. **W**
10. **Tests**: port every WPF test class (`AccessibilityTests.*`, `TabbedWindowTests`, `ThemeTests`, `TornCornerTests`, `TypeBadgeTests`, `IconTests`, `MenuPlacementTests`, `ConfirmDialogTests`, `EditDialogTests`, `StyleGuideTests`, `ScreenshotTests`, …) to Avalonia.Headless. Replace `FakeKeyboardDevice`/`AccessibilityTests.Press` with headless key input. Delete `WpfCollection`. **W** + **CI**
11. **Screenshots**: `tools/New-Screenshots.ps1` runs the ported `ScreenshotTests`. Regenerate and commit the images and GIFs that visibly changed. **W**
12. **Cutover** (one PR, after the parity checklist passes): rename `keycuts.App`'s assembly to `keycuts.GUI`, delete the WPF project, `UseWPF` and `WpfCollection`, and update `installer/Package.wxs` for the new files (Avalonia's native libraries). Update `CLAUDE.md`'s architecture section. Then build both MSIs, install over 1.6.2, run the manual checks (context menu as non-admin, Recycle Bin drag, upgrade keeps settings), and release (proposed version 2.0.0). **W**

### Phase 4: keycuts core on macOS (CLI first)

Milestone: from a dev build on the Mac, `keycuts create -t https://mail.google.com -s gmail` then `gmail` in Terminal opens Gmail, and `list`, `rm`, `rename`, `open` and `paste` work.

1. **`ShFormat`**: emit and parse as above, with mode 755 (`File.SetUnixFileMode`). A new corpus in `tests/corpus/macos/` with its own emitter, parser and round-trip snapshots, including nasty quoting (`'`, `"`, `$`, backtick, spaces, unicode, newlines in descriptions). **W** (tests run everywhere) + **M**
2. **Settings JSON backend**: atomic writes (temp file + move), tolerant of unknown values, the same value names as the registry. **W** + **M**
3. **Default folder** `~/Shortcuts`; `KEYCUTS_OUTPUT_FOLDER` works as on Windows in Debug builds. **M**
4. **Target classification on macOS** (`TargetResolver`): files, folders, `.app` bundles, URLs, `.webloc` (plist URL), symlinks. Finder aliases are reported as not supported. The target check (Missing / Unknown) uses Unix paths. **W** + **M**
5. **Run counting** lines for sh, and the usage folder. **M**
6. **`$HOME` substitution** (`PathVariables`' macOS table) and its `"…"` quoting. **W** + **M**
7. **Name warnings**: PATH executables, shell builtins as a fixed list, case-insensitive. `keycuts` and `keypaste` stay refused. **W** + **M**
8. **Platform services**: Trash recycler, `IDesktop` for macOS, `pbcopy` for `paste`, sh shims and the install-folder pointer, `GuiLauncher` (`open -a`). **M**
9. **PATH** (#101): detect, explain, and on consent add or remove the marked block in `~/.zprofile` (bash: `~/.bash_profile`; fish/nushell: instructions only). Removal leaves the rest of the file byte-identical. Add a CLI verb `keycuts path [status|add|remove]`. **W** (tests with a temp `HOME`) + **M**

### Phase 5: the Avalonia app on macOS

Mockups first: macOS window chrome (traffic lights plus the folder tabs), ⌘ shortcuts, menu bar menu, Settings additions.

1. **Window**: traffic lights in a client area extended under the title bar, folder tabs beside them, no zoom or full screen (the "never maximized" rule), the same two sizes. **M**
2. **Keys**: ⌘ where Windows uses Ctrl (⌘1/2/3, ⌘C/E/F/Z, ⌘, for Settings, ⌘Q), F2 kept, Return for rename as in Finder decided in the mockup. The app menu holds About, Settings, Quit. **M**
3. **Drag from Finder and browsers, file dialogs.** **M**
4. **Settings on macOS**: the right-click toggle drives the Quick Action, the PATH status and button, theme following the system (including Increase Contrast), the icon choice (Dock icon while running; the bundle's own icon stays fixed, since changing it would break the signature). **M**
5. **Welcome panel text for macOS**: how to run a keycut (Run box hotkey, Terminal), with no mention of Spotlight. Bubbles: `g`, `dl` (`~/Downloads`), `kc` (the app). **M**
6. **Screenshots for macOS** (`ScreenshotTests` with a platform suffix; README shows both). **M**

### Phase 6: the Run box

1. **Menu bar presence and app lifetime** (resident, Dock icon only while a window is open). **M**
2. **Hotkey service** (Carbon), the Settings picker, and a message when the hotkey can't be registered. **M**
3. **Matching and ranking** in Common (pure, unit-tested on every platform): exact, prefix, contains (name, description, target), ties broken by runs and then last run. **W**
4. **The box**: headless tests for keys (Enter, Tab, Esc, Up/Down), runs through `IDesktop`, focus return, "Create …" fallback. **W** + **M**
5. **Start at login** toggle; the welcome panel offers it. **M**

### Phase 7: Finder Quick Action (#102)

The `.workflow` committed under `installer/macos/` as source. It runs `open -a keycuts "$@"`, which the running app receives (Phase 0.4) and opens the Creator with the target filled. Installed and removed by the Settings toggle, by `keycuts install-shell-integration` / `uninstall-shell-integration`, and by the `.pkg` postinstall for the console user. Tests check the files against a temp `HOME` with `pbs` stubbed. **W** + **M**

### Phase 8: packaging and releases (#106)

1. `tools/Build-MacPackage.sh`: publish `osx-arm64` and `osx-x64`, build `keycuts.app` (the GUI and `keycuts` CLI executables in `Contents/MacOS`, the Quick Action in `Contents/Resources`), sign inside-out, `pkgbuild`/`productbuild`/`productsign`, notarize, staple. Output goes to `artifacts/installer/osx-<arch>/`. Two `.pkg` files, matching the two MSIs. **M**
2. postinstall: create `~/Shortcuts` for the console user, write the shims and the install-folder pointer, install the Quick Action, `pbs -flush`. **It doesn't touch PATH**: the app's welcome panel offers "Add to PATH" as it does on Windows. **M**
3. `.icns` from `assets/icons` (`tools/New-AppIcons.ps1` writes the PNG set; `iconutil` runs in the macOS build). **M**
4. `release.yml`: a macOS job builds, signs and notarizes both `.pkg` files and attaches them to the same `v<Version>` release, and `SHA256SUMS.txt` covers all six files. Secrets as listed in #106, with jobs guarded against fork PRs. **CI**
5. Homebrew cask in `andrewralon/homebrew-tap`; the release opens a PR there. The cask's `binary` stanza links `keycuts`. **CI**
6. The update check works unchanged (it links to the release page). Check the notice on macOS. **M**

### Phase 9: docs

README: install and use on macOS, a platform matrix (feature × Windows × macOS), the known gaps (no Spotlight, no right-click on a folder's background, no CLSID or hosts-file keycuts). `docs/internals.md`: macOS sections (script format, Run box, settings file, PATH block, Quick Action). `CLAUDE.md`: macOS commands and gotchas. Close #104. **W**

### Phase 10: Spotlight, investigated (option B)

One shortcut, "Run keycut", with "Show in Spotlight" and "Receive input from Spotlight", running `"$HOME/Shortcuts/$1"`, with quick key `k`. Find out whether the input reaches the shell step, how reliably Spotlight runs it, and whether keycuts can install it (a signed `.shortcut` file the user imports once). Report back and decide. **M**

## GitHub issue changes (Phase 1)

| Issue | Action |
|---|---|
| #84 managed `.lnk` parser | **Close.** The `COMReference` is gone (late-bound `WScript.Shell`), so `.lnk` handling stays Windows-only and `net10.0` needs no parser. |
| #85 `src/` restructure | **Close**, replaced by new issue N1. |
| #92 `DestinationKind` | Leave deferred. macOS writes the existing type names (`url`, `file`, `folder`); nothing here needs the rename. |
| #96 System.CommandLine | Leave optional. `keycuts path` and `install-shell-integration` fit CommandLineParser verbs. |
| #98 Avalonia | **Rewrite**: full WPF parity on Windows first (Phase 3), the parity checklist below as acceptance criteria, `keycuts.App` beside WPF until cutover, assembly `keycuts.GUI` after it. Remove Batmanager, `MgrWindow`, `BatFormLogic`, `Copy(DataGrid)`, CommunityToolkit and "ship early on Windows" text. Blocked by N1–N3, not #90. |
| #100 macOS script format | **Rewrite** to "A keycut on macOS" above: no `keycuts-meta`, `# <type>`/`<target>` lines, `~/Shortcuts`, run counting, `$HOME`, quoting, 755. Blocked by N3, not #91/#92. Name: "macOS keycuts: script format, targets, run counts". |
| #101 macOS PATH | **Edit**: build on `IPathUpdater`, remove "`~/.local/bin` is already on PATH" and the `/etc/paths.d` installer write, keep the `~/.zprofile` block and detection. Not blocked by #94 (done). |
| #102 Finder Quick Action | **Edit**: `open -a keycuts "$@"` received by the running app, not `--args --destination`, `installer/macos/` not `packaging/macos/`, and the Settings toggle. |
| #103 NSServices | **Close.** The Quick Action covers it. Reopen if the Services menu is wanted for itself. |
| #104 parity gaps | **Edit**: the Run box and Terminal are the launchers. Drop the `.app` toggle and `IShortcutStore` items. Keep the README matrix and first-run text. |
| #106 macOS packaging | **Edit**: two `.pkg` files, no `/etc/paths.d`, shims and the install-folder pointer in postinstall, a `release.yml` macOS job, `tools/Build-MacPackage.sh`. |

New issues (label `area:macos` or `type:port`, milestone M4 macOS unless noted):

- **N1. Cross-platform build and tests**: `net10.0` for Common, CLI and Tests; `[WindowsFact]`/`[MacFact]`; a macOS CI job (Phase 2.1–2.3, milestone M3 Port).
- **N2. Settings backend**: the registry backend now, JSON on macOS later (Phase 2.4 + 4.2).
- **N3. Script format seam**: `IScriptFormat`, `BatFormat` (Phase 2.5).
- **N4. Desktop seam**: `IDesktop` (Phase 2.6).
- **N5. macOS platform services**: Trash, open/reveal/edit/run, `pbcopy`, shims, name warnings, `$HOME`, `GuiLauncher` (Phase 4.3–4.8).
- **N6. The Run box**: menu bar, hotkey, box, start at login (Phase 6).
- **N7. The Avalonia app on macOS**: window, keys, drag, Settings, welcome, screenshots (Phase 5).
- **N8. Investigate a Spotlight quick key for keycuts** (Phase 10).
- **N9. Phase 0 prototypes**: one checklist issue, closed with the results.

## Avalonia parity checklist (acceptance for #98)

Behavior is what `docs/internals.md` describes; each line needs a test or a recorded manual check.

- **Window**: three screens via folder tabs with tab-list semantics; own caption buttons; Ctrl+1/2/3 (not while tabs are disabled); never maximized but edge-snapping works; two sizes and the shared large size; top-left kept; reload numbering (only the latest load sets the count).
- **Creator**: drop of files, folders, URLs, `.lnk`/`.url`, Recycle Bin and other shell items; paste and Ctrl+V; Browse; resolve while typing; name warnings and the replace confirmation; Shift flips variables and "Saved as" follows; `NameStatusText`; fields kept after create; description always shown; the context-menu argument fills the target.
- **Welcome**: shown on first run or empty folder; three bubbles; Got it; "Show Welcome" in Settings; Add to PATH after a create.
- **Manager**: every key in the internals list; search with Delete/Home/End staying in the box; the count pill and its announcement; sort orders; Name/Description/Target resizable, Type/Modified/Runs/Last run fixed, hidden at 1040/780/720; "Counting since" notes; in-place rename and description (250 ms rule, one edit at a time); the Edit form and `CanEditInForm`; delete with Undo for 8 s; row menu by right-click, Shift+F10 and the Menu key; Open file location; resolved targets and their tooltip; Missing/Warning labels; status 3 s and errors 6 s; the create row's Enter behavior.
- **Settings**: every option saves at once; folder commit on Enter, focus loss or picker; Escape reverts; restore defaults with confirmation; right-click toggle shows actual state; theme; app icon and transparent background (picker disables tabs); update check toggle; version row; two columns down to 720 × 470.
- **Startup**: rename `keycuts`/`keypaste` keycuts to `*_previous`; migration once per folder with its notice; add run counting once per folder; shims kept; icons repointed; update check once a day with the footer notice.
- **Accessibility**: automation names and help text, announcements, focus order, high contrast, contrast ratios from the style guide.
- **Tests**: every behavior above that had a WPF test has a headless one; screenshots and GIFs regenerate.

## Risks and open questions

- **Avalonia DataGrid**: the Manager leans on DataGrid behavior (star columns, grippers, row menus). If Avalonia's DataGrid can't match, build the table from a `ListBox` with a custom header (Phase 0.7 decides).
- **Hotkey reliability on macOS 26** (Phase 0.2). Fallback: Shortcuts' "Use as Quick Action" with a keyboard shortcut opening the app's Run box through a URL scheme.
- **Resident memory**: an Avalonia app idling in the menu bar takes tens of MB. Measure in Phase 0.2.
- **xUnit major version** for Avalonia.Headless 12 (Phase 0.6).
- **Universal binary vs two `.pkg` files**: two for now. A universal build needs every native library merged with `lipo`.
- **Default hotkey**: ⌃⌥Space was proposed, but it is a macOS default (Phase 0 results, correction 2); choose another in the Phase 5 mockup.
- **Version numbers**: proposed 2.0.0 for the Avalonia cutover on Windows; the first macOS release is whatever version is current when Phase 8 ships.

## Parked

- One `.app` bundle per keycut so Spotlight finds it by name (option C).
- Raycast script-command metadata in the header (option D).
- NSServices (#103).
- A Run box on Windows.

## Phase 0 results

Run 2026-11-19 on macOS 26.6 (arm64) with .NET SDK 10.0.401 and Avalonia 12.1.6. The prototype is in `~/Repos/keycuts-phase0` on the Mac (outside the repo): `ProtoApp` (menu bar app, hotkey, Run box, login item, parity window) and `make-app.sh` (publish, bundle, sign, pkg, notarize). The 0.6 probe is `tests/phase0` plus `.github/workflows/macos-phase0.yml`.

| Step | Result |
|---|---|
| 0.1 Signing | **Signed; notarization pending at Apple.** Developer ID Application and Developer ID Installer certificates (G2 Sub-CA, team 8RG5QJVST3) were created 2026-11-19. Only the account holder can create them, and both a team App Store Connect API key (403, "can only be performed by the Account Holder") and Xcode's Manage Certificates refused, even signed in as the account holder; the developer website accepted a CSR made with `openssl`. The keys were imported with `security import -T /usr/bin/codesign -T /usr/bin/productsign`, and the first `codesign` still asked for the login keychain password once ("Always Allow"). The `.app` passes `codesign --verify --deep --strict` with the hardened runtime and Team ID; the `.pkg` passes `pkgutil --check-signature` (Developer ID Installer, trusted timestamp). `notarytool` authenticates with the API key through a keychain profile (`store-credentials`). The first submission was still "In Progress" after 20+ minutes; stapling and `spctl` follow when it finishes. What the plan got wrong: (1) sign **every file** in `Contents/MacOS`, not only Mach-O files, because `codesign` treats the managed `.dll`s there as nested code; (2) leave the main executable to the bundle signature, because signing it checks every sibling file, including ones not yet signed; (3) an **ad-hoc** signature with `--options runtime` can't start (library validation refuses `libhostfxr.dylib`: "different Team IDs"), so local ad-hoc builds skip the hardened runtime; (4) check a `.pkg` with `spctl -a -vvv -t install`. The self-contained arm64 `.app` is 111 MB with 221 files. |
| 0.2 Menu bar, hotkey, focus | **Works**, ad-hoc and Developer ID signed. A real ⌃⌥Space over a full-screen app (VS Code) opened the box. `RegisterEventHotKey` with ⌃⌥Space returned 0. Synthetic keypresses (System Events) over Finder, Safari and Terminal opened the box with its text box focused. Enter ran the keycut with `/bin/sh`. Esc and Enter call `[NSApp hide:]`, and the previous app was frontmost again each time. `setActivationPolicy:` (0 = Dock icon, 1 = none) switches the Dock icon at runtime. **Resident memory: 91 MB idle in the menu bar, 100–164 MB with a window open**, not "tens of MB". **⌃⌥Space is a macOS default** ("Select next source in Input menu", symbolic hotkey 61). It's turned off on this Mac, but it's on by default where more than one input source is set up. Pick another default (see "Plan corrections"). Menu bar icons need `MacOSProperties.IsTemplateIcon` so they follow the menu bar color. |
| 0.3 Running scripts | **Works.** `/bin/sh <file>` started from the GUI app ran a URL, folder, file, `.app` and a failing keycut with no Terminal window (window count 4 → 4). Each wrote its run count, and the failing one exits 1, so `s=$?; …; exit $s` keeps the status. `open <extensionless executable>` does open a Terminal window (4 → 5): never use it to run a keycut. |
| 0.4 Files sent to the app | **Works.** `open -a <app> <paths>` reaches `IActivatableLifetime.Activated` as `FileActivatedEventArgs`, several paths in one event, on a running app and on a cold start (not in `argv`), so there is one code path. Folders arrive with a trailing `/`. The prototype's `Info.plist` declares `CFBundleDocumentTypes` for `public.item` and `public.folder` with `LSHandlerRank None`. |
| 0.5 Start at login | **Works, and survives a reboot.** With the Developer ID build registered, the app started by itself after a restart (parent `launchd`, no arguments, status still 1). `SMAppService.mainAppService` through `objc_msgSend`: status 3 (`notFound`) before the first registration, not 0, then `registerAndReturnError:` returns YES and status 1 (`enabled`), even for an ad-hoc-signed app outside `/Applications`. The Settings toggle must read `notFound` as off. |
| 0.6 Tests on macOS | **Works on CI.** `Avalonia.Headless.XUnit` 12.1.6 needs **xUnit v3** (`xunit.v3.extensibility.core` ≥ 3.2.2), so the migration is Phase 2 step 1. Use **`xunit.v3` 3.2.x with `xunit.runner.visualstudio` 3.1.5**: it keeps the VSTest runner, TRX and `GitHubActionsTestLogger`, with the current `Microsoft.NET.Test.Sdk` 17.14.1. `xunit.v3` 4.x runs only on Microsoft.Testing.Platform, and the .NET 10 SDK's `dotnet test` refuses it in VSTest mode ("Testing with VSTest target is no longer supported"). Moving to MTP is a separate decision (global.json `test.runner`, new loggers). The probe (typing, Enter, a ⌘ modifier, DataGrid star columns) passed on `macos-latest` (macOS 26 arm64), `ubuntu-latest` and `windows-latest`. |
| 0.7 Avalonia gaps (macOS half) | **DataGrid**: `Avalonia.Controls.DataGrid` 12.1.2 is MIT. Star columns share the width 1:2:3 with a fixed 70/80 px column, headless and on screen; `CanUserResizeColumns` is on, but dragging a divider is still to check by hand. **No maximize**: `CanMaximize = false` turns off the green button (no zoom, no full screen) and keeps close and minimize. **Extended client area**: `ExtendClientAreaToDecorationsHint = true` keeps the native traffic lights with the folder tabs beside them, and `BeginMoveDrag` moves the window from the tab strip. **Live regions**: `AutomationProperties.LiveSetting` (Polite, Assertive) exists in 12; still to check with VoiceOver and Narrator. On Windows, `WindowDecorationProperties.SetElementRole` (CloseButton, MaximizeButton, TitleBar…) is the likely way to keep snap layouts with own caption buttons. **Windows half still to do (W)**: edge snapping with `CanMaximize = false`, the Shell IDList Array drop, Narrator, own caption buttons. |

`Environment.SpecialFolder.LocalApplicationData` (and `ApplicationData`) return `~/Library/Application Support`, so `UsageLog`'s path code works unchanged.

### Plan corrections found on the Mac

1. **Tests can't become one `net10.0` project before cutover.** `keycuts.Tests` references `keycuts.GUI` (WPF, `net10.0-windows10.0.19041.0`), so Phase 2.1/2.3 needs a split: a GUI-free `net10.0` test project for Common and CLI (runs on macOS CI) and the WPF tests kept Windows-only until Phase 3.10.
2. **Default hotkey**: ⌃⌥Space conflicts with "Select next source in Input menu" (on by default). ⌃⌘Space is out too (Emoji & Symbols in every app's Edit menu). Choose the default in the Phase 5 mockup after checking macOS 26's list (System Settings → Keyboard → Keyboard Shortcuts) and common app menus, and keep the "can't register" message in Settings.
3. **First-run setup in the app, not the `.pkg` postinstall.** A postinstall runs as root with no reliable console user (and never runs for a Homebrew cask in CI or for a drag-installed app), so `~/Shortcuts`, the shims, the install-folder pointer and the Quick Action belong in the app's own startup (as the Windows app already repairs shims at start). The postinstall can stay empty.
4. **Signing recipe** (Phase 8.1): every file in `Contents/MacOS`, main executable last through the bundle, `spctl -t install` for the `.pkg` (see 0.1).
5. **Resident memory** is about 90 MB idle, not tens. Worth a line in the Settings text for "keep running in the menu bar", and a measurement again after trimming (`InvariantGlobalization`, trimming the self-contained publish).
6. **Two self-contained executables in one `Contents/MacOS`** (GUI and CLI) share one copy of the runtime only if both are published with the same runtime version into the same folder. Publish both with one `RuntimeFrameworkVersion` and check the shared files are identical, or make the CLI framework-dependent on the GUI's runtime.
7. **`GuiLauncher`** should use `open -b <bundle id>`, not `open -a keycuts`: `-a` matches any app named keycuts (a second copy in Downloads, an old build).

## Sources for the launcher research

- [MacMost: How to use Spotlight actions in macOS Tahoe](https://macmost.com/how-to-use-spotlight-actions-in-macos-tahoe.html)
- [Shing Lyu: Poor man's Raycast](https://shinglyu.com/productivity/2026/01/09/poor-mans-raycast-replace-app-launcher-features-using-only-macos-built-ins.html)
- [MacScripter: Changes to the Shortcuts app in Tahoe](https://www.macscripter.net/t/changes-to-the-shortcuts-app-in-tahoe/77180)
- [Raycast manual: Script commands](https://manual.raycast.com/script-commands)
- [PowerShell #27587: a script-launched .app triggers Rosetta](https://github.com/PowerShell/PowerShell/issues/27587)
- [Apple forums: RegisterEventHotKey and Option/Shift on Sequoia](https://developer.apple.com/forums/thread/763878)
- [Nick Liu: Tahoe hotkey dead end](https://nickboy-github-io.pages.dev/posts/tahoe-hotkey-dead-end/)
- [Avalonia 12](https://avaloniaui.net/blog/avalonia-12), [headless testing with xUnit](https://docs.avaloniaui.net/docs/testing/headless-xunit), [macOS platform options](https://docs.avaloniaui.net/docs/platform-specific-guides/macos), [TrayIcon](https://docs.avaloniaui.net/docs/reference/controls/tray-icon)
