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
- [ ] Edit opens the right file in Notepad for each type, including paths with spaces.
- [ ] Adding a description in Notepad, saving, and switching back to keycuts shows it in the Description column.
- [ ] A hand edit that breaks the file causes that keycut to drop out of the list (as unparseable today) without crashing the manager.
- [ ] Edit on a keycut that was deleted outside the app shows the inline message, with no exception.

## Out of scope
An in-app edit form, renaming, and changing the type manually.
