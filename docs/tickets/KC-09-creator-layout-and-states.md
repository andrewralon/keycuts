# KC-09 — Creator: layout and visual states

**Area:** keycuts.GUI · **Depends on:** 06, 08 · **Blocks:** 10, 11

## Why
Replace the current "1. / 2. / 3." step layout in `MainWindow.xaml` with the compact creator from the UI2 prototype's
`MainWindow` component ([`design-reference/src/App.tsx`](<../design-reference/src/App.tsx>), plus the
`.quick-drop*`, `.quick-destination-input`, `.browse-menu-wrap`, `.browse-choice-menu`, and `.name-*` rules in `index.css`).
This ticket covers **structure and states only**. Input is handled in KC-10 and submission in KC-11.

## Layout (top to bottom)
1. Header row: eyebrow **NEW KEYCUT** (small, accent, letter-spaced) with heading **Launch anything instantly** (exact text) below it.
   A settings `IconButton` sits right-aligned with `AutomationProperties.Name="Settings"`.
2. **Destination drop area** (grey `SurfaceSubtle`, 1px dashed `BorderStrong`, 12px radius, min height ~96):
   - Left: a 42px rounded icon tile (`AccentSoft` background, accent icon). It shows the arrow icon when empty and the
     type icon (link, folder, file, or play) once a destination resolves.
   - Middle: label **Destination** with an **inline, borderless destination `TextBox`** below it:
     - Placeholder **Drop or paste a file, folder, or URL here**. This replaces the original spec copy, per the UI2 design.
     - Transparent background, no border, and secondary text color. It keeps no border on hover or focus; focus is shown on the
       whole drop area instead (below).
     - Single line, no wrapping. Long values scroll horizontally inside the field, with a tooltip showing the full value.
     - `AutomationProperties.Name="Destination"`.
     - **This field is the only place the destination appears.** Don't repeat the path or URL anywhere else.
   - Right: a secondary **Browse** button with a trailing chevron and no leading icon. The chevron points down when closed and up when open.
     It opens a menu (`.browse-choice-menu`: ~190px wide, raised surface, menu shadow, right-aligned under the button) with two items:
     - File icon, **File…**, with the subtitle *Choose a single file*
     - Folder icon, **Folder…**, with the subtitle *Choose a directory*
     - Items are ~46px tall with an accent icon, a bold title, and a tertiary subtitle.
   - **Focus-within:** when the destination field or Browse has keyboard focus, the drop area border turns accent and
     gets a 3px soft accent glow (`.quick-drop:focus-within`).
3. **Name form, always visible** (UI2 change; previously hidden until a destination was set):
   - Label **Name your keycut**, then a `TextBox` (placeholder `e.g. l-vicki`) with a primary **Create** button
     (arrow icon) beside it.
   - **Create is disabled until both a valid destination and a non-empty name exist.**
   - Helper text **Press Enter to create it**. This line is replaced by the error message in error states.
4. Footer strip (`SurfaceSubtle`, top border): a quiet **View all keycuts** link (external icon) with a count pill on the right
   showing the real keycut count.

No step indicators, instructional cards, or extra dividers.

## States (driven by `CreatorViewModel.State` plus flags)
| State | Visual |
| --- | --- |
| Empty | Arrow icon, empty destination field with placeholder, name form visible, Create disabled. |
| Drag hover | Drop area gets an `AccentSoft` background, a 2px solid accent border, and a soft accent outer glow. Keep layout stable (compensate padding). |
| Destination selected | Type icon plus the resolved destination in the field. Name field focused. Create is enabled once a name is typed. |
| Saving | Create button shows a spinner and "Saving". The destination field, Browse, name field, and Create are disabled, and the drop area ignores drops. |
| Duplicate-name error | Name `TextBox` gets the error border. The helper line turns `Danger` and reads: “l-vicki” already exists. Choose another name. The drop area stays in its normal style. |
| Invalid or inaccessible destination | Drop area gets a `DangerSoft` background and `Danger` border. A short specific message (not found, access denied, or unsupported) appears in the helper line slot. Keep the typed text in the field so the user can fix it. Create is disabled. |
| Success | Briefly confirm, for example a check icon with “Created l-vicki” in the helper line for ~2s, then return to Empty and increment the count. |

> The UI2 prototype also turns the drop area red in its "error" preview, which shows a duplicate name. Treat that as a preview artifact.
> The drop area turns red only for **destination** errors; name errors style only the name field.

## Acceptance criteria
- [x] All seven states can be reached and look right in light and dark. Use design-time data and the KC-16 samples.
- [x] The heading text and copy match the strings above.
- [x] The Browse menu matches the UI2 design and opens below the button without clipping the compact window.
- [ ] The creator stays compact, with no scrolling at 100% or 150% DPI.
- [ ] Long destinations (200+ characters) stay on one line in the field, with a tooltip.

## Implementation notes

**Files changed**
- `keycuts.GUI/Views/CreatorView.xaml` (rewritten: header, drop area, name form, footer, all state triggers) and `CreatorView.xaml.cs` (Browse menu open/close and placement, drag-hover flag, Create guarded by `CanCreate`, `IsSaving` replaces the manual button disable).
- `keycuts.GUI/ViewModels/CreatorViewModel.cs`: `CreatorState` enum plus `State`, `IsDragHover`, `IsSaving`, `IsBrowseMenuOpen`, `DestinationKind`, `DestinationError`, `SetDuplicateName`, `SetSuccess`, and derived bindings (`CanCreate`, `IsInputEnabled`, `ShowDragHover`, `ShowDestinationError`, `DestinationIcon`, `HelperText`, `HelperIsError`, `IsSuccess`).
- `keycuts.GUI/MainFormLogic.cs`: sets `DestinationKind` when it sets the destination (so the type icon shows).
- `keycuts.GUI/Themes/Controls.xaml` and `TextBoxChrome.cs`: `TextBoxChrome.Placeholder` attached property and placeholder in the `AppTextBox` template; a `TextBoxChrome.HasError` trigger so the error border also shows when the field isn't focused; removed the extra `Margin` on `PART_ContentHost` (WPF already applies `Padding` there, so typed text was indented twice versus the placeholder).
- `keycuts.GUI/DesignData/DesignViewModels.cs` (DEBUG): design contexts for every state (`CreatorEmpty`, `CreatorSelected`, `CreatorUrl`, `CreatorDragHover`, `CreatorSaving`, `CreatorDuplicateName`, `CreatorInvalidDestination`, `CreatorLongDestination`, `CreatorSuccess`).
- `keycuts.Tests/CreatorViewModelTests.cs`: 17 new tests for state, icon, helper text, and Create enablement.
- Deleted `keycuts.GUI/Images/Antu_codelite.svg_30x30.png` (nothing referenced it). `Antu_codelite.svg.png` and `TR_256_ico.ico` are also unreferenced, but this ticket didn't orphan them, so they stay.

**Decisions**
- Drop-area dashed outline is a `Rectangle` overlay (WPF `Border` can't dash). Hover swaps it to a 2px solid accent line without changing layout.
- Browse menu is a styled `ContextMenu` (keyboard navigation and automation for free), right-aligned under the button; the chevron rotates via `IsBrowseMenuOpen`.
- `Destination`/`ShortcutName` edits clear the resolved kind, destination error, duplicate-name error, and success message. KC-10/KC-11 must set `DestinationKind`/`DestinationError` **after** assigning `Destination`.
- `HasValidDestination` is currently "non-blank and no `DestinationError`"; KC-10 should resolve the text (`DestinationResolver`) and set kind or error.
- Inline destination text uses `FontSizeCaption` (12) so the full placeholder fits in the 440px window; window margins are 20.
- Saving disables Create, so the button uses the normal disabled look with the spinner and "Saving".

**Deviations**
- "NEW KEYCUT" has no letter spacing (WPF `TextBlock` has none; a hack would hurt screen readers).
- No drop shadow on the Browse menu; the shared `ContextMenu` style from KC-05 has none and adding one needs a transparent popup.
- Browse menu items have no handlers yet (KC-10). The legacy `PreviewDragOver` on the destination field still clears it (KC-10 reworks drop).
- Success auto-reset after ~2s, Enter/Create submit flow, and duplicate/error detection are KC-11; this ticket only provides the states.

**Verification**: all states were rendered headlessly from the design data in light and dark (throwaway harness, since removed) and the open Browse menu was screenshotted in light. The creator is ~356 DIP tall with no scroll. Not ticked: the 100%/150% DPI and long-destination tooltip criteria need a live check.

**Manual QA still needed**
- Run the app at 100% and 150% DPI: no scrolling, nothing clipped, Browse menu fully visible.
- Hover tooltip on a 200+ character destination; check horizontal scrolling while typing.
- Live drag from Explorer / a browser: drag-hover look and flicker while moving over child controls; drop is ignored while saving.
- Keyboard: Tab order, focus glow on the drop area for field and Browse, Enter in the name field, menu navigation with arrow keys.
- Dark mode and high-contrast look of the Browse menu; Narrator for the helper line and icon-only Settings button.