# Internals

Behavior that spans several files. Read the section before changing that area. Per-method detail lives in code comments.

## Window and shell

- One window, three screens (`ShellMode`). The top row is `RailView`: a `RailTabControl` (a `TabControl` showing only headers, so screen readers get a real tab list) plus our own caption buttons. `WindowChrome` makes the bar draggable; tabs and buttons are `IsHitTestVisibleInChrome`. No tab keyboard shortcuts.
- Never maximized: `NoMaximizeHook` strips `WS_MAXIMIZEBOX`, refuses `SC_MAXIMIZE` and undoes `WindowState.Maximized`. The Manager keeps resize edges and side snapping. Resize border is 0 on the Creator and `WindowLayout.GetFrameThickness` on the Manager.
- A selected tab's shadow is its own shape (`Shadow` in `RailTabItem`). A shared effect set from a trigger can't be frozen and fails across test threads.
- Settings gets MainWidth and its content height capped to the work area, so it scrolls. Only the Manager's size is remembered.
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

- `FooterView` is shared by Creator and Manager: count pill (number only; `CountDescription` is its tooltip/automation name, announced via `LiveRegion.Announce`), a `Message` slot (Manager's Undo then status, via `ManagerStatusView`), and the update line, which is the only thing that shrinks or wraps.
- The Target box shows `TargetText` (the `%VAR%` form when Shift is held or the setting is on) only while it isn't focused (`IsTargetEditing`). `Target` always holds the real path.
- Description visibility is `ShowDescription` in HKCU (`IDescriptionVisibilityStore`), written only when the user toggles it. Hidden text is kept but not saved.
- Saving/Created is `NameStatusText` beside the Name label; the helper line under Name takes no room unless it has a warning. Fields stay filled after a create; Created clears after `SuccessDuration` (3 s).
- `HelperText` controls announce their own changes; don't also call `LiveRegion.Announce`.

## Welcome panel

- `WelcomeViewModel` on `CreatorViewModel.Welcome`, wired only in `App.xaml.cs`. Shows when `WelcomeDismissed` is unset or the folder is empty, decided once from the first count.
- Three bubbles (`WelcomeExample`): `g` → google.com, `dl` → Downloads, `kc` → `Environment.ProcessPath`. A bubble fills the Creator's fields; the first is picked only into an empty form. Got it clears fields only if a bubble filled them.
- After a create, offers **Add to PATH** if `UserPathUpdater.IsOnPath` says the folder isn't on PATH (skipped under `KEYCUTS_OUTPUT_FOLDER`).

## Manager

- **Keys.** Rows take no focus; `ManagerView_PreviewKeyDown` handles Up/Down, Page Up/Down, Home/End, Enter, F2, Shift+F2, Delete, Ctrl+C/E/F/Z, Ctrl+Shift+E/O. Delete/Home/End stay with the search box while it has text, Ctrl+C while text is selected. A key while a row's `...` button is focused acts on that row. Ctrl+E on a non-editable row explains in the status bar. Enter on a hyperlink is left to the link.
- **Editing in place.** A click on the name/description of the already-selected row starts rename or description edit (not the click that dismisses a menu, nor within 250 ms of one). Only one edit at a time (`IsEditing`); reloads wait; clicking away saves.
  - `RenameAsync` moves the file and rewrites only `REM <shortcut>`. Refuses taken names (case-only change allowed).
  - `SetDescriptionAsync` rewrites only `REM <description>` (inserting it before the launch line if missing), preserves line breaks, BOM and streams (temp file + `File.Replace`), refuses non-UTF-8 (`NotEditable`) or a changed file (`Changed`), and doesn't touch an unchanged file.
- **Edit form** (`EditWindow`/`EditKeycutViewModel`, opened via `ManagerViewModel.ShowEditForm`). Re-emits the whole file, so `CanEditInForm` accepts only files the emitter writes identically (ignoring header version, `<destination>` tag, `REM <shortcut>` path, missing description). File is read as strict UTF-8 from bytes, not `File.ReadAllText`. `UpdateAsync` refuses a changed file, writes temp + `File.Replace`, renames with `File.Move` first and puts the name back if the swap fails. `KeycutEditTests` pins every corpus file.
- **Delete and Undo.** `CreateDefault()` recycles; `new KeycutRepository(folder)` deletes permanently. Undo is one level, lives as long as the "Deleted" message (8 s), and restores the kept bytes via temp file + no-overwrite `File.Move`. A failed restore keeps the Undo. Delete and restore exclude each other. Permissions and alternate streams aren't restored (accepted). Drives without a bin say so.
- **Order.** After an edit or Undo, `SortLikeTheFolder` keeps `allRows` in folder order so the next reload doesn't rebuild rows under the focus.
- **Column widths.** Name, Description, Target are draggable star columns; Type, Modified, menu are fixed. DataGrid traps:
  1. A resized star column keeps star width and columns to its right give way. Don't set `CanUserResize="False"` on Target.
  2. The last star column's right edge can't move; `ManagerHeaderPinnedRight`/`ManagerHeaderFixed` collapse those gripper hosts via the header's `Tag`.
  3. Fixed columns need `MinWidth` = `Width`, or the grid squeezes them to 20 and never restores them.
  4. After Modified hides or returns, `RefillStarColumns` restores star widths.
  5. Double-click on a divider would auto-size to content; the `ColumnGripper` style swallows `PreviewMouseDoubleClick`.
  Widths aren't remembered; if they ever are, store star proportions.
- Status messages last 3 s (`StatusMessageDuration`), errors 6 s.

## Target check (Missing / Warning / Unknown)

- Missing = a specific path was looked for and isn't there. Anything uncertain is Unknown (no label).
- `CheckTargetsAsync` checks `Keycut.Program` (or the target) with env vars expanded, records `CheckedPath`/`CheckedDetail` for the tooltip.
- A bare program name is looked up the way Windows does via `NameConflicts.Find`. For `START "" /D "<folder>" <program>`, see `CheckStartIn`: a program found by name needs the folder to exist; one only in App Paths works anyway (→ Warning).
- Text that can't be a path, or an extension not in PATHEXT, is Unknown.

## Name warnings

- `NameConflicts.Find` (pure; inputs via `NameConflictEnvironment`) lists what else a name opens from Win+R, in ShellExecuteEx order: Windows folder, System32, machine then user PATH (with PATHEXT), App Paths. Before the output folder hides the keycut, after it is shadowed.
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
- `ShortcutIcons.PointAt` repoints `.lnk` files (Start menu, desktop, pinned taskbar) whose target is the running exe to `icons\<name>.ico`, then `SHChangeNotify`. Runs at start too. The exe's and MSI's own icons are fixed (Porcelain).
- The picker overlay disables the page and the tabs (`AreTabsEnabled`), not the caption buttons.

## Install, PATH, CLI

- PATH gets only the install's `bin\` (`BINFOLDER`), holding `keycuts.cmd` and `keypaste.cmd` (CRLF). `keycuts.exe` can't move there: a self-contained apphost needs its runtime beside it.
- `OUTPUTFOLDER` is an MSI directory (trailing backslash, trimmed by `GetOutputFolder`); default from `[%USERPROFILE]`, matched to `Runner.DefaultOutputFolder` by a test.
- `UserPathUpdater` is a no-op when old and new folders match; the installer first adds the folder. Machine PATH entries are left alone.
- CLI help: `-h`/`--version` → stdout, exit 0; parse errors → stderr, exit 1. `paste` sets the clipboard via Win32 (no WinForms).
- Windows 11 shows the verbs under "Show more options"; top-level needs a packaged `IExplorerCommand`.
