# KC-09 — Creator: layout and visual states

**Area:** keycuts.GUI · **Depends on:** 06, 08 · **Blocks:** 10, 11

## Why
Replace the current "1. / 2. / 3." step layout in `MainWindow.xaml` with the compact creator from the UI2 prototype's
`MainWindow` component ([`Figma Redesign Keycuts UI2/src/App.tsx`](<../../Figma Redesign Keycuts UI2/src/App.tsx>), plus the
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
- [ ] All seven states can be reached and look right in light and dark. Use design-time data and the KC-16 samples.
- [ ] The heading text and copy match the strings above.
- [ ] The Browse menu matches the UI2 design and opens below the button without clipping the compact window.
- [ ] The creator stays compact, with no scrolling at 100% or 150% DPI.
- [ ] Long destinations (200+ characters) stay on one line in the field, with a tooltip.
