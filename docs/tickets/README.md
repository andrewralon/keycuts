# Keycuts UI redesign — tickets

Implements the new compact **keycut creator** and the in-window **Keycut Manager** in the production
WPF app (`keycuts.GUI`), using the Figma Make prototype as the visual/interaction spec.

- Visual spec (current): [`Figma Redesign Keycuts UI2/src/App.tsx`](<../../Figma Redesign Keycuts UI2/src/App.tsx>)
  (components `MainWindow`, `BatmanagerPanel`) and
  [`src/index.css`](<../../Figma Redesign Keycuts UI2/src/index.css>) (tokens, sizes, badge colors).
  UI2 supersedes `Figma Redesign Keycuts UI`. It adds the inline destination field (paste/type) and the Browse ▾ File…/Folder… menu.
  The manager, tokens, and icons are unchanged between the two.
- Icons: [`Figma Redesign Keycuts UI2/public/icons/`](<../../Figma Redesign Keycuts UI2/public/icons/>).
- The prototype is **React**; production is **WPF on .NET 10**. Port the look and behavior, not the code.
  Ignore the prototype's preview-only chrome (sidebar, state tabs, fake title bar, style guide, mock timers).

## Current production architecture (as of this breakdown)

| Area | Where | Notes |
| --- | --- | --- |
| Creator window | `keycuts.GUI/MainWindow.xaml(.cs)`, `MainFormLogic.cs`, `DragDrop.cs` | Code-behind + `INotifyPropertyChanged` on the window. Step labels 1-2-3, free-text destination `TextBox`, `MessageBox` for errors/overwrite, `Thread.Sleep` + beep on create, global Enter = create, Escape = close. |
| Create logic | `keycuts.GUI/CommonLogic.cs` → `keycuts.Common/Runner.cs` | `Runner.Run` writes a `.bat` to the output folder (registry setting) and updates PATH. Returns `ExitCode`. |
| Type detection | `keycuts.Common/Shortcut.cs` `GetShortcutTypeFromDestination` | Handles URL, `.url`, `.lnk`, CLSID, hosts file. **Throws** for missing paths (`File.GetAttributes`). |
| Reading keycuts | `keycuts.Common/ShortcutFile.cs` | Regex-parses the `.bat` command line into `Type`/`Destination`. No description field. |
| Manager | `keycuts.Batmanager` (separate exe) launched via `Runner.OpenBatmanager()` | Plain `DataGrid`, context menu Run/Edit (Notepad)/Open location/Copy/Delete. Being retired (KC-20). |
| Settings | `keycuts.GUI/SettingsWindow.xaml`, `Settings.cs`, `RegistryStuff.cs` | Output folder, force overwrite, right-click menus. Not redesigned here. |
| Startup args | `keycuts.GUI/App.xaml.cs` | Explorer "keycut this!" context menu passes the destination as `args[0]`. Must keep working. |
| Tests / CI / format config | — | None exist. |

## Ticket list

| # | Ticket | Area | Depends on |
| --- | --- | --- | --- |
| 01 | [Test project and verification commands](KC-01-test-project-and-verification.md) | Infra | — |
| 02 | [Destination detection and validation service](KC-02-destination-detection-and-validation.md) | Common | 01 |
| 03 | [Keycut repository and description metadata](KC-03-keycut-repository-and-description.md) | Common | 01 |
| 04 | [Create-keycut service with structured results](KC-04-create-keycut-service.md) | Common | 02, 03 |
| 05 | [Design tokens and light/dark themes](KC-05-design-tokens-and-themes.md) | GUI | — |
| 06 | [Icon library](KC-06-icon-library.md) | GUI | 05 |
| 07 | [Type badge control](KC-07-type-badge-control.md) | GUI | 05, 06 |
| 08 | [Single-window shell and view switching](KC-08-app-shell-and-view-switching.md) | GUI | 05 |
| 09 | [Creator: layout and visual states](KC-09-creator-layout-and-states.md) | GUI | 06, 08 |
| 10 | [Creator: drag-and-drop, paste, and Browse](KC-10-creator-input-drop-paste-browse.md) | GUI | 02, 09 |
| 11 | [Creator: Enter-to-create, saving, errors, success](KC-11-creator-submit-flow.md) | GUI | 04, 10 |
| 12 | [Manager: layout, table, and states](KC-12-manager-layout-and-table.md) | GUI | 03, 07, 08 |
| 13 | [Manager: search and sort](KC-13-manager-search-and-sort.md) | GUI | 12 |
| 14 | [Manager: row actions menu](KC-14-manager-row-actions-menu.md) | GUI | 12 |
| 15 | [Manager: Edit action](KC-15-manager-edit-action.md) | GUI | 14 |
| 16 | [Sample and long-list development data](KC-16-sample-and-long-list-data.md) | Dev | 03 |
| 17 | [Keyboard, focus, and accessibility pass](KC-17-keyboard-focus-accessibility.md) | GUI | 11, 13, 14 |
| 18 | [QA verification, docs, and cleanup](KC-18-qa-verification-and-cleanup.md) | Release | all |
| 19 | [Design: Browse file vs folder, and paste](KC-19-design-browse-and-paste.md) — **resolved in UI2** | Design | — |
| 20 | [Retire the Batmanager app](KC-20-retire-batmanager.md) | Cleanup | 12, 14, 15 |

Suggested order (sequential): **01 → 02 → 03 → 04 → 05 → 06 → 07 → 08 → 16 → 09 → 10 → 11 → 12 → 13 → 14 → 15 → 20 → 17 → 18**.
Implementing agents: start with [HANDOFF.md](HANDOFF.md). The orchestrator prompt is in [AGENT-PROMPT.md](AGENT-PROMPT.md).

## Decisions

| # | Topic | Decision | Tickets |
| --- | --- | --- | --- |
| 1 | Description | Stored as an optional `REM <description>…</description>` comment line in the keycut `.bat` file. Existing files without it show an empty cell. | KC-03, KC-12 |
| 2 | Edit | Opens the keycut `.bat` in Notepad, the same as Batmanager today. No in-app edit form. Editing the description comment in Notepad is how users set a description. | KC-15 |
| 3 | Browse: file vs folder | **Browse ▾** opens a menu: **File…** (*Choose a single file*) / **Folder…** (*Choose a directory*). Per UI2. | KC-09, KC-10 |
| 4 | Paste | The destination line is an inline, borderless text field (placeholder "Drop or paste a file, folder, or URL here"). Users can paste or type into it. Per UI2. | KC-09, KC-10 |
| 5 | Force overwrite | When on, duplicate names are overwritten silently with no confirmation. When off, the creator shows the inline duplicate-name error. | KC-04, KC-11 |
| 6 | Theme | Follow the Windows light/dark app setting and switch live. No in-app toggle. High contrast falls back to system colors. | KC-05 |
| 7 | Batmanager | Retire it. Remove the project, its launch code, and the GUI button once the new manager reaches parity. | KC-20 |
| 8 | Delete | No confirmation. Delete from the row menu removes the keycut immediately. | KC-14 |
| 9 | Name form | Always visible (UI2). Create is disabled until there's a valid destination and a name. | KC-09, KC-11 |

## Global out of scope

Whole-row click behavior, advanced filtering, printing or reference sheets, extra manager actions,
a second destination field, multi-step creation indicators, and a Settings window redesign.
