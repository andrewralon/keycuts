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
- [ ] Creator → manager → creator happens in one window, with no second process or window.
- [ ] Sizes match the above. The manager is clearly taller and wider than the creator.
- [ ] Launching with a path argument still lands in the creator with the destination set and the name field focused.
- [ ] Unit tests cover `ShellViewModel` navigation and the Escape precedence rules.
