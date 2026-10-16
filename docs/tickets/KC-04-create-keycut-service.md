# KC-04 — Create-keycut service with structured results

**Area:** keycuts.Common (+ thin GUI adapter) · **Depends on:** 02, 03 · **Blocks:** 11

## Why
The creator needs specific inline states: saving, duplicate name, and invalid or missing destination. Today
`CommonLogic.CreateShortcut` runs synchronously on the UI thread and returns a bare `ExitCode`. The window then
shows `MessageBox` dialogs, calls `Thread.Sleep(400)`, and beeps.

## Scope
- `KeycutCreator.CreateAsync(string destination, string name, CreateOptions options, CancellationToken)` returns
  `CreateResult`, which carries `Outcome` and `Keycut?`. Outcomes:
  - `Success`
  - `DuplicateName`: the file exists and overwrite is off
  - `InvalidName`: empty, invalid filename characters, reserved device names (`CON`, `NUL`, …), or path separators
  - `DestinationInvalid`: re-validated via KC-02 at submit time, because the target may have been deleted since selection
  - `OutputFolderError`: maps `CannotCreateOutputFolder`
  - `PathUpdateError`: maps `CannotUpdatePath`
  - `Unexpected`: carries the exception message for logging
- Internally reuse `Runner.Run` / `KeycutArgs` so the `.bat` format and PATH handling stay identical.
  Don't fork the file-writing logic.
- `CreateOptions.Overwrite` defaults to `Runner.GetForceOverwrite()` (the existing Settings toggle). When it's true, an existing file
  with the same name is overwritten with no confirmation, and the outcome is `Success`.
- **Name rule:** the creator accepts a plain name only. Today `Shortcut` accepts a full path as the "name" and writes there.
  Keep that working for the CLI, but return `InvalidName` from the GUI path.
- Runs off the UI thread (`Task.Run`). No `Thread.Sleep`.

## Acceptance criteria
- [x] Unit tests use a temp output folder. They cover each outcome, overwrite on and off, and a destination deleted between selection and create.
- [x] A created `.bat` is byte-identical to what `Runner.Run` produces today for the same inputs, apart from the new optional description line from KC-03.
- [x] `keycuts.GUI/CommonLogic.cs` either delegates to the new service or is removed once KC-11 lands. The CLI is unaffected.

## Notes
- Running tests must not touch the user's PATH. `PathSetup.AddToOrReplaceInSystemPath` currently runs on every `Run`.
  Put it behind a seam, such as an injectable `IPathUpdater` or an option, so tests can skip it.

## Implementation notes

**Files changed**
- New: `keycuts.Common/KeycutCreator.cs` (`KeycutCreator.CreateAsync`, `IsValidName`, `CreateOptions`, `CreateResult`, `CreateOutcome`), `keycuts.Common/IPathUpdater.cs` (`IPathUpdater`, `SystemPathUpdater`, `NoOpPathUpdater`), `keycuts.Tests/KeycutCreatorTests.cs` (28 cases).
- Changed: `Runner.cs` (new `Runner(fixedOutputFolder, IPathUpdater)` ctor; `Run` takes an optional description; output-folder creation failure now returns `CannotCreateOutputFolder` instead of throwing), `KeycutArgs.cs` (`Description`), `KeycutRepository.cs` (`TryParse` made `internal` to read back the created file), `keycuts.GUI/CommonLogic.cs` (delegates to `KeycutCreator`, maps outcome to `ExitCode`), `keycuts.Tests/DestinationResolverTests.cs` (retrying `Dispose` for the KC-02 flaky cleanup).

**Decisions**
- Order of checks: name, destination (KC-02 `DestinationResolver`), output folder, duplicate. `CreateResult` also carries `DestinationError` (for DestinationInvalid) and `Message` (for Unexpected).
- The PATH seam is `IPathUpdater`, used only through a `Runner` created with a fixed output folder. In that mode the registry is neither read nor written for the folder. `CreateOptions.OutputFolder`, `PathUpdater` and `Overwrite` default to the production values (registry folder, real PATH, Settings toggle). `SystemPathUpdater` catches exceptions (for example no admin rights) and reports `PathUpdateError`.
- The `.bat` is written by `Runner.Run`/`CreateShortcutFile`, unchanged. The service passes `name + ".bat"` to `Shortcut`, so a dot in the name ("notes.v2") isn't dropped as an extension. A test compares output against the existing `Runner` path.
- Name rules: no leading/trailing whitespace, no trailing dot, max 200 chars, no invalid filename characters (so no separators/colons/wildcards), no reserved device names (also `NUL.txt`). Duplicate check is case-insensitive via `File.Exists`.
- The CLI keeps using `Runner.Run` with full-path names. Its only behavior differences: PATH failure now returns `CannotUpdatePath` instead of an exception, and folder creation failure returns `CannotCreateOutputFolder`.

**Deviations**
- `CommonLogic.CreateShortcut` still exists as a sync adapter (blocks the UI thread as before; `Thread.Sleep` remains in `MainWindow`). KC-11 should call `KeycutCreator` directly and remove it. Empty destination now maps to `CannotFindPath` instead of `BadArguments` (GUI only).

**Manual QA still needed**
- Create a keycut in the real GUI with a changed output folder to confirm the real PATH update still works (needs admin and touches the real PATH/registry; not done here).
- Confirm that `Overwrite = null` honors the Settings "force overwrite" toggle.

Verification: build 27 warnings (baseline), 104 tests pass, `dotnet format` shows no new violations (36 pre-existing; none in new files).