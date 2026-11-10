# KC-21 — Settings window redesign

**Area:** keycuts.GUI · **Depends on:** 05, 06, 08, 17

## Why
The UI redesign left Settings out ("Not redesigned here" in [README.md](README.md)). It still used the old layout: a
hard-coded 18pt font, proportional rows, plain checkboxes, a Save button that neither closed the window nor confirmed
anything, and errors in a `MessageBox`.

## Design
[`docs/design-reference/src/App.tsx`](<../design-reference/src/App.tsx>) (`SettingsWindow`, `Toggle`) and
[`src/index.css`](<../design-reference/src/index.css>) (`.settings-window`, `.settings-section`, `.setting-row`, `.toggle`,
`.section-label`, `.section-heading`, `.field-row`, `.field-message`, `.settings-footer`). Port the look and behavior only.
Leave out the mock data, the fake title bar and the `state` preview switch.

- 440px wide, 24px padding.
- **Header:** H1 "Settings", subtitle "Choose how keycuts creates your shortcuts."
- **OUTPUT:** accent eyebrow; "Output folder" label; text box (placeholder "Choose where shortcuts are saved"), **Choose**
  (folder icon, `OpenFolderDialog` seeded with the current value) and **Launch** (external icon, opens Explorer; disabled
  when the field is empty or the folder doesn't exist); helper "New .bat files will be saved here."; error state (danger
  border and message) on a validation failure.
- **BEHAVIOR:** top border; setting rows with a label, a note and a right-aligned toggle: "Force overwrite" and
  "Right-click context menus" (note uses the real verb label, “keycut this!”).
- **Footer:** subtle background, top border, right-aligned **Cancel** (`SecondaryButton`) and **Save changes**
  (`PrimaryButton`, check icon).

## Scope
- A reusable `ToggleSwitch` style for `ToggleButton` in `Themes/Controls.xaml`: 36×20 track, 14px thumb, theme tokens only,
  `AppFocusVisual`, Toggle pattern through `ToggleButton`'s automation peer.
- `ViewModels/SettingsViewModel` with an `ISettingsStore` seam over `Runner` (`RunnerSettingsStore`), so tests never touch the registry.
- Validation: not empty; a full path; create it if missing; prove it is writable. Shown inline; the window stays open.
- Keyboard: Enter saves (`IsDefault`), Escape cancels (`IsCancel`, replacing the `KeyDown` handler). Focus starts in the folder field.
- Window: modal with an owner, sizes to content, not resizable, `CenterOwner`, themed title bar (from `ThemeManager`, as for `MainWindow`).
- Keep the registry values and `Runner` calls, the debug output-folder override, and the main window's reload afterwards.
- Known bugs listed in the plan: the wrong `NotifyPropertyChanged` name, `SetRightClickContextMenu` reading the wrong default,
  and Save not closing.

## Acceptance criteria
- [x] Header, OUTPUT, BEHAVIOR and footer match the design in Light, Dark and HighContrast (rendered offscreen at 2× for all three; live Windows theme switch is manual QA).
- [x] `ToggleSwitch` style: 36×20, 14px thumb, tokens only, focus visual, Toggle pattern; every toggle has `AutomationProperties.Name`.
- [x] Choose opens `OpenFolderDialog` seeded with the current folder; cancelling keeps the value (view model tested; the dialog itself is manual QA).
- [x] Launch opens the folder and is disabled when the field is empty or the folder doesn't exist.
- [x] Validation errors (empty, not a full path, can't be created, not writable) show inline with the error border and red message; nothing is saved and the window stays open.
- [x] Save changes validates, saves and closes with `DialogResult = true`; Cancel and Escape close without saving.
- [x] Tab order: folder field, Choose, Launch, Force overwrite, Right-click menus, (environment variables, theme, Show welcome — see notes), Cancel, Save changes. Focus starts in the folder field.
- [x] Same three registry values through `Runner`; the debug override still blocks persistence; the main window reloads only after a save (or Show welcome).
- [x] No hard-coded `FontSize="18"`; window is `NoResize`, `CenterOwner`, sized to content.
- [x] Tests for load/save mapping, the Cancel path, validation, Launch enabled state, the toggle style and window accessibility.

## Out of scope
A packaged Windows 11 top-level context-menu handler, per-setting autosave, and moving settings out of `HKCU`.

## Implementation notes

**Files changed**
- `keycuts.GUI/ViewModels/SettingsViewModel.cs` (new): `ISettingsStore`, `RunnerSettingsStore` (wraps `Settings.LoadSettings`/`SaveSettings`, so
  the registry and `Runner` calls are unchanged), and `SettingsViewModel` (state, validation, `ChooseCommand`, `LaunchCommand`, `Save`, `Saved` event).
  The folder picker, Explorer launch, folder check and `Directory.Exists` are injectable.
- `keycuts.GUI/SettingsWindow.xaml(.cs)`: rewritten to the design; the window is a thin view over the view model. Save calls
  `ViewModel.Save()` and sets `DialogResult = true` on success; Cancel is `IsCancel`; the `KeyDown` handler is gone. A second
  constructor takes a view model for tests.
- `keycuts.GUI/Themes/Controls.xaml`: `ToggleSwitch` style.
- `keycuts.GUI/MainWindow.xaml.cs`: reloads (and refreshes the creator's environment-variables setting) only when the dialog returns true; Show welcome still reloads.
- `keycuts.GUI/MainFormLogic.cs`: removed `SaveSettings`, which had no callers left. `Settings.cs` is kept: the store uses it.
- `keycuts.Tests/SettingsViewModelTests.cs` (new): `SettingsViewModelTests` (20) and `SettingsWindowTests` (13, STA, offscreen).
- `keycuts.Tests/WpfCollection.cs` (new) and `[Collection]` on `AccessibilityTests`: the window tests and the accessibility tests
  share the one `Application` per AppDomain, so they must not create it in parallel (it failed once without this).
- `README.md`, `CLAUDE.md`, `docs/tickets/README.md`, `KC-18`: labels and status.

**Decisions and deviations**
- **Kept three settings the plan didn't mention** (agreed with the user): the theme choice (System/Light/Dark, previewed on click,
  kept by Save) as an APPEARANCE section; "Use environment variables in paths" (#31) as a third BEHAVIOR toggle; and **Show welcome**
  as a secondary button on the left of the footer. The window is about 655px tall instead of about 390px, and the tab order has those
  controls between Right-click menus and Cancel. The theme uses the existing themed radio buttons rather than a new segmented control.
- **The two "known bugs" in `Runner`/`SettingsWindow` were already fixed** in `1385303` ("Fix Settings crash and remove every need for
  elevation"): `RightClickContextMenus` already raised its own name, and `SetRightClickContextMenu` compares with
  `GetRightClickContextMenu()` (whether the verbs exist), not a default. A test pins the property names. Save-doesn't-close is fixed here.
- **Theme preview on open:** checking the saved theme's radio in the constructor used to call `ThemeManager.SetPreference`, and closing
  always re-applied the registry theme, so just opening Settings could swap the palette. Now only a click previews, and closing only
  restores when something was previewed. Tested.
- **Validation messages:** empty → "Choose where shortcuts are saved."; not a full path → the existing `Settings.InvalidOutputFolderMessage`;
  can't create or write → "Choose a folder you have permission to write to." Writability is proven with a `DeleteOnClose` probe file, so nothing
  is left behind. A partial failure from the store (PATH not updated, context menu not changed) shows in red above the footer, and the window stays open.
- **Validation creates a missing folder** before saving, including while the Debug `KEYCUTS_OUTPUT_FOLDER` override is active (the override
  still blocks the registry and PATH; `Runner` uses `NoOpPathUpdater`). Use temp folders when testing with the override.
- The folder box gets a tooltip with the full path, because the box is narrow next to Choose and Launch.
- No letter spacing on the eyebrows (WPF has none), the same as KC-09.

**Review fixes (PR #151, Copilot)**
- **Partial saves refresh the main window.** `SettingsViewModel.StoreChanged` is true once a save reaches the store, even when it then reports
  a PATH or context-menu problem, and `MainWindow` refreshes when it is set, however the dialog closes.
- **Height capped to the screen.** On load the window takes the owner monitor's work-area height as `MaxHeight`
  (`WindowLayout.GetWorkAreaInDips`, moved out of `MainWindow`) and stays on screen. The body is in a `ScrollViewer`, and the footer stays visible.
- **Errors are announced.** The window raises `LiveRegionChanged` on the folder message and the save error when they appear, as the creator does;
  `LiveSetting` alone raises nothing. The folder message is now `Assertive`.
- **Launch's `Directory.Exists` check stays synchronous (declined).** Copilot noted that typing a network path could block the UI. A debounced
  background check was tried, then reverted: the case is rare (someone typing a UNC path into a rarely edited setting, with at worst a brief pause),
  and the async version made the tests timing-dependent (a CI failure and a race in the window tests). Revisit if network output folders become common.

**Follow-up (after #151 merged; Copilot's third review)**
- **Enter on a focused toggle saved nothing; it flipped the toggle.** A bare `ToggleButton` handles Enter, unlike the old checkboxes. The
  `ToggleSwitch` style now sets `KeyboardNavigation.AcceptsReturn = False`, so Enter reaches Save changes and Space still toggles.
- **A save error could be hidden below the scrolled body** when the window is height-capped. The window now scrolls the message into view
  when it announces it.

**Verification**
- `dotnet build keycuts.sln --no-incremental`: 0 errors, 0 warnings (unchanged).
- `dotnet test keycuts.sln`: 683 passed (33 new), repeated clean runs after the collection fix and the review fixes.
- `dotnet format keycuts.sln --verify-no-changes`: clean (also with `--severity error`).
- Rendered the window offscreen in Light, Dark and HighContrast, with and without the error state (throwaway test, not committed).

**Manual QA still needed** (added to KC-18)
- Light, Dark and HighContrast with the real Windows setting: palette, toggle track and thumb, focus rings on the toggles and buttons, the error state, and the themed title bar.
- Narrator: heading "Settings"; "Output folder" with the helper or error as help text and the error announced; each toggle's name, note and on/off state; Choose, Launch, Cancel, Save changes.
- DPI 125%, 150% and 200%, and text size 150%: the window fits the screen, the body scrolls, and the footer stays visible.
- Choose opens the folder picker at the current folder; Launch opens Explorer; Launch is disabled for a missing folder.
- Enter saves from the folder box and from a toggle; Escape and Cancel close without saving (registry unchanged).
- Real right-click registration: turn "Right-click context menus" off and on, Save, and check Explorer ("Show more options" on Windows 11) for "keycut this!".
- Change the output folder to a new folder, Save: the folder is created, the count and manager refresh, and PATH follows (deliberately; touches the real PATH).
  An unwritable folder (for example `C:\Windows\System32\keycuts`) shows the inline error.
