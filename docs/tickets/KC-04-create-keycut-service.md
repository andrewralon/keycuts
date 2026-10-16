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
- [ ] Unit tests use a temp output folder. They cover each outcome, overwrite on and off, and a destination deleted between selection and create.
- [ ] A created `.bat` is byte-identical to what `Runner.Run` produces today for the same inputs, apart from the new optional description line from KC-03.
- [ ] `keycuts.GUI/CommonLogic.cs` either delegates to the new service or is removed once KC-11 lands. The CLI is unaffected.

## Notes
- Running tests must not touch the user's PATH. `PathSetup.AddToOrReplaceInSystemPath` currently runs on every `Run`.
  Put it behind a seam, such as an injectable `IPathUpdater` or an option, so tests can skip it.
