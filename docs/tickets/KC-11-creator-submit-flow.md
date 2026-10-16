# KC-11 — Creator: Enter-to-create, saving, errors, success

**Area:** keycuts.GUI · **Depends on:** 04, 10 · **Blocks:** 17

## Why
Creating should be: type a name → press Enter → done, with clear inline feedback. Today, Enter is handled at the
**window** level (`Main_KeyDown`) and creates from anywhere. The code blocks with `Thread.Sleep(400)`, uses
`MessageBox` for overwrite and errors, and plays a beep.

## Scope
- `CreatorViewModel.CreateCommand` (an `AsyncRelayCommand`):
  - `CanExecute`: a valid destination, a non-whitespace name, and not saving.
  - Bound to the **Create** button and to **Enter in the name field only** (`KeyBinding` on the `TextBox`, or `IsDefault`
    on the Create button scoped to the form). Enter on Browse, the settings button, or the footer link must **not** create.
  - Remove the window-level Enter handler.
- Flow: set **Saving** → `KeycutCreator.CreateAsync` (KC-04) → map the outcome:

| Outcome | UI |
| --- | --- |
| `Success` | **Success** state, then reset to **Empty**: clear the destination and name, refresh the count, and focus the destination field. Keep the existing system sound as a subtle confirmation unless product says otherwise. |
| `DuplicateName` | **Duplicate-name error**. Keep the name text, select all of it, and focus the field. The error clears as soon as the name is edited. |
| `InvalidName` | Same visuals as duplicate, with the message "Use letters, numbers, dashes, or dots — no \ / : * ? \" < > \|". |
| `DestinationInvalid` | **Invalid destination** state, for example "This file no longer exists." |
| `OutputFolderError` / `PathUpdateError` / `Unexpected` | Inline error under the name row with a short, human-readable message and a hint to check Settings. No `MessageBox`. |

- Duplicate check: optionally run `KeycutRepository.Exists` with a debounce (~250ms) while typing, so the error shows before
  Enter is pressed.
- **Force overwrite (decided):** when the Settings toggle is on, a duplicate name is overwritten silently. There's no confirmation and
  no duplicate error. When it's off, the duplicate-name error shows inline and nothing is written.
- Remove the `Thread.Sleep`, the `MessageBox` overwrite prompt, and `UpdateLayout` refresh hacks from the creator path.

## Acceptance criteria
- [ ] Drop → type name → Enter creates the `.bat` in the output folder, and the creator returns to Empty with the count incremented.
- [ ] Clicking Create does the same.
- [ ] The Saving state is visible for slow operations. Test it with an artificial delay in a debug build. The UI never freezes.
- [ ] Duplicate names with Force overwrite off show the inline error and create no file. With it on, the existing file is overwritten.
- [x] Enter does nothing when the name is empty or the destination is invalid.
- [x] View model unit tests cover each outcome mapping and the `CanExecute` rules, using a fake `KeycutCreator`.

## Implementation notes

**Files changed**
- `keycuts.GUI/ViewModels/CreatorViewModel.cs`: `CreateCommand` (`AsyncRelayCommand`), outcome mapping, success reset, duplicate-name preview, `FocusRequested` event, injectable `createAsync` / `nameExists` seams (default: `KeycutCreator.CreateAsync` and the configured output folder).
- `keycuts.GUI/Views/CreatorView.xaml(.cs)`: Create button bound to `CreateCommand`; `KeyBinding` Enter on the name `TextBox` only; the view-level `KeyDown` Enter handler, `Thread.Sleep`, `MessageBox`, `UpdateLayout` and the old beep-on-every-outcome were removed; 250 ms debounce for the duplicate check; focus handling via `FocusRequested`.
- `keycuts.GUI/CommonLogic.cs`: removed the temporary `CreateShortcut` adapter (the creator calls `KeycutCreator` directly).
- `keycuts.Tests/CreatorSubmitTests.cs` (new): 16 test methods (some parameterized) with a fake creator for every outcome, `CanExecute` rules, Saving, success reset, duplicate preview.

**Decisions**
- Re-validation (KC-10 note): `CanCreate` still doesn't wait for the typing-debounce resolve. `KeycutCreator.CreateAsync` resolves the destination again, so an unresolved or stale destination comes back as `DestinationInvalid` and shows the Invalid-destination state. Nothing is created.
- Success keeps the fields for `SuccessDuration` (2 s), then `Clear()` and focuses the destination. Editing either field in that window cancels the reset. `CanCreate` is false during Success, so a second Enter can't re-submit and hit a duplicate error.
- The name is trimmed before it's sent (`KeycutCreator` rejects names with edge whitespace).
- `InvalidName` reuses the duplicate-name visuals with its own message. `OutputFolderError` / `PathUpdateError` / `Unexpected` show a red helper line under the name row mentioning Settings; destination errors reuse the KC-10 messages (e.g. "That destination wasn’t found.") rather than "This file no longer exists.".
- Force overwrite: the VM never shows a duplicate error when the setting is on (the typing preview is skipped and `KeycutCreator` overwrites).
- The system beep now plays only on success (ticket: keep the sound).
- Debug builds only: `KEYCUTS_CREATE_DELAY_MS` adds an artificial delay before creating, to see the Saving state.

**Deviations:** none from the scope. Window-level Enter had already gone in KC-08/09; the remaining Enter handler was on the creator view (bubbling `KeyDown`).

**Verification:** `dotnet build` 0 errors, 27 warnings (baseline, `--no-incremental`); `dotnet test` 313 passed; `dotnet format --verify-no-changes` 36 pre-existing WHITESPACE errors, none in new/changed files. The GUI launched with a temp `KEYCUTS_OUTPUT_FOLDER` and stayed responsive; it was closed afterwards.

**Manual QA still needed** (the unchecked criteria above):
- Drop → type name → Enter creates the `.bat` and the creator returns to Empty after ~2 s with the count incremented; the Create button does the same.
- Saving state visible with `KEYCUTS_CREATE_DELAY_MS=1500` (Debug), UI not frozen, inputs disabled.
- Duplicate with Force overwrite off: inline error, name selected and focused, no file; error clears on edit; shows while typing (~250 ms). With it on: silently overwritten.
- Enter on Browse, the settings button, and the footer link doesn't create; Enter in the destination field only resolves.
- Focus lands correctly after each outcome; the success beep sounds.