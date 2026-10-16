# KC-12 — Manager: layout, table, and states

**Area:** keycuts.GUI · **Depends on:** 03, 07, 08 · **Blocks:** 13, 14

## Why
The new Keycut Manager is a dense, scrollable view for occasionally reviewing 20–100 keycuts. It replaces the
external `keycuts.Batmanager` launch from the GUI. Reference: the prototype's `BatmanagerPanel` component and the
`.manager-*`, `table`, `th`, `td`, and `.type-*` rules in `index.css`.

## Layout (top to bottom)
1. **Toolbar:** eyebrow **KEYCUT MANAGER** with the heading **Your keycuts** below it. The one-line subtitle from the prototype
   ("Scan, search, and manage every shortcut in one place.") is optional; drop it if space is tight. On the right is a
   secondary **New keycut** button (spark icon) that runs `ShowCreator`.
2. **Tools row:** a search box (search icon, placeholder "Search names, types, descriptions, or destinations") with the
   **result count** right-aligned ("12 keycuts" / "1 keycut").
3. **Table** (bordered, 8px radius, fills the remaining height, scrolls vertically):
   - Columns, in order: **Shortcut** · **Type** · **Description** · **Destination** · *(three-dot menu)*.
   - Width priority: Shortcut (keep the full name; use a star or auto width with a sensible min, ~160) → Type (fits the badge, ~110)
     → Description (~25%, truncates) → Destination (takes the **remaining** width and truncates) → menu (fixed ~44).
   - **Header stays visible while scrolling.** A `DataGrid` or a `ListView`+`GridView` does this natively.
   - **Rows are ~36px**, single line, with no wrapping. Description and destination use `TextTrimming="CharacterEllipsis"`
     and a tooltip showing the full value. The shortcut name is bold and uses the primary text color; other cells use the secondary color.
   - Cells: name, `TypeBadge` (KC-07), description, destination, and the menu button (KC-14).
   - **Missing destination:** if `DestinationStatus == Missing`, show a small `Danger` "Missing" indicator (icon plus text)
     next to or under the name, with a tooltip. Color must not be the only cue.
   - **No whole-row click behavior.** No row selection highlight, no double-click handling, and Enter on a row does nothing.
     Rows get only a restrained `SurfaceHover` background on hover, with an arrow cursor rather than a hand. The row whose menu
     is open keeps that tint.
   - UI virtualization is on (`VirtualizingPanel.IsVirtualizing`, `VirtualizationMode=Recycling`), and scrolling is smooth with 100+ rows.
4. **Status bar** (`SurfaceSubtle`, top border, ~34px): bottom-left shows the **total item count** ("24 items").
   Don't show "Sorted by shortcut" or similar text.

## States
| State | Behavior |
| --- | --- |
| Loading | Load via `KeycutRepository.LoadAllAsync` when the manager opens and after any change. Show a quiet loading indicator in the table area. Search is disabled. |
| Populated | As above. |
| Empty (no keycuts) | Centered empty state: file and spark illustration, **No keycuts yet**, a short line of copy, and a primary **Create a keycut** button that runs `ShowCreator`. |
| No search results | "No keycuts match “query”", with a text button that clears the search. |
| Load error | The output folder is inaccessible or invalid. Show an inline message with an **Open settings** button. Don't crash, and don't create the folder. |

- Refresh triggers: opening the manager, returning from Settings, after delete or edit, and on `Window.Activated`, so changes made outside the app show up.
  `F5` also refreshes, as it does in Batmanager.
- Remove the "Open Batmanager" button and `MainFormLogic.OpenBatmanager` usage from the GUI. Retiring the Batmanager project
  itself is KC-20.

## Acceptance criteria
- [x] Works comfortably with 20 and with 100 items (KC-16 fixtures): the header stays pinned, rows are 36px, and there's no horizontal scrollbar at the default size.
- [x] Full shortcut names stay visible for typical names (≤ 24 chars) at the default width. Description and destination truncate with tooltips.
- [x] Clicking or double-clicking a row does nothing. Hover styling is subtle.
- [ ] Empty, loading, no-results, and error states render in both themes.
- [x] The bottom-left count reflects the total. The toolbar count reflects the filtered results.
## Implementation notes

**Files changed**
- New: `keycuts.GUI/ViewModels/KeycutRowViewModel.cs`, `keycuts.Tests/ManagerViewModelTests.cs` (20 tests).
- Rewritten: `ViewModels/ManagerViewModel.cs` (rows, states, async load), `Views/ManagerView.xaml(.cs)` (toolbar, search row, DataGrid, states, status bar).
- Edited: `ViewModels/ShellViewModel.cs` (optional loader/checker args; manager-mode `ReloadAsync` loads rows), `App.xaml.cs` (wires `KeycutRepository.LoadAllAsync`/`CheckDestinationsAsync`), `MainWindow.xaml.cs` (`Activated` and `F5` refresh while the manager shows), `DesignData/DesignViewModels.cs` (rows via `ShowKeycuts`, new `ManagerError` sample).

**Decisions**
- `DataGrid` with fully custom templates: pinned header, 36px rows, no selection/focus visuals, arrow cursor, hover tint (`SurfaceHover`, also when `KeycutRowViewModel.IsMenuOpen` for KC-14), virtualization with recycling, no horizontal scrollbar. Enter on the grid is swallowed; there is no click/double-click handling.
- Columns: Shortcut 1.7* (min 190, so 24-char names fit), Type 110, Description 1.5*, Destination 2.8*, menu 44 (reserved, empty until KC-14).
- Missing destinations: red "x Missing" under the name, tooltip with the destination.
- `ManagerViewModel.State` (Loading/Populated/Empty/NoResults/Error) drives the views. The loading indicator shows for the first load only; later refreshes are silent. A newer load cancels the older one. If loaded content is unchanged the existing rows are kept (no flicker, scroll position preserved on window activation).
- Without a loader (existing shell tests) the shell keeps the old count-only reload.
- `ApplyFilter()` is the hook for KC-13; for now it shows all rows, so the No-results state and its "Clear search" button are built but only reachable once KC-13 filters.
- The subtitle is kept. `MainFormLogic.OpenBatmanager` has no GUI callers left (the button was removed in KC-08); the method itself is left for KC-20.

**Deviations**
- "Clear search" uses `SecondaryButton` rather than a text-button style (none exists yet).
- No-results and error states were not exercised in the real GUI (see QA).

**Verification**: build 27 warnings (baseline), 333 tests pass, `dotnet format` shows only the 36 baseline WHITESPACE errors. GUI run headlessly against temp fixtures (100, 12, empty): header pinned, no horizontal scrollbar, Missing flag, empty state, light theme.

**Manual QA still needed**
- Error state (inaccessible folder) and no-results state visuals; loading spinner on a slow/network folder.
- Dark and high-contrast themes for all states.
- Smooth scrolling with 100+ rows; clicking/double-clicking/Enter on rows does nothing; mouse wheel and keyboard scrolling (KC-17 covers keyboard).
- Refresh on window activation after adding/deleting a `.bat` externally, F5, and returning from Settings after changing the output folder.
- Resize down to the minimum (720x480) and check column truncation/tooltips; DPI scaling; screen reader names.
