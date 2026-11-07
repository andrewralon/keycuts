# KC-10 — Creator: drag-and-drop, paste/type, and Browse

**Area:** keycuts.GUI · **Depends on:** 02, 09 · **Blocks:** 11

## Why
The flow is: drop, paste, or browse to a file, folder, or URL → the app detects and displays it → focus moves to the name field.
The UI2 design makes the destination line an **inline text field**, so paste (and typing) work there directly. Browse becomes
a **File… / Folder…** menu. Today `DragDrop.cs` accepts `FileDrop` and `Text` on many nested grids.

## Scope
- **Drag and drop**
  - The whole creator view is a drop target, not just the drop area. Remove the per-grid duplicate handlers.
  - Accepted formats, in priority order: `FileDrop` (use the first item), `UniformResourceLocatorW`/`UniformResourceLocator`
    (browser link and address-bar drags), and `UnicodeText`/`Text`.
  - **The destination is now a `TextBox`, and WPF text boxes insert dropped text at the caret by default.** Handle
    `PreviewDragEnter`, `PreviewDragOver`, and `PreviewDrop` on the field (or the whole view) and set `e.Handled = true`, so a drop **replaces**
    the destination instead of inserting into it. The current code's `TextboxDestination_PreviewDragOver` exists for the same reason.
  - `DragEnter`/`DragOver` set the **Drag hover** state and `DragDropEffects.Link` or `Copy`. `DragLeave` and `Drop` clear it.
    Unsupported data shows the "no drop" cursor and no hover state.
  - On drop, set the field text and resolve with `DestinationResolver.ResolveAsync` (KC-02). On success, move focus to the name field.
    On failure, show the **Invalid destination** state.
  - If several files are dropped, use the first one. Document this; don't error.
  - Keep `SetForegroundWindow` activation so dropping from Explorer brings keycuts forward.
  - Ignore drops while **Saving**.
- **Paste and typing (inline destination field)**
  - The field is a normal `TextBox` bound to `CreatorViewModel.DestinationText`. Ctrl+V, the context menu Paste, and typing all work natively.
  - **Paste:** intercept `DataObject.Pasting` (or `CommandManager.PreviewExecuted` for `ApplicationCommands.Paste`) on the field.
    - If the clipboard holds files (`FileDrop`, for example a file copied in Explorer), replace the field with the first path.
      A plain `TextBox` wouldn't paste files on its own.
    - If the paste replaces the whole field (the field was empty or all selected), resolve immediately. On success, move focus to the name field,
      the same as a drop.
  - **Typing:** resolve with a short debounce (~300ms) after the last keystroke to update the type icon. Don't show the invalid state
    while the user is still typing. Show it on `LostFocus`, on Enter, or on Create.
  - **Enter in the destination field:** resolve; if valid, move focus to the name field. It never creates directly.
  - Trim whitespace, quotes, and line breaks from pasted values (KC-02 handles this; make sure the field shows the normalized value after resolving).
  - After a successful resolve, the field shows the normalized destination, for example a `.lnk` replaced by its target or `amazon.com` by `https://amazon.com/`.
    Put the caret at the end so the meaningful tail of a long path is visible.
  - **Convenience:** Ctrl+V while focus is anywhere in the creator *except* the two text fields (for example on Browse or the footer) pastes into the
    destination field as above.
- **Browse ▾ menu** (native dialogs)
  - The Browse button toggles the menu. Expose it to UI Automation as an expand/collapse control (the WPF counterpart of the prototype's `aria-haspopup`).
    Implement the menu as a themed `ContextMenu` or `Popup` anchored to the button, right-aligned.
  - **File…** uses `Microsoft.Win32.OpenFileDialog` and **Folder…** uses `Microsoft.Win32.OpenFolderDialog`, which is built into WPF on .NET 8+.
    Both dialogs are owned by the main window. Implement them as `BrowseFileCommand` and `BrowseFolderCommand`.
  - Keyboard: Enter, Space, or Alt+Down opens the menu. Arrows move between items, Enter activates, and Escape closes and returns focus to Browse.
    The menu also closes when the user clicks elsewhere or picks an item.
  - The selection goes through the same resolver. Cancel leaves the state unchanged.
- **Replacing a destination:** dropping, pasting, or browsing again replaces the current destination and keeps any typed name.
- **Focus:** after any successful drop, whole-field paste, or Browse selection, focus the name field and place the caret at the end.

## Acceptance criteria
- [ ] Each of these sets the correct type icon: a file and a folder dragged from Explorer, a `.lnk` (resolves to its target),
      a `.url` file, a URL dragged from Edge/Chrome (address-bar lock icon and an in-page link), and pasted or typed URLs and paths.
- [ ] Dropping onto the destination field replaces its text and doesn't insert at the caret.
- [ ] Pasting a file copied in Explorer (Ctrl+C on the file) sets its path.
- [ ] Typing a path or URL updates the icon after a short pause and doesn't show an error mid-typing. An invalid value shows the error on blur or Enter.
- [ ] Dropping a deleted file, or a path the user can't access, shows the invalid state with a specific message.
- [ ] Browse ▾ → File… and Browse ▾ → Folder… work and the dialogs can be cancelled. The menu is fully keyboard operable.
- [ ] Ctrl+V inside the name field still pastes text into the name normally.
- [ ] The Explorer right-click startup argument still pre-fills the destination (KC-08).
- [ ] Focus moves to the name field after every successful drop, whole-field paste, and Browse.

## Out of scope
A second destination field. Paste items in the Browse menu (the UI2 design has only File… and Folder…).

## Implementation notes

**Status:** implemented and unit-tested; no acceptance box is ticked because each one needs real drag, clipboard, dialog or keyboard interaction that couldn't be driven headlessly (see manual QA). The GUI was smoke-launched with an Explorer-style argument (`C:\Windows\System32`) and a temp `KEYCUTS_OUTPUT_FOLDER`; it started and was closed.

**Files changed**
- `keycuts.GUI/DragDrop.cs` — rewritten as a static helper: `TryGetDestination` (FileDrop first item → `UniformResourceLocatorW` → `UniformResourceLocator` → `UnicodeText`/`Text`), `TryGetFirstFile`, `HasSupportedData`, `DecodeUrlData`.
- `keycuts.GUI/Views/CreatorView.xaml(.cs)` — whole view is the drop target via `PreviewDragEnter/Over/Leave/Drop` (handled, so text boxes never insert at the caret); per-grid and destination-field handlers removed; paste handler, 300 ms typing debounce, Enter/LostFocus/Ctrl+V handling, native dialogs, Browse menu keyboard support.
- `keycuts.GUI/Controls/MenuButton.cs` (new) — Button subclass whose automation peer implements `IExpandCollapseProvider` (Browse).
- `keycuts.GUI/ViewModels/CreatorViewModel.cs` — `BrowseFileCommand`/`BrowseFolderCommand` (+ `BrowseFileRequested`/`BrowseFolderRequested` events; the view owns the dialogs), `ApplyResolved(result, showError)`.
- `keycuts.GUI/MainFormLogic.cs` — `ActivateThisWindow` made public (drop keeps `SetForegroundWindow`).
- `keycuts.Tests/CreatorInputTests.cs` (new) — data-format priority, URL decoding, `ApplyResolved`, error visibility, Browse commands.

**Decisions**
- Drops/pastes/Browse set the raw text, then call `DestinationResolver.ResolveAsync`; valid → field shows the normalized destination, caret at end, focus to name (caret at end). Invalid → Invalid destination state (`ShowError`). Several dropped files: first one only, silently.
- Typing: 300 ms debounce sets only the kind/icon (text is not rewritten mid-typing, no error). Error + normalization happen on LostFocus, Enter, drop, paste, Browse. Enter in the destination never creates.
- Paste: copied files replace the whole field with the first path; text replaces the field and resolves immediately only if the field was empty/fully selected; partial pastes stay native and go through the debounce.
- Ctrl+V outside the two text boxes pastes into the destination (view `PreviewKeyDown`).
- Drag effect: Link if allowed, else Copy; unsupported data → `None` and no hover. Ignored while Saving.
- Stale resolves are discarded (version counter + text comparison).
- `OpenFileDialog.DereferenceLinks=false` so the resolver (not the dialog) resolves `.lnk` targets.
- Explorer startup path (`MainFormLogic.HandleNewDestination`) is unchanged.

**Deviations:** none. `CanCreate` still only needs non-error text plus a name (KC-11 owns submit-time validation); Create clicked before the debounce resolves is KC-11's concern.

**Manual QA still needed**
- Drag from Explorer (file, folder, `.lnk`, `.url`), from Edge/Chrome (address-bar lock icon, in-page link), and multi-file drag; confirm icon, replace-not-insert, hover state, no-drop cursor on unsupported data, and window activation.
- Drop a deleted file / inaccessible path → specific message.
- Ctrl+C a file in Explorer, then Ctrl+V in the field and while focus is on Browse/footer; Ctrl+V in the name field still pastes text.
- Typing: icon updates after a pause, no error mid-typing, error on blur/Enter.
- Browse ▾: mouse toggle (click again closes), Enter/Space/Alt+Down opens, arrows/Enter/Escape (focus returns to Browse), File…/Folder… dialogs and Cancel; Narrator announces expanded/collapsed.
- Explorer right-click startup pre-fills the destination and focuses the name.
