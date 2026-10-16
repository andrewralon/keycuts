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
- [x] Delete removes the `.bat` file from disk and from the list, and updates both counts.
- [ ] Clicking a row (outside the menu button) does nothing.


## Implementation notes

**Files changed**
- New: `keycuts.GUI/ViewModels/IKeycutActions.cs` (seam: `Launch`, `Edit`, `CopyText`, `DeleteAsync`, plus `SystemKeycutActions`), `keycuts.Tests/ManagerRowActionsTests.cs` (15 tests, all with a fake).
- `ManagerViewModel.cs`: optional `IKeycutActions` ctor argument; `OpenKeycut`, `EditKeycut`, `CopyDestination`, `DeleteKeycutAsync`; `StatusMessage`/`StatusIsError` (auto-clears after 4s, errors 8s); `KeycutDeleted` and `FocusRowRequested` events.
- `KeycutRowViewModel.cs`: `OpenCommand`, `EditCommand`, `CopyDestinationCommand`, `DeleteCommand`, `ActionsLabel`; optional owner argument.
- `ShellViewModel.cs`: passes the actions through; updates the creator's count after a delete. `App.xaml.cs`: passes `SystemKeycutActions`.
- `ManagerView.xaml(.cs)`: 28px `MenuButton` ("more" icon) per row with the themed `ContextMenu`, `TabNavigation="Continue"` on the grid, status-bar message area (polite live region), and a named empty-state button for the focus fallback.

**Decisions**
- Delete has no confirmation (Decision 8). It focuses the next row's menu button, else the previous row's, else the search box (or the "Create a keycut" button when the list is empty, since search is disabled then).
- If the file is already gone, the row is removed and an error message is shown. Other delete failures keep the row.
- Clipboard: `Clipboard.SetDataObject` retried 5 times on `COMException`, then the error is shown inline.
- Edit is wired to `IKeycutActions.Edit`, which launches `notepad.exe "path"` with `UseShellExecute=true`. KC-15 still owns the "file no longer exists" message and refresh.
- Menu placement is fixed (190px wide, right-aligned under the button). Shift+F10 and the Menu key use the same ContextMenu. Alt+Down also opens it.

**Deviations**
- None to the scope. The `MenuButton` control from KC-09 is reused so UI Automation reports expand/collapse.

**Results:** build 27 warnings (baseline), 0 errors; 367 tests pass; `dotnet format --verify-no-changes` shows only the 36 baseline WHITESPACE errors (none in the new or changed files). The GUI was launched against a temp fixture (`KEYCUTS_OUTPUT_FOLDER`) and closed. The Manager view's menu was not driven headlessly.

**Manual QA still needed** (use a fixture folder, never `C:\Shortcuts`)
- Open, Edit, Copy destination and Delete on URL, Folder, File and Command keycuts (real launch, Notepad, real clipboard).
- Keyboard only: Tab to a row's button (focus ring), Enter/Space/Shift+F10/Menu key opens, arrows cycle, Enter/Space activates, Escape closes and refocuses the button, click outside closes. After Delete, focus lands on the next row's button, and on the search box or create button when the list is empty.
- Only one menu open at a time; the row with the open menu keeps its hover tint; clicking a row does nothing.
- Light, dark and high-contrast look of the menu and Delete (danger) item; DPI scaling; Narrator names ("Actions for {name}", expanded/collapsed).
- Status messages ("Copied", "Deleted “name”", errors) appear, clear, and don't overlap the item count when the window is narrow.
- Delete on a read-only or locked file shows the inline error and keeps the row.
