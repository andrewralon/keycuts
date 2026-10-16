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
- [ ] Enter does nothing when the name is empty or the destination is invalid.
- [ ] View model unit tests cover each outcome mapping and the `CanExecute` rules, using a fake `KeycutCreator`.
