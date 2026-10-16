# KC-03 — Keycut repository and description metadata

**Area:** keycuts.Common · **Depends on:** 01 · **Blocks:** 04, 12, 16

## Why
The manager needs real data: every keycut in the output folder with its name, type, description, and destination.
Today that logic sits in `keycuts.Batmanager/BatFormLogic.cs` (being retired in KC-20) and is tied to a `DataGrid`. The design also adds a
**Description** column, which the current `.bat` format doesn't store.

## Scope
1. **Model**: a `Keycut` record with these fields:
   - `Name`: file name without `.bat`
   - `Kind`: display kind (see KC-02)
   - `RawType`
   - `Description`: may be empty
   - `Destination`
   - `FilePath`
   - `DestinationStatus`: `Unknown | Ok | Missing`
2. **Repository**: `KeycutRepository`. It takes the output folder in its constructor so tests can inject a temp folder.
   Production builds it from `Runner.GetOutputFolder()`.
   - `Task<IReadOnlyList<Keycut>> LoadAllAsync(CancellationToken)`. Enumerate `*.bat` and parse with `ShortcutFile`.
     Skip unparseable files as today and log them with `Debug.WriteLine`. A missing output folder returns an empty list.
     **Don't create the folder on read**, unlike `BatFormLogic`. Report errors such as access denied or bad path as a typed failure.
   - `bool Exists(string name)`: case-insensitive, for the duplicate-name check.
   - `Task DeleteAsync(Keycut)`. Deletes the file and reports a typed failure if it's already gone or locked.
   - `Task<int> CountAsync()` for the creator's "View all keycuts" badge. `LoadAllAsync().Count` is acceptable.
   - `Task CheckDestinationsAsync(IEnumerable<Keycut>)`. Runs in the background and sets `DestinationStatus = Missing` for
     File/Folder keycuts whose target doesn't exist. Use a timeout per UNC path. URLs and commands are never checked.
3. **Description metadata** (decided: a comment line in the keycut file):
   - Format: `REM <description>…</description>`, placed in the header next to the existing `REM <type>`/`REM <destination>` lines.
   - Write: `Runner.CreateShortcutFile` always writes the line for new keycuts, empty by default (`REM <description></description>`).
     Users set descriptions by editing this line in Notepad (KC-15), so the empty placeholder shows them where to type.
   - Read: `ShortcutFile` parses the line case-insensitively and trims it. If the line is missing or malformed, `Description` is empty.
     Hand-edited files must never fail to load because of this line.
   - `ShortcutFile` currently picks command lines with `Contains("\\explorer.exe\"")`. Make sure `REM` lines are excluded so a
     description that mentions explorer.exe can't be mistaken for the command.
   - Files without the line (every existing keycut) keep working unchanged.

## Acceptance criteria
- [x] Unit tests against a temp folder cover: an empty folder, a missing folder, a mix of URL/folder/file/command `.bat` files,
      an unparseable file (skipped), a description that's present, empty, absent, or malformed, a description containing
      `explorer.exe`, a delete, a delete of a file that's already gone, `Exists` case-insensitivity, and `Missing` detection.
- [x] 100 files load in well under 500 ms locally.
- [x] The CLI still builds and behaves the same. Batmanager is being retired (KC-20), so don't migrate `BatFormLogic`.

## Notes
- `ShortcutFile.MatchesRegex` writes to `Console`. That's harmless in WPF, but don't add more of it.
- The CLI (`keycuts.CLI/Options.cs`) could take `--description` later. That's out of scope here.

## Implementation notes

**Files changed**
- New: `keycuts.Common/Keycut.cs` (`Keycut` record + `DestinationStatus`), `keycuts.Common/KeycutRepository.cs` (repository, `KeycutRepositoryException`, `KeycutRepositoryError`), `keycuts.Tests/KeycutRepositoryTests.cs` (repository, description and Runner round-trip tests).
- Changed: `keycuts.Common/ShortcutFile.cs` (`Description`, `ParseDescription`, `IsRemLine`; REM lines excluded from command selection), `keycuts.Common/Runner.cs` (writes `REM <description>…</description>`; `CreateShortcutFile` is now `public` with an optional `description` argument so tests can call it without touching PATH/registry).

**Decisions**
- Typed failures are `KeycutRepositoryException` with an `Error` of `NotFound | AccessDenied | InvalidPath | InUse | Unknown`. `LoadAllAsync` throws it for an invalid/unreadable folder; `DeleteAsync` throws it for a missing, locked, or protected file.
- A description line must be exactly `REM <description>…</description>` (case-insensitive, trimmed); anything else counts as malformed and gives an empty description. The first valid line wins.
- `Runner` flattens CR/LF in a description to spaces so it can't break the header.
- `ShortcutFile.Description` has `[Browsable(false)]` so the retiring Batmanager grid keeps its current columns. `BatFormLogic` is not migrated.
- `Keycut.Destination` trims the surrounding quotes that `ShortcutFile` keeps for file/folder destinations.
- `CheckDestinationsAsync` checks File, Folder and HostsFile keycuts (env vars expanded; max 8 in parallel). URLs, commands and CLSID keys are never checked. A check that times out (2 s) leaves the status `Unknown`.
- `Keycut` is a record, but `DestinationStatus` is mutable so the background check can update it.
- Added `KeycutRepository.CreateDefault()` (uses `Runner.GetOutputFolder()`).

**Verification**: build 27 warnings (baseline), 0 errors. Tests 64/64 pass. `dotnet format --verify-no-changes` reports the same 37 pre-existing WHITESPACE errors in 9 old files (none in new code). 100 files load in well under 500 ms (asserted in a test). Note: one earlier run showed a flaky KC-02 test (`DestinationResolverTests.Dispose` couldn't delete a `link.lnk` held by the shell); it passed on re-run.

**Deviations**: none.

**Manual QA still needed**
- Create a keycut with the CLI or GUI and confirm the new `REM <description></description>` line appears in the `.bat`. Run the keycut to confirm it still launches.
- Edit the description in Notepad and confirm it is picked up (once KC-12 shows it).
- A `Missing` check against an unreachable UNC path (timeout behavior).