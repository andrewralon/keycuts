# KC-08 — Single-window shell and view switching

**Area:** keycuts.GUI · **Depends on:** 05 · **Blocks:** 09, 12

## Why
Opening the manager must **replace** the compact creator in the same window, not open a second window. Today
"Open Batmanager" launches a separate exe. The two modes are deliberately different sizes, and the current
`MainWindow` mixes everything into code-behind.

## Scope
- Light MVVM scaffolding with no new NuGet dependency unless the team prefers CommunityToolkit.Mvvm:
  `ObservableObject`, `RelayCommand`/`AsyncRelayCommand`, `ShellViewModel`, `CreatorViewModel`, and `ManagerViewModel`.
- `MainWindow` hosts a `ContentControl` bound to `ShellViewModel.CurrentView`, with `DataTemplate`s for
  `CreatorView` and `ManagerView` as `UserControl`s.
- Window sizing per mode:
  - **Creator:** ~440 DIP wide, `SizeToContent="Height"`, not resizable or only minimally resizable. Compact.
  - **Manager:** ~960×700 DIP by default, resizable, with a minimum of ~720×480. Keep the window on-screen (clamp to the
    work area of the current monitor) when growing.
  - Remember the manager's size for the session and restore the creator's compact size and position when returning.
- Window title: "keycuts" for the creator, "keycuts — Your keycuts" for the manager.
- Navigation commands: `ShowManager` (from "View all keycuts") and `ShowCreator` (from "New keycut").
- Focus on switch: the creator focuses the destination field, or the name field if a destination is set. The manager focuses the search box.
- Keep the existing behavior:
  - `App.xaml.cs` startup arg (Explorer right-click "keycut this!") still pre-fills the destination and focuses the name field.
  - `SetForegroundWindow` activation from `MainFormLogic.ActivateThisWindow` still works.
  - **Escape** in the creator still closes the window. In the manager, Escape first closes any open menu. If none is open,
    it clears a non-empty search. If the search is already empty, it returns to the creator.
- The settings button opens the existing `SettingsWindow` with `Owner = MainWindow`. When it closes, reload the keycut count and data,
  because the output folder may have changed.

## Acceptance criteria
- [x] Creator → manager → creator happens in one window, with no second process or window.
- [x] Sizes match the above. The manager is clearly taller and wider than the creator.
- [x] Launching with a path argument still lands in the creator with the destination set and the name field focused.
- [x] Unit tests cover `ShellViewModel` navigation and the Escape precedence rules.

## Implementation notes

**Files changed**
- New: `keycuts.GUI/Mvvm/ObservableObject.cs`, `Mvvm/RelayCommand.cs` (`RelayCommand` + `AsyncRelayCommand`; no new NuGet package).
- New: `keycuts.GUI/ViewModels/{ShellViewModel,CreatorViewModel,ManagerViewModel}.cs`, `keycuts.GUI/WindowLayout.cs` (size constants + work-area clamp).
- New: `keycuts.GUI/Views/{CreatorView,ManagerView}.xaml(.cs)` (interim content, see below).
- Rewritten: `MainWindow.xaml(.cs)` (`ContentControl` bound to `ShellViewModel.CurrentView` with `DataTemplate`s; per-mode sizing; Escape; Settings).
- Changed: `App.xaml.cs` (builds the shell, passes `args[0]` to `shell.Creator`), `MainFormLogic.cs` (`HandleNewDestination(CreatorViewModel, string)`; removed `OpenSettings` and `ClearDestination`).
- Tests: `keycuts.Tests/ShellViewModelTests.cs` (navigation, Escape precedence, count reload, `WindowLayout` clamp, commands). 236 tests pass.

**Decisions**
- `CreatorView` and `ManagerView` are interim, minimal, token-styled content. KC-09 and KC-12 replace them. The legacy create flow (`CommonLogic` adapter, `MessageBox`, `Thread.Sleep`, beep) and drag-and-drop moved into `CreatorView` code-behind unchanged in behavior; KC-11 reworks them.
- Focus on switch is done in each view's `Loaded` handler (views are recreated per switch; view-models persist, so creator input survives a round trip). Creator focuses the name field when a destination is set, else the destination field. Manager focuses search.
- `ShellViewModel.HandleEscape()` returns an `EscapeResult` and holds the precedence rules (creator: close; manager: menu, then search, then creator). `ManagerViewModel.IsMenuOpen` is the hook KC-14 must set.
- Entering the manager resets the search text and menu state. The keycut count reloads on entering either view, after a keycut is created, and after Settings closes.
- Settings opens modally with `Owner = MainWindow` (was a non-modal `Show()`), so the reload happens when it closes.
- The manager's size is remembered for the session; the creator's position is restored when returning. Work area is the current monitor's, via `MonitorFromWindow`/`GetMonitorInfo`, converted to DIPs.
- Creator is `ResizeMode=CanMinimize` (not resizable), 440 DIP wide, `SizeToContent=Height`.

**Deviations**
- `Images/Antu_codelite.svg_30x30.png` is now unused (the settings button uses the Settings icon); I left it for KC-09 to delete, as KC-06 noted.
- `MainFormLogic.OpenBatmanager` is no longer called from the GUI but remains until KC-20.
- `ActivateThisWindow` still runs before `Show()` at startup, as before (the process main-window handle is not yet available then); unchanged behavior.

**Manual QA still needed**
- Click View all keycuts / New keycut and confirm one window resizes (440 wide compact; ~960×700 manager, min ~720×480), titles "keycuts" / "keycuts — Your keycuts".
- Creator position is restored after returning; manager size is remembered on the second visit; window near a screen edge or on a second monitor is clamped on-screen; maximize the manager then return.
- Explorer right-click "keycut this!" opens with the destination filled and the name field focused, and the window is brought to front.
- Escape sequence in the manager and in the creator (closes). Enter creates in the creator only.
- Settings opens over the window; change the output folder and confirm the count refreshes. Drag-and-drop onto the creator. Light/dark and high contrast look; DPI scaling.