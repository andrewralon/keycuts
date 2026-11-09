# keycuts — Gameplan

Updated 2026-11-09. This replaces the original 44-issue plan, which is still in git history. Most of that plan's Phase 1 and Phase 2 shipped, and the UI redesign (KC-01 to KC-20, `docs/tickets/`) made most of its Phase 3.3 moot by replacing the WPF GUI and the Batmanager app.

The overall order hasn't changed: **Windows first, then the parser safety net before any format change, with macOS last.**

## Where things stand

| Area | State | Issues |
|---|---|---|
| .NET 10 foundation | Done. SDK-style projects, CI, warnings as errors, versions pinned in `Directory.Packages.props`. | #82, #83, #86, #80 (rescoped into #117), #81 |
| UI redesign | Done. One window with Creator and Manager views; Batmanager retired. | #112; closed #40, #54, #63, #75, #110 |
| Installer and context menu | Done apart from signing and arm64. Per-user MSI, no elevation, verbs in `HKCU\Software\Classes`, PATH owned by the installer, releases from `master`. | #99, #105, #94, #64; open: #118, #119 |
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
   - **Next, one per commit**, each measured by `LaunchLineRoundTrip.verified.txt`: (a) strip the quotes in the regex fallback for File/Folder (about 22 of the 34), then drop `KeycutRepository`'s `Trim('"')`; (b) keep the leading `::` on CLSID keys in the fallback (3); (c) read `START "" /B cmd /C "<command>"` back as Command (1); (d) empty/no-command files should parse as Unknown, not Command (3); (e) `OpenWithApp` double quoting (1); (f) drop the `MatchesRegex` quote-splicing hack (1); (g) decide what to do about `%` escaping in `Url`/`Command` for hand-written files (3).
4. **First run: #11 and #61.** Explain the Win+R workflow and offer to create a `keycuts` keycut to the app itself.
5. **Manager features: #55** (rename a keycut, refreshing the list; rename the file and rewrite the REM `<shortcut>` line only, don't re-emit from parsed fields, or hand-added arguments like `%*` are lost) **and #79** (open the keycut's own file location).
6. **#92: replace `ShortcutType` with `DestinationKind`**, after #91, because it changes the type names written into files. It also fixes the dead `%windir%` hosts comparison and unblocks **#9** (drag-and-drop of Recycle Bin and other shell items).
7. **#31: substitute environment variables in paths**, after #91, so the literal destination survives alongside the expanded one.
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
