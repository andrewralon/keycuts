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
- [ ] Works comfortably with 20 and with 100 items (KC-16 fixtures): the header stays pinned, rows are 36px, and there's no horizontal scrollbar at the default size.
- [ ] Full shortcut names stay visible for typical names (≤ 24 chars) at the default width. Description and destination truncate with tooltips.
- [ ] Clicking or double-clicking a row does nothing. Hover styling is subtle.
- [ ] Empty, loading, no-results, and error states render in both themes.
- [ ] The bottom-left count reflects the total. The toolbar count reflects the filtered results.
