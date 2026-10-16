# KC-14 — Manager: row actions menu

**Area:** keycuts.GUI · **Depends on:** 12 · **Blocks:** 15, 17

## Why
Item actions live **only** in each row's three-dot menu, because rows have no click behavior. The menu must be fully keyboard accessible.

## Scope
- The last column holds a 26–28px `IconButton` ("more" icon) with `AutomationProperties.Name="Actions for {name}"`.
  It's in the tab order and shows a visible focus ring.
- The menu is a themed WPF `ContextMenu` (styles from KC-05) anchored to the button
  (`PlacementTarget`, `Placement=Bottom`, right-aligned). Items, with icons:
  1. **Open keycut** (play icon): runs the keycut's `.bat` with `Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })`.
     The existing Batmanager uses `Process.Start(path)`, which doesn't use the shell on .NET Core, so don't copy that.
  2. **Edit** (file icon): opens the `.bat` in Notepad. See KC-15.
  3. **Copy destination** (copy icon): `Clipboard.SetText(keycut.Destination)`. Show a brief "Copied" confirmation in the status bar.
     Wrap the call in a retry or try/catch, because the clipboard can be locked by another process.
  4. A separator.
  5. **Delete** (delete icon, `Danger` color): **no confirmation** (decided). Selecting it immediately calls `KeycutRepository.DeleteAsync`,
     removes the row, updates the counts, and moves focus to the next row's menu button (or the search box if the list is now empty).
     Show a brief "Deleted “l-vicki”" message in the status bar.
- Open and close behavior:
  - Opens on click, Enter or Space on the button, Shift+F10, or the Apps/Menu key.
  - Arrow keys move between items, and Enter or Space activates the focused item.
  - The menu closes after any selection, on Escape, or on a click elsewhere. On Escape and after non-destructive actions,
    focus returns to the row's menu button.
  - Only one menu is open at a time. The row with the open menu keeps its hover tint (`row-active`).
- Errors (file locked, already deleted, failed to launch) show as a short inline message in the status bar area. Don't use a `MessageBox`.
- Don't add other actions. Batmanager's "Open destination location" is intentionally **not** carried over (out of scope).

## Acceptance criteria
- [ ] Each action works on URL, Folder, File, and Command keycuts using real files.
- [ ] Keyboard only: Tab to a row's menu button, open it, arrow to an item, activate it, and Escape closes it and returns focus. Clicking outside closes it.
- [ ] Delete removes the `.bat` file from disk and from the list, and updates both counts.
- [ ] Clicking a row (outside the menu button) does nothing.
