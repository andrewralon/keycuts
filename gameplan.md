# keycuts — Gameplan

Updated 2026-11-08. This replaces the original 44-issue plan, which is still in git history. Most of that plan's Phase 1 and Phase 2 shipped, and the UI redesign (KC-01 to KC-20, `docs/tickets/`) made most of its Phase 3.3 moot by replacing the WPF GUI and the Batmanager app.

The overall order hasn't changed: **Windows first, then the parser safety net before any format change, with macOS last.**

## Where things stand

| Area | State | Issues |
|---|---|---|
| .NET 10 foundation | Done. SDK-style projects, CI, warnings as errors, versions pinned in `Directory.Packages.props`. | #82, #83, #86, #80 (rescoped into #117), #81 |
| UI redesign | Done. One window with Creator and Manager views; Batmanager retired. | #112; closed #40, #54, #63, #75, #110 |
| Installer and context menu | Done apart from signing and arm64. Per-user MSI, no elevation, verbs in `HKCU\Software\Classes`, PATH owned by the installer, releases from `master`. | #99, #105, #94, #64; open: #118, #119 |
| Parser safety net | Done. Characterization snapshots of the parser and emitter over `tests/corpus/legacy/`, plus a round-trip snapshot. | #87, #88, #89 |
| Quick bugs | Done: #65 (CLI overwrite "no"), #93 (`Command` emitted nothing), #25 (old `START /D` emitter). Crash fixed, manual check pending: #68. | |

## Next, in order

1. **Merge #117, then the safety-net PR.** CI is the first place the new snapshots run on Windows.
2. **Manual checks on Windows.** These can't be tested in CI:
   - #64: as a non-admin, right-click a file in Explorer and check that "keycut this!" appears and works. Then close #64 for real.
   - #68: on the x64 build, create a keycut to `C:\Windows\System32\inetsrv\config\applicationHost.config`. It no longer crashes. The original failure was probably 32-bit file-system redirection, which the x64 build doesn't have.
3. **#91, rescoped: read type and destination from the `REM` lines keycuts already writes**, with the command-line regex as the fallback. Then fix the round-trip failures one per commit, with `RoundTrip.verified.txt` as the requirement list. Only 7 of 43 corpus files round-trip today. Most failures come from one bug: File and Folder destinations come back with their surrounding quotes.
4. **First run: #11 and #61.** Explain the Win+R workflow and offer to create a `keycuts` keycut to the app itself.
5. **Manager features: #55** (rename a keycut, refreshing the list) **and #79** (open the keycut's own file location).
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
