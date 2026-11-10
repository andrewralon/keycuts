# keycuts — Gameplan

Updated 2026-11-09. This replaces the original 44-issue plan, which is still in git history. Most of that plan's Phase 1 and Phase 2 shipped, and the UI redesign (KC-01 to KC-20, `docs/tickets/`) made most of its Phase 3.3 moot by replacing the WPF GUI and the Batmanager app.

The overall order hasn't changed: **Windows first, then the parser safety net before any format change, with macOS last.**

## Where things stand

| Area | State | Issues |
|---|---|---|
| .NET 10 foundation | Done. SDK-style projects, CI, warnings as errors, versions pinned in `Directory.Packages.props`. | #82, #83, #86, #80 (rescoped into #117), #81 |
| UI redesign | Done. One window with Creator and Manager views; Batmanager retired. | #112; closed #40, #54, #63, #75, #110 |
| Installer and context menu | Done apart from signing and arm64. Per-user MSI, no elevation, verbs in `HKCU\Software\Classes`, PATH owned by the installer, releases from `main`. | #99, #105, #94, #64; open: #118, #119 |
| Parser safety net | Done. Characterization snapshots of the parser and emitter over `tests/corpus/legacy/`, plus a round-trip snapshot. | #87, #88, #89 |
| Quick bugs | Done: #65 (CLI overwrite "no"), #93 (`Command` emitted nothing), #25 (old `START /D` emitter). Also #68 (crash on an access-denied destination), checked by hand on 2026-11-09. | |

## Next, in order

1. ~~**Merge #117, then the safety-net PR.**~~ Done (#117, #120).
2. ~~**Manual checks on Windows.**~~ Done 2026-11-09, both closed:
   - #64: as a non-admin, "keycut this!" works for a file, a folder and a folder background.
   - #68: this machine has no IIS, so `applicationHost.config` doesn't exist. Tested an access-denied path instead (`System32\config\SAM`): the GUI shows "Access to that destination was denied." and the CLI returns `NotStarted`. No crash.
3. **#91, rescoped: read type and destination from the `REM` lines keycuts already writes**, with the command-line regex as the fallback. Then fix the round-trip failures one per commit, with `LaunchLineRoundTrip.verified.txt` as the requirement list.
   - **Baseline (before #91):** 7 of 43 corpus files round-trip. The 35 failures: ~22 are File/Folder destinations that come back with their surrounding quotes, 3 are CLSID keys re-emitted as Folder, 3 are empty or non-keycuts files with no destination, 3 are `%` escaping in hand-written files, 2 are the `MatchesRegex` quote-splicing hack (`gg.bat`, `header-content-drift.bat`), 1 is `OpenWithApp` quoted twice (`hostsfile-type.bat`).
   - **Done: REM lines read first** (`ShortcutFile.TryParseRemTypeAndDestination`). Both `<type>` and `<destination>` must be present, the type must be a defined name (not a number or a list), and the destination must not be empty, or the regex result stands. `RoundTrip.verified.txt` went from 7 ok to 38 ok, but that number is no longer a measure: the second parse reads the REM lines the emitter just wrote, so it passes even when the emitted launch line is broken (all of `old-format/` emits `START "" /B ""C:\...""`).
   - **Done: launch-line round trip.** `LaunchLineRoundTrip.verified.txt` repeats the round trip with the emitted REM `<type>` and `<destination>` lines removed, so only the launch line carries the keycut. **8 ok, 34 fail, 1 skipped.** The 34: 12 new-format files whose emitted launch line is right but whose fallback parse keeps the quotes; 10 old-format files whose first parse keeps the quotes, so the emitter quotes them twice; 3 CLSID keys that lose `::` in the fallback; 3 empty files (`empty`, `dequote`, `pm`); 3 `%` escaping (`jira`, `newjira`, `swaphosts`); 1 `MatchesRegex` hack (`gg`); 1 `OpenWithApp` quoted twice (`hostsfile-type`); 1 `cmd /C` line the parser can't read (`command-type`). Keep `RoundTrip.verified.txt` too: it guards the REM path.
   - **Behavior changes to know about** (in `Parser.verified.txt`): the REM destination wins over the launch command, so `d.bat` now reads the expanded path `C:\Users\USERNAME\Desktop` instead of `%USERPROFILE%\Desktop` (see #31); `keypaste.bat`/`kp.bat` change from Command to File and lose the hand-added `%*`; `header-content-drift.bat` reads the REM url, not the chrome command; CLSID destinations keep their `::`.
   - **Done: quotes stripped in the fallback** (`ShortcutFile.Unquote`, one leading and one trailing quote, the inverse of the emitter's wrapping). `LaunchLineRoundTrip.verified.txt`: 8 ok, 34 fail → **30 ok, 12 fail**, exactly the 22 predicted. `KeycutRepository`'s `Trim('"')` is gone too; it had started removing quotes that belong to the value (`"cmd.exe" /k ...`, `swaphosts.bat`'s closing quote). The 12 left are (b) to (g) below: 3 CLSID, 3 empty, 3 `%`, `gg`, `hostsfile-type`, `command-type`.
   - **Done: CLSID keys keep their `::` in the fallback** (the capture group in `patternCLSID` now includes it). `LaunchLineRoundTrip.verified.txt`: 30 ok, 12 fail → **33 ok, 9 fail** (`programs`, `rb`, `recyclebin`). Not fixed: `[shell:]?` in the same pattern is a character class, so a `shell:::{...}` launch line still reads back as Folder; no corpus file has one.
   - **Done: the emitter's `cmd /C` line reads back as Command** (`ShortcutFile.IsEmittedCommand`, an exact inverse of `Runner.BuildCommand`, tried before every other pattern so an `explorer.exe` call inside the command isn't read as a Folder, and without `MatchesRegex`, so quoted arguments survive). `LaunchLineRoundTrip.verified.txt`: 33 ok, 9 fail → **34 ok, 8 fail** (`command-type`).
   - **Done: files with no launch line parse as Unknown** (`Type` starts at Unknown instead of `default(ShortcutType)`, which is Command). `empty`, `dequote` and `pm` are now skipped by both round trips instead of failing: `LaunchLineRoundTrip.verified.txt` **34 ok, 5 fail, 4 skipped**; `RoundTrip.verified.txt` 38 ok, 1 fail, 4 skipped. The manager already left these out (no `Command`). `pm.bat` is a real 0.1.x keycut whose launch line is a bare quoted path, not `START`; the parser still can't read it.
   - **Done: `OpenWithApp` is a bare path** (unquoted like `Destination`; the constructor no longer adds a second pair). `LaunchLineRoundTrip.verified.txt` **35 ok, 4 fail, 4 skipped**; `RoundTrip.verified.txt` 39 ok, 0 fail, 4 skipped (`hostsfile-type`).
   - **Done: the quote splice is gone** (`MatchesRegex` joined any `" "` into a backslash for every pattern; `ShortcutFile.IsStartInFolder` now handles the 0.1.x `START "" /D "<folder>" "<file>"` form explicitly, so `ff`, `ghd`, `ie`, `iis`, `npp` and `pdn` are unchanged, and an empty folder leaves the rest of the line as the destination). `LaunchLineRoundTrip.verified.txt` **36 ok, 3 fail, 4 skipped** (`gg`). Left: `openwithapp.bat`'s app is lost because the fallback has no File-with-app form; it only parses through its REM lines today.
   - **Next, one per commit**, each measured by `LaunchLineRoundTrip.verified.txt`: ~~(a) strip the quotes in the regex fallback for File/Folder~~ (done); ~~(b) keep the leading `::` on CLSID keys in the fallback~~ (done); ~~(c) read `START "" /B cmd /C "<command>"` back as Command~~ (done); ~~(d) empty/no-command files should parse as Unknown, not Command~~ (done); ~~(e) `OpenWithApp` double quoting~~ (done); ~~(f) drop the `MatchesRegex` quote-splicing hack~~ (done); ~~(g) decide what to do about `%` escaping in `Url`/`Command` for hand-written files~~ (decided: accepted as expected failures, see Decisions); ~~(h) make the URL escaping in the emitter reversible~~ (done).
   - **Done: (g) decided.** `jira`, `newjira` and `swaphosts` are listed in `CharacterizationRenderer.ExpectedRoundTripFailures`; `LaunchLineRoundTrip.verified.txt` reads **36 ok, 3 fail (3 expected), 4 skipped**. The parser notes that predated reading the REM lines (`command-type`, `header-content-drift`, `keypaste`, `kp`) were rewritten in the same PR.
   - **Done: (h) URL escaping is reversible.** `Shortcut.SanitizeBatEscapeCharacters` doubles every `%` and puts `^` before every `^ & | < >` outside double quotes (inside them cmd takes `^` literally; checked by running the output through cmd). Before, `&&` was written `&^&` (the first `&` split the line and cut the URL off), `%%` ran as `%`, and `^&` became `^^&` (bare `&` again). Emitter snapshot: `url-with-ampersand-and-percent` and `url-with-double-ampersand`. Only the launch line of new URL keycuts with those characters changes; the REM destination is the same. This closes the #91 fallback steps: 36 of 39 launch-line round trips pass, and the 3 failures are expected.
4. ~~**First run: #11 and #61.**~~ Done: a welcome panel in the creator (`WelcomeViewModel`) explains Win+R and offers a keycut to `keycuts.GUI.exe`, named `kc` by default (not `keycuts`, which would collide with the CLI's `keycuts.exe` on PATH) and editable. It shows on the first run (`WelcomeDismissed` unset, upgrades included), whenever the keycuts folder is empty, and from Settings → Show Welcome. Decided with the user: offered, not created silently; in the app, not the installer's finish page (the portable zip has no installer).
5. ~~**Manager features: #55 and #79.**~~ Done. #79: **Open file location** in the row menu shows the `.bat` in Explorer. #55: F2 or **Rename** edits the name in place (decided with the user: inline, like Explorer, with F2); `KeycutRepository.RenameAsync` moves the file and rewrites only the REM `<shortcut>` line (through a temp file), never re-emitting, so hand-added arguments like `%*` survive; taken names are refused, a case-only change is allowed.
6. ~~**#92: replace `ShortcutType` with `DestinationKind`.**~~ Rescoped (see Decisions, 2026-11-09): only the hosts bug was fixed now. `Shortcut.IsHostsFile` compares with this machine's System32, replacing the dead `%windir%` comparison. The `ShortcutType` rename and the type names written into files wait for the port. #9 (drag-and-drop of Recycle Bin and other shell items) stays in the backlog.
7. ~~**#31: substitute environment variables in paths.**~~ Done, as decided with the user: a Settings option, off by default; holding Shift while creating flips it for that keycut; the creator shows the path as it will be saved ("Saved as …") and follows both. Known folders (user, program, Windows, OneDrive) plus custom variables whose value is a folder; longest match wins. The variable form is what's written to both the REM `<destination>` and the launch line. Shared (non-profile) keycuts folders are #137.
8. **#97: `keycuts paste` plus a `keypaste` shim**, then **#108** (expand variables before copying). #96, the `System.CommandLine` rewrite, is optional: do it only if #97 needs more verbs than CommandLineParser handles comfortably.
9. **#118: signing.** It needs a certificate, so it's a cost decision before it's an engineering one. **#119: win-arm64.**
10. **#107 leftovers:** `Console.WriteLine` used as logging in the library, the bare `BAD THINGS` catches in `keypaste`, and the Debug-only `Console.ReadKey()` in both CLIs.

## Later: macOS (Phase 4, unchanged and deprioritized)

#84 (managed `.lnk` parser), #85 (`src/` restructure), #98 (Avalonia), #100–#104, #106. When this starts, design the cross-platform seam against the two real implementations. That's the reason #90 was closed rather than built ahead of time.

## Rules that still apply

- **Never move two variables in one commit.** An emitter or parser change must show up as a reviewed diff in `keycuts.Tests/Characterization/Snapshots/*.verified.txt`. When a snapshot test fails, it writes a `.received.txt` next to the snapshot; if the change is intended, copy it over the `.verified.txt` in the same commit.
- **Never edit or delete corpus files**; add new ones (`tests/corpus/legacy/README.md`).
- **Add an interface only when a fix needs one to be testable**, the way `IPathUpdater`, `IClassesStore` and `IKeycutActions` were added.

## Decisions made on 2026-11-08

| Issue | Decision | Why |
|---|---|---|
| #90 platform seam | Closed, not planned | Eight interfaces with one implementation each, for a port at the end of the roadmap. The redesign added seams where tests needed them. |
| #91 JSON header | Rescoped | The emitter already writes `REM <type>` and `REM <destination>`. Reading them gets most of the round-trip benefit without a new format. |
| #95 `settings.json` | Closed, not planned | Its bug (reads that wrote) is fixed, and the MSI now owns the `HKCU` values, so moving settings would make the installer harder. |
| #96 CLI rewrite | Optional | #65 and #25 were fixed directly, and CommandLineParser 2.9.1 is on nuget.org. |
| #81 lockfiles | CPM and `nuget.config` only | The installer's `-r win-x64` publish rewrites lockfiles on every build. Pinned versions cover the original failure (a package missing from the feed). |
| #105 installer | Closed | Shipped. Signing and arm64 moved to #118 and #119, and the `keypaste` shim to #97. |
| #55, #61, #65 | Reopened | Closed without the fix in place. #65 is now fixed. |

## Decisions made on 2026-11-09

| Issue | Decision | Why |
|---|---|---|
| #91 REM precedence | The REM `<type>` and `<destination>` win over the launch command, even when they disagree. | The parser only feeds display and checks; the `.bat` still runs its own command, and `Command` keeps the full line. Comparing the two would need a second parser. Cost: `d.bat` (legacy REM holds the expanded path; #31 fixes it for new files), `keypaste.bat` and `kp.bat` (`%*` not in `Destination`). |
| #91 `%` in the fallback | The parser reads `%` and `^&` in a launch line as written; no unescaping, no migration of 0.1.x files. The 3 hand-written `not-keycuts` files are expected launch-line round-trip failures. | Only a 0.1.x URL keycut with `%` and no REM lines is affected (it shows `%%20`), and none is known: every current file has REM lines, and the `.bat` runs correctly either way. Unescaping everywhere would corrupt hand-written scripts with adjacent variables (`%A%%B%`); unescaping only files with a `REM keycuts` header is the fix if a report comes in. Re-emitting a hand-written script changes it whatever the parser reads. |
| #92 `DestinationKind` | Rescoped: only the hosts-file bug is fixed now. No `IShortcutScriptFormat`, `IShellObjectCatalog`, macOS tables or v2 schema; the `ShortcutType` rename and the type names written into files wait for the port. | The issue was written for the macOS port, which #90 closed as premature. On Windows its concrete value was the dead `%windir%` hosts comparison (fixed: `Shortcut.IsHostsFile`) and unblocking #9, which a rename alone doesn't do. |
