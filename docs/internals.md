# Internals

Behavior that spans several files. Read the section before changing that area. Per-method detail lives in code comments.

## Window and shell

- One window, three screens (`ShellMode`). The top row is `RailView`: a `RailTabControl` (a `TabControl` showing only headers, so screen readers get a real tab list) plus our own caption buttons. `WindowChrome` makes the bar draggable; tabs and buttons are `IsHitTestVisibleInChrome`. No tab keyboard shortcuts.
- Ctrl+1, Ctrl+2 and Ctrl+3 pick the tabs (`MainWindow.Main_KeyDown`, on the bubbling `KeyDown`, so a control that wants the key keeps it); not while `AreTabsEnabled` is off.
- Never maximized: `NoMaximizeHook` strips `WS_MAXIMIZEBOX`, refuses `SC_MAXIMIZE` and undoes `WindowState.Maximized`. The Manager and Settings keep resize edges and side snapping. Resize border is 0 on the Creator and `WindowLayout.GetFrameThickness` on the Manager and Settings.
- A selected tab's shadow is its own shape (`Shadow` in `RailTabItem`). A shared effect set from a trigger can't be frozen and fails across test threads.
- Two sizes (`MainWindow.ApplyMode`). Small, the Creator: MainWidth, `SizeToContent.Height`, no resize border. Large, the Manager and Settings: one shared, resizable size (`largeSize`, 960 × 540 until the window first leaves it), clamped to the work area. Switching between the two large screens touches nothing, so resizing either resizes both; Settings never grows the window and scrolls instead. The window keeps its top-left corner both ways: a Manager moved elsewhere leaves the Creator there too.
- Settings is two columns (`SettingsView.UseTwoColumns`) at `TwoColumnMinWidth` (840) and up; narrower, the right column moves under the left one.
- The shell numbers its reloads; only the latest may set the count or reach the welcome panel. A count-only reload cancels a running Manager load (`ManagerViewModel.CancelLoad`).
- Tests: tab bar names are in `RailView`'s name scope (not `Window.FindName`); a `TabItem`'s automation peer comes from the tab list's peer.

## Settings

- `SettingsView` over `SettingsViewModel`, created fresh by the shell's `settingsFactory` (wired in `App.xaml.cs`; a shell without one can't open Settings).
- Every change is stored at once. `ISettingsStore.SaveOptions` stores everything but the folder; `SaveOutputFolder` stores the folder alone and returns `FolderSaveResult`. A partial failure (PATH, menu) shows as `SaveError`. A folder that wasn't stored stays as an uncommitted edit.
- Folder commits on Enter, focus loss or the folder picker (`CommitOutputFolder`). Escape in the box reverts an uncommitted edit (`RevertOutputFolder`); otherwise Escape closes the icon picker, else returns to the Creator (`ShellViewModel.HandleEscape`).
- Events `Stored`, `OutputFolderChanged`, `ShowWelcomeRequested`, `UpdateChecked` keep the shell in step.
- Restore defaults (`Settings.Defaults()`) confirms in `ConfirmWindow`, never touches the folder or PATH.
- The right-click toggle shows whether the verbs exist, not the stored `RightClickContextMenu` value (which only records the choice so an MSI upgrade doesn't re-add a removed menu).
- `ShowVersionRow = false` hides the version row in screenshots.

## Creator and footer

- `FooterView` is shared by the screens and is collapsed (its `Bar`, bound to `UpdateNotice.IsVisible`) unless a newer version is out; it holds only the update line. The Manager's status and Undo are `ManagerStatusView` in the search row, collapsed while `StatusMessage` is empty and `CanUndoDelete` is off. The count is `ManagerViewModel.CountText` in a pill beside the search box ("of" only while `SearchText` isn't blank, since the shell can set the total before the rows reload), announced via `LiveRegion.Announce` when it changes.
- The Target box shows `TargetText` (the `%VAR%` form when Shift is held or the setting is on) only while it isn't focused (`IsTargetEditing`). `Target` always holds the real path.
- The description box is always shown (2026-11-17; the old `ShowDescription` value in HKCU is no longer read).
- Saving/Created is `NameStatusText`, under the name box in the Creator (beside the Name label in the Manager's create row); the helper line under Name takes no room unless it has a warning. Fields stay filled after a create; Created clears after `SuccessDuration` (3 s).
- `HelperText` controls announce their own changes; don't also call `LiveRegion.Announce`.
- **Two forms.** The Creator and the Manager's create row are each a `CreatorViewModel` (`ShellViewModel.Forms`); the row's is `ManagerViewModel.CreateRow`, with `DescriptionAlwaysShown` and `ClearsAfterCreate`. What the app reads once (the environment-variables setting, the path variables) goes to both. Their views attach a `CreatorInput` (drop, paste, Ctrl+V, Browse, resolve while typing, the name check, Shift, the confirm dialog), which lets go of the view model while the view is unloaded, so a hidden view never answers a Browse. Shared styles are in `Views/CreatorForm.xaml`; the drop area's triggers find an element named `DropArea` in the view.
- **The Manager's create row.** `CreateRowView`, in a `ContentControl` with an implicit template, so a Manager without a `CreateRow` (tests) builds none. `TargetEnterCreates`: Enter in its target resolves, then creates if the name is filled, else moves to the name. `ManagerView_PreviewKeyDown` ignores keys from inside the row except Ctrl+F. A create there reloads the table (the shell listens to `KeycutCreated`). The Manager opens with the focus in the search box, not the row.

## Welcome panel

- `WelcomeViewModel` on `CreatorViewModel.Welcome`, wired only in `App.xaml.cs`. Shows when `WelcomeDismissed` is unset or the folder is empty, decided once from the first count.
- Three bubbles (`WelcomeExample`): `g` → google.com, `dl` → Downloads, `kc` → `Environment.ProcessPath`. A bubble fills the Creator's fields; the first is picked only into an empty form. Got it clears fields only if a bubble filled them.
- After a create, offers **Add to PATH** if `UserPathUpdater.IsOnPath` says the folder isn't on PATH (skipped under `KEYCUTS_OUTPUT_FOLDER`).

## Manager

- **Keys.** Rows take no focus; `ManagerView_PreviewKeyDown` handles Up/Down, Page Up/Down, Home/End, Enter, F2, Shift+F2, Delete, Ctrl+C/E/F/Z, Ctrl+Shift+E/O. Delete/Home/End stay with the search box while it has text, Ctrl+C while text is selected. Shift+F10 and the Menu key open the selected row's menu under the row, and stay with the search box while it has text too. WPF asks for a menu when the key is released (`ContextMenuOpening` without a pointer position, on the focused element), so `ManagerView_ContextMenuOpening` answers that; a menu opened on the key press would be closed by it. The search box has an explicit Cut/Copy/Paste menu, or WPF's text box would open its built-in one first. Ctrl+E on a non-editable row explains in the status line (in the search row). Enter on a hyperlink is left to the link.
- **Editing in place.** A click on the name/description of the already-selected row starts rename or description edit (not the click that dismisses a menu, nor within 250 ms of one). Only one edit at a time (`IsEditing`); reloads wait; clicking away saves.
  - `RenameAsync` moves the file and rewrites only `REM <shortcut>`. Refuses taken names (case-only change allowed).
  - `SetDescriptionAsync` rewrites only `REM <description>` (inserting it before the launch line if missing), preserves line breaks, BOM and streams (temp file + `File.Replace`), refuses non-UTF-8 (`NotEditable`) or a changed file (`Changed`), and doesn't touch an unchanged file.
- **Edit form** (`EditWindow`/`EditKeycutViewModel`, opened via `ManagerViewModel.ShowEditForm`). Re-emits the whole file, so `CanEditInForm` accepts only files the emitter writes identically (ignoring header version, `<destination>` tag, `REM <shortcut>` path, missing description). File is read as strict UTF-8 from bytes, not `File.ReadAllText`. `UpdateAsync` refuses a changed file, writes temp + `File.Replace`, renames with `File.Move` first and puts the name back if the swap fails. `KeycutEditTests` pins every corpus file.
- **Delete and Undo.** `CreateDefault()` recycles; `new KeycutRepository(folder)` deletes permanently. Undo is one level, lives as long as the "Deleted" message (8 s), and restores the kept bytes via temp file + no-overwrite `File.Move`. A failed restore keeps the Undo. Delete and restore exclude each other. Permissions and alternate streams aren't restored (accepted). Drives without a bin say so.
- **Row menu.** No `...` column: each `DataGridRow` gets its own copy of the unshared `RowMenu` resource (`Table_LoadingRow`), so its bindings follow the row through recycling. A right-click opens it at the pointer and selects the row; the keyboard path places it with `MenuPlacement.UnderLeftEdge` and `RowMenu_Closed` puts it back to `MousePoint`. After a non-destructive item or Escape, the focus goes to the search box.
- **Order.** After an edit or Undo, `SortLikeTheFolder` keeps `allRows` in folder order so the next reload doesn't rebuild rows under the focus.
- **Column widths.** Name, Description, Target are draggable star columns; Type, Modified, Runs, Last run are fixed. DataGrid traps:
  1. A resized star column keeps star width and columns to its right give way. Don't set `CanUserResize="False"` on Target.
  2. The last star column's right edge can't move; `ManagerHeaderPinnedRight`/`ManagerHeaderFixed` collapse those gripper hosts via the header's `Tag`.
  3. Fixed columns need `MinWidth` = `Width`, or the grid squeezes them to 20 and never restores them.
  4. After Modified hides or returns, `RefillStarColumns` restores star widths.
  5. Double-click on a divider would auto-size to content; the `ColumnGripper` style swallows `PreviewMouseDoubleClick`.
  6. A column that starts hidden builds its header off the visual tree, and a `RelativeSource FindAncestor` binding there fails for good. Header bindings to the Manager go through `ManagerProxy` (a `BindingProxy` in the view's resources).
  Widths aren't remembered; if they ever are, store star proportions.
- Status messages last 3 s (`StatusMessageDuration`), errors 6 s.

## Run counts (#187)

- keycuts isn't running when a keycut runs, so the `.bat` counts itself: `UsageLog.BatLines`, after the launch line and before `EXIT`, append one line to `%LOCALAPPDATA%\keycuts\usage\%~n0.runs`. Count = number of lines (`UsageLog.CountRuns`), last run = the file's write time. No `%DATE%` parsing, concurrent runs both count, and `%~n0` keeps counting under a name changed in Explorer.
- Both lines fail silently (the second is parenthesized so `2>NUL` covers the `>>` itself). The `.bat` keeps the launch's exit code: `ECHO` and a failed redirection leave ERRORLEVEL alone, and when the folder is missing, `MD` is followed by `"%ComSpec%" /C EXIT %ERRORLEVEL%` (expanded before `MD` runs) to put the code back. `UsageTests` runs the lines through real `cmd` with `LOCALAPPDATA` pointed at a temp folder. Cost (#240, Console Host, `ShellExecuteEx`): ~3 ms more console on screen (72 → 75 ms median), inside the noise; the first run, with the folder missing, ~30 ms once.
- Parser: neither line is a launch line. `CanEditInForm` drops exact `BatLines` from both sides, so files with and without them stay editable; saving the form adds them.
- Older keycuts: `KeycutRepository.AddRunCounting` inserts the lines before a final `EXIT`, byte for byte (Latin-1 round trip keeps BOM, ANSI bytes, line breaks), restores the last write time, and skips anything else (no final `EXIT`, unparseable, read-only). The GUI runs it once per folder (`AddRunCountingOnce`), at startup and when Settings commits a new folder (`ShellViewModel.PrepareOutputFolder`, before the reload, #238). Every folder done is in `RunCountingFolders` (`REG_MULTI_SZ` in HKCU; the older single `RunCountingFolder` still counts as done), so lines removed by hand stay removed, even after switching away and back.
- `Keycut.Runs`: null = no record (file lacks the lines), 0 = counts but never ran. Only repositories built with a `UsageLog` read counts (`CreateDefault`); tests pass a temp one.
- Rename and the Edit form's rename move the record (replacing a leftover under the new name); delete removes it into `DeletedKeycut.Usage` and Undo restores it with its time. A brand-new keycut clears a leftover record for its name (`Runner.Usage`, set only by the CLI and `CreateOptions.Usage` from the GUI); overwriting keeps the count.
- Manager: Runs and Last run columns, sorted most/latest first. Modified makes way first (table < 1040), then Runs and Last run (< 780). Their headers' tooltip and help text say "Counting since <date>" (`CountingSinceText`, from `UsageLog.CountingSince`, the usage folder's creation time, read on each load; nothing before it exists), via `SortHeaderButton.Note` (#235).

## Target check (Missing / Warning / Unknown)

- Missing = a specific path was looked for and isn't there. Anything uncertain is Unknown (no label).
- `CheckTargetsAsync` checks `Keycut.Program` (or the target) with env vars expanded, records `CheckedPath`/`CheckedDetail` for the tooltip.
- A bare program name is looked up the way Windows does via `NameConflicts.Find`. For `START "" /D "<folder>" <program>`, see `CheckStartIn`: a program found by name needs the folder to exist; one only in App Paths works anyway (→ Warning).
- Text that can't be a path, or an extension not in PATHEXT, is Unknown.

## Name warnings

- `NameConflicts.Find` (pure; inputs via `NameConflictEnvironment`) lists what else a name opens from Win+R, in ShellExecuteEx order: Windows folder, System32, machine then user PATH, App Paths, each folder in full before the next. Before the output folder hides the keycut, after it is shadowed.
- Win+R ignores PATHEXT (#209, checked through the real Run dialog and ShellExecuteEx, which agree): it tries `.com`, `.exe`, `.bat`, `.lnk`, `.cmd` in that order (`RunDialogExtensions`), and never runs `.js`, `.vbs` or `.ps1` from a bare name. So `name.exe` beside a keycut hides it; `name.lnk`/`name.cmd` are shadowed. The target check passes `NameLookup.Cmd` instead, since a keycut's `START` runs under cmd, which does use PATHEXT.
- `NameCheck.Run` adds the existing keycut while "Ask before replacing" is on. GUI asks via `ConfirmWindow`; the CLI via `CreateConfirmation.Decide`, exiting 14 with redirected input.
- `AskBeforeReplacing` in HKCU; if unset, the old `ForceOverwrite` decides, inverted.
- `keycuts` and `keypaste` (`CommandNames`) are refused; `MoveAsideCommandNames` renames existing ones to `<name>_previous` at startup.

## Parser fallback

When REM type/target lines are missing or invalid, the launch line is tried in order: emitter's `cmd /C`, CLSID, folder, hosts, `START "" /D "<folder>" <program> [args]` (`TryReadStartInFolder`), file, command, URL.

- For the `/D` form, a quoted program with only quoted args joins into one path. Any unquoted program or argument gives `<folder>\<program>` as the target (not editable); the rest goes to `Arguments`/`Command` and the Target tooltip.
- Programs starting with `/`, unquoted ones containing `& | < > ^`, and cmd built-ins stay Commands.
- `Program`, `Arguments`, `StartInFolder`, `ProgramName` say where the program ends; they're empty for a quoted File target no pattern can split.
- `ShortcutFile.Unquote` removes exactly one pair of quotes; don't add another trim.
- The parser reads launch lines as written; it doesn't unescape.
- The hosts file is identified only by `Shortcut.IsHostsFile`.

## Update check

- `UpdateChecker` asks `GitHubReleaseSource` for `/releases/latest` at most once a day on GUI start. GitHub needs a `User-Agent`.
- Stored in HKCU: `LastUpdateCheck`, `DismissedRelease`, and `LatestReleaseSeen` as one `REG_MULTI_SZ` (version, URL), so one write is atomic. Anything but two non-empty lines counts as never checked.
- Compare three parts (`UpdateChecker.Normalize`); `Runner.Version` has four.
- A release URL that isn't an https page of this repo (`IsReleasePage`) becomes the releases page.

## App icon

- `AppIconChoice` saved as `AppIcon` (unknown → default). Black & White and Accent follow the theme; high contrast picks by window color. Names and descriptions in `Themes/AppIcons` are the owner's wording, pinned by a test.
- `AppIconManager` sets `Window.Icon` on load and on theme change. Applies and saves immediately (`IAppIconService`).
- **Transparent icon background** (`AppIconTransparent`, a DWORD, off unless 1) swaps the drawing for its own without a background (`AppIcons.GetTransparent`: Porcelain → `key-charcoal`, Onyx → `key-white`, Cobalt and Twilight → `key-blue`, Classic → `classic-transparent`) on the windows and the shortcuts. Saved at once through `IAppIconService` like the choice; restore defaults turns it off. The picker's previews show the transparent drawings while it is on. **The tab bar always shows the transparent one** (`AppIconManager.TabBar`), whatever the setting.
- `ShortcutIcons.PointAt` repoints `.lnk` files (Start menu, desktop, pinned taskbar) whose target is the running exe to `icons\<name>.ico`, then `SHChangeNotify`. Runs at start too. The exe's and MSI's own icons are fixed (Porcelain).
- The picker overlay disables the page and the tabs (`AreTabsEnabled`), not the caption buttons.

## Install, PATH, CLI

- PATH gets only the install's `bin\` (`BINFOLDER`), holding `keycuts.cmd` and `keypaste.cmd` (CRLF). `keycuts.exe` can't move there: a self-contained apphost needs its runtime beside it.
- `OUTPUTFOLDER` is an MSI directory (trailing backslash, trimmed by `GetOutputFolder`); default from `[%USERPROFILE]`, matched to `Runner.DefaultOutputFolder` by a test.
- `UserPathUpdater` is a no-op when old and new folders match; the installer first adds the folder. Machine PATH entries are left alone.
- CLI help: `-h`/`--version` → stdout, exit 0; parse errors → stderr, exit 1. `paste` sets the clipboard via Win32 (no WinForms).
- `list` (#173's first verb, #237): loads through `KeycutRepository` with `UsageLog.CreateDefault()` like the GUI, and `KeycutListing` filters (name, type, target, description), sorts (the Manager's orders) and lines up the columns. `--usage` adds Runs and Last run (ISO local time); a dash is no record. Parser logging goes to stderr while loading, so stdout is only the table. An unreadable folder exits 3 (`CannotFindPath`).
- Windows 11 shows the verbs under "Show more options"; top-level needs a packaged `IExplorerCommand`.
