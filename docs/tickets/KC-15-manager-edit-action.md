# KC-15 — Manager: Edit action

**Area:** keycuts.GUI · **Depends on:** 14

## Decision
**Edit opens the keycut's `.bat` file in Notepad**, the same as Batmanager does today. Keep it simple: no in-app edit form.
Editing in Notepad is also how users set a **Description**. They fill in the `REM <description>…</description>` line that
KC-03 writes into every new keycut.

## Scope
- **Edit** in the row menu (KC-14) runs `Process.Start(new ProcessStartInfo("notepad.exe", $"\"{keycut.FilePath}\"") { UseShellExecute = true })`.
  Quote the path because it may contain spaces.
- If the file no longer exists, don't launch Notepad. Show the inline "This keycut no longer exists" message in the status area and refresh the list.
- **Refresh after editing:** the manager reloads when the window is re-activated (KC-12), so changes saved in Notepad
  (description, destination, or a hand-edited command) show up when the user switches back. Don't use a file watcher.
- Renaming isn't supported through Edit. Users can delete and recreate the keycut. Note this in the README's user docs (KC-18).

## Acceptance criteria
- [x] Edit opens the right file (path passed to the seam; quoting is manual QA) in Notepad for each type, including paths with spaces.
- [x] Adding a description (reload logic unit-tested; live Notepad round-trip is manual QA) in Notepad, saving, and switching back to keycuts shows it in the Description column.
- [x] A hand edit that breaks the file causes that keycut to drop out of the list (as unparseable today) without crashing the manager.
- [x] Edit on a keycut that was deleted outside the app shows the inline message, with no exception.

## Out of scope
An in-app edit form, renaming, and changing the type manually.

## Implementation notes
- **Files changed:** keycuts.GUI/ViewModels/IKeycutActions.cs (SystemKeycutActions.Edit throws FileNotFoundException when the file is missing, before launching), keycuts.GUI/ViewModels/ManagerViewModel.cs (EditKeycut shows "This keycut no longer exists" as an error status and calls LoadAsync to refresh; other failures use the existing message), keycuts.Tests/ManagerRowActionsTests.cs (4 new tests, FakeActions.EditError).
- **Decisions:** no new interface member; the missing-file signal is FileNotFoundException through the existing seam. Refresh on re-activation already exists (MainWindow.Activated, KC-12) and IsSameContent compares Description/Destination, so Notepad edits show up; unparseable files drop out via the loader. Broken-file criterion relies on existing loader behaviour (KC-02/KC-07), not re-tested here.
- **Deviations:** none.
- **Results:** build 27 warnings (baseline), 371 tests pass, format 36 WHITESPACE (baseline).
- **Manual QA needed:** Edit on a keycut in a path with spaces opens Notepad on the right file; add a description, save, switch back, see it in the Description column; break the file by hand and confirm it drops out; delete the .bat externally then Edit shows the message and the row disappears.
