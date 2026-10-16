# KC-18 — QA verification, docs, and cleanup

**Area:** Release · **Depends on:** all previous tickets

## Why
This closes out the feature: verify everything the spec lists, run the repository's prescribed checks, and remove
leftovers from the old UI without touching unrelated working behavior.

## Automated checks (commands from KC-01)
- [x] `dotnet format keycuts.sln --verify-no-changes` (exits 2 with only the 28 pre-existing WHITESPACE errors in 7 old files; none added)
- [x] `dotnet build keycuts.sln` (27 warnings, 0 errors; no new warnings compared with the recorded baseline)
- [x] `dotnet test keycuts.sln` (378 passed; run twice, identical)

## Manual verification matrix
Run in **light and dark** themes against the KC-16 fixtures (0, 2, 20, and 100 items) and a real output folder.

| Area | Verify |
| --- | --- |
| Drag and drop | File, folder, `.lnk`, `.url`, browser URL (address-bar lock and in-page link), multiple files (uses the first), unsupported data (rejected) |
| Browse | Browse ▾ menu opens and closes by mouse and keyboard. File… and Folder… pick and cancel |
| Paste / type | Paste a URL, a text path, and a file copied in Explorer into the destination field. Typing a path updates the icon after a pause |
| Edit | Opens Notepad. A description added there shows in the manager after switching back |
| Detection and validation | Correct icon per type. Missing, inaccessible, and unsupported destinations show the invalid state |
| Focus | Focus moves to the name field after every input method |
| Enter submission | Enter creates. Enter on other controls doesn't. Saving state shows. Returns to empty and the count increments |
| Errors | Duplicate name (Force overwrite off and on), invalid name, destination deleted before Create, output folder unwritable |
| Manager | Same window. Size is much larger than the creator. Header stays pinned. 36px rows. Truncation and tooltips. No row click. Subtle hover |
| Search | Name, type, description, and destination. Result count updates. Escape clears |
| Sort | Shortcut, Type, and Description in both directions |
| Menu actions | Open, Edit, Copy destination, and Delete on each type. Closes on selection, Escape, and outside click |
| States | Empty, populated, loading, no results, load error, missing destination, long list (100) |
| Keyboard | The KC-17 checklist |
| Regression | Explorer right-click "keycut this!" still opens the creator pre-filled. Settings window still saves and the manager reflects output-folder changes. The CLI still creates keycuts. Batmanager is fully removed (KC-20) |

## Cleanup
- [x] Remove dead code from the old creator: the `Steps`/`Instructions` properties, duplicate per-grid drag handlers,
      `MainFormLogic.ClearDestination`, the "Open Batmanager" button, the `Thread.Sleep` and `MessageBox` paths, and `CommonLogic`
      if it's fully superseded. Keep anything the CLI still uses. Batmanager removal itself is KC-20.
- [ ] Don't commit either prototype's `node_modules`. Decide whether to keep `Figma Redesign Keycuts UI2` in the repo as the design
      reference. The superseded `Figma Redesign Keycuts UI` folder and the stray empty `keycuts-prototype/` folder (only a `.git`) can be removed.
      All three are currently untracked.
- [x] Update the root `README.md`: describe the creator flow (drop, paste, or browse → name → Enter) and the in-app manager,
      and refresh screenshots if any are added.
- [ ] Capture before and after screenshots of the creator and manager in both themes for the PR.

## Implementation notes

**Files changed:** deleted `keycuts.GUI/CommonLogic.cs` (staged `git rm`; nothing referenced it after KC-11 and KC-20); root `README.md` (creator flow, manager, themes, debug-only `KEYCUTS_OUTPUT_FOLDER` and `tools/New-SampleKeycuts.ps1`, Batmanager removal, no screenshots); this ticket. Not committed.

**Cleanup audit (grep + build + tests)**
- Already gone earlier: `Steps`/`Instructions`, per-grid drag handlers, `MainFormLogic.ClearDestination`, `OpenBatmanager`, the Open Batmanager button, `Thread.Sleep` and `MessageBox` creator paths.
- Kept deliberately: `MainFormLogic` (`HandleNewDestination` for Explorer `args[0]`, `SaveSettings` for the Settings window, `ActivateThisWindow`); the `Thread.Sleep` retry in `SystemKeycutActions` (clipboard retry, not UI blocking); `keycuts.GUI/Images/*` (tracked, may be used as app icons outside XAML).
- Results: build 27 warnings / 0 errors (`--no-incremental`); tests 378 passed on two consecutive runs (no flakiness seen); format: 28 WHITESPACE errors in 7 files (baseline, not grown).

**Prototype folders (not deleted, user decides)**
- `Figma Redesign Keycuts UI/` and `Figma Redesign Keycuts UI2/`: ~0.1 MB each, 34 files each, no `node_modules`. `keycuts-prototype/`: 19 files, essentially only `.git` metadata (0 MB).
- `.gitignore` already contains `node_modules/`, so no new ignore was needed.
- Recommendation: keep `Figma Redesign Keycuts UI2` (design reference, small), delete `Figma Redesign Keycuts UI` (superseded) and `keycuts-prototype/` (empty stray repo) after confirming nothing is wanted. Not ticked because the decision is the user's.

**Screenshots:** not captured (no headless capture). The screenshot box stays unticked.

### MANUAL QA CHECKLIST
Run in light and dark themes against KC-16 fixtures (0, 2, 20, 100 items; `KEYCUTS_OUTPUT_FOLDER`, Debug build, never `C:\Shortcuts`) and, for end-to-end items, a real output folder. Tags show the source ticket.

**Drag and drop**
- [ ] Drag from Explorer: file, folder, `.lnk` (resolves to target), `.url`: correct type icon; text is replaced, not inserted at the caret. (KC-10, KC-02)
- [ ] Drag from Edge/Chrome: address-bar lock icon and an in-page link. URLs with trailing newline are cleaned. (KC-10, KC-02)
- [ ] Multi-file drag uses the first file only. Unsupported data shows the no-drop cursor and no hover. (KC-10)
- [ ] Hover look appears and does not flicker over child controls; drop is ignored while Saving; window activates after drop. (KC-09, KC-10)
- [ ] Drop onto the creator works end to end after the shell rewrite. (KC-08)
- [ ] Dropping a deleted file or an inaccessible path shows the invalid state with a specific message. (KC-10)

**Browse**
- [ ] Browse ▾ opens/closes by mouse (click again closes) and keyboard (Enter/Space/Alt+Down open; arrows; Enter picks; Escape closes and returns focus to Browse). (KC-10, KC-17)
- [ ] File… and Folder… dialogs pick and cancel; a `.lnk` picked resolves to its target. (KC-10)
- [ ] Menu is fully visible at 100% and 150% DPI. (KC-09)
- [ ] Narrator announces expanded/collapsed. (KC-10, KC-14)

**Paste / type**
- [ ] Ctrl+C a file in Explorer, Ctrl+V into the destination field and while focus is on Browse or the footer: path is set. (KC-10, KC-17)
- [ ] Paste a URL and a text path; scheme-less URLs (e.g. `amazon.com`) resolve as URL. (KC-10, KC-02)
- [ ] Ctrl+V in the name field still pastes text into the name. (KC-10)
- [ ] Typing a path: icon updates after a pause, no error mid-typing, error on blur/Enter. Enter in the destination only resolves/moves on, never creates. (KC-10, KC-17)
- [ ] Long destination (200+ chars) stays on one line with a tooltip and scrolls horizontally while typing. (KC-09)
- [ ] Unreachable UNC path (e.g. `\\10.255.255.1\share`) shows an error after ~2 s without freezing the UI. (KC-02)

**Edit**
- [ ] Edit opens Notepad on the right file, including paths with spaces. (KC-15, KC-14)
- [ ] Add a description in Notepad, save, switch back: it shows in the Description column. (KC-15, KC-03)
- [ ] Break the `.bat` by hand: the row drops out. Delete the `.bat` externally then Edit: "This keycut no longer exists" shows and the row disappears. (KC-15)

**Validation / detection**
- [ ] Correct icon/badge per type (URL, File, Folder, Command). Missing, inaccessible and unsupported destinations show the invalid state. (KC-02, KC-07, KC-10)
- [ ] Badges render correctly in light, dark and high contrast. (KC-07)
- [ ] Design-time previews show `l-vicki` and `downloads` with correct badges in the XAML designer / Hot Reload. (KC-16)

**Focus**
- [ ] Focus lands on the name field after every successful drop, whole-field paste and Browse; on the destination at launch; name field when launched with a path. (KC-10, KC-08, KC-17)
- [ ] Manager focuses search on entry. (KC-08)
- [ ] Focus is correct after each create outcome (success, duplicate, errors). (KC-11)

**Submit**
- [ ] Drop, type name, Enter creates the `.bat`; creator returns to Empty after ~2 s with count incremented; Create button does the same; success beep sounds. (KC-11)
- [ ] Saving state is visible with `KEYCUTS_CREATE_DELAY_MS=1500` (Debug); UI not frozen; inputs disabled. (KC-11)
- [ ] Enter on Browse, the settings button and the footer link does not create. (KC-11)
- [ ] The new `.bat` contains `REM <description></description>` and still launches. (KC-03)
- [ ] Real PATH update still works with a changed output folder (touches real PATH/registry; needs admin; do deliberately). (KC-04)
- [ ] Overwrite honors the Settings "force overwrite" toggle. (KC-04)

**Errors**
- [ ] Duplicate name, Force overwrite off: inline error, name selected/focused, no file; clears on edit; appears while typing (~250 ms). On: silently overwritten. (KC-11)
- [ ] Invalid name, destination deleted before Create, unwritable output folder: inline messages (mention Settings for folder errors). (KC-11, KC-04)

**Manager**
- [ ] Same window resizes: creator ~440 wide; manager ~960x700, min ~720x480; titles "keycuts" / "keycuts — Your keycuts". (KC-08)
- [ ] Creator position restored on return; manager size remembered; window clamped near screen edges / second monitor; maximize then return. (KC-08)
- [ ] Header pinned, 36px rows, truncation and tooltips, no horizontal scrollbar at minimum size, subtle hover; clicking/double-clicking/Enter on rows does nothing. (KC-12, KC-14)
- [ ] Smooth scrolling with 100 rows; scroll position kept on refresh/activation. (KC-12, KC-13)
- [ ] Refresh on activation after adding/deleting a `.bat` externally, on F5, and after changing the output folder in Settings. (KC-12, KC-08)

**Search**
- [ ] Matches name, type, description and destination; result count updates; Escape clears; clears on return to the creator; "Clear search" in no-results. (KC-13, KC-08)

**Sort**
- [ ] Shortcut, Type, Description, both directions; hover chevron, accent on active column, focus ring; Tab then Enter/Space sorts; Narrator reads "Sorted ascending/descending". (KC-13)

**Menu actions**
- [ ] Open, Edit, Copy destination, Delete on URL, Folder, File and Command keycuts using real launch/Notepad/clipboard (fixtures only). (KC-14)
- [ ] Menu closes on selection, Escape (focus returns to the row button) and outside click; only one open at a time; row keeps hover tint while open. (KC-14)
- [ ] Delete moves focus to next row's button, else previous, else search (or "Create a keycut" when empty). Delete on read-only/locked file shows inline error and keeps the row. (KC-14)
- [ ] Status messages ("Copied", "Deleted “name”", errors) appear, clear, and don't overlap the count when narrow. (KC-14)

**States**
- [ ] Empty, populated, loading (slow/network folder), no results, load error (inaccessible folder), missing destination, 100-item list, in light, dark and high contrast. (KC-12, KC-13, KC-16)
- [ ] Creator states: empty, selected, URL, drag-hover, saving, duplicate, invalid, long destination, success. (KC-09)

**Keyboard / accessibility (KC-17 checklist)**
- [ ] Tab order: creator destination, Browse, name, Create (when enabled), View all keycuts, Settings; manager New keycut, search, sort headers, row buttons, Settings. (KC-17)
- [ ] Drop-area accent ring while the destination or Browse has focus. (KC-17, KC-09)
- [ ] Row menu opens with Enter/Space/Shift+F10/Apps/Alt+Down; arrows, Enter/Space, Escape. (KC-14, KC-17)
- [ ] Escape precedence: menu, then clear search, then back to creator; Escape in the creator closes. Enter creates only in the creator. (KC-08, KC-17)
- [ ] Narrator: headings ("Launch anything instantly", "Your keycuts"), field names, helper/error and "Saving" announcements, "Actions for {name}", "Type: {kind}", icon-only Settings. (KC-17, KC-09, KC-14)

**Themes / high contrast / DPI**
- [ ] Switch Windows app mode live while the app is open: palette and title bar re-theme without restart. (KC-05)
- [ ] High-contrast theme: system colors, readable, focus rings visible. (KC-05, KC-17)
- [ ] Visual states of buttons, text box glow/error, menus, tooltip, scrollbar, spinner in both themes; glow not clipped. (KC-05)
- [ ] All icons at 12/16/18/23 px in light/dark/HC and at 125%/150% DPI; icon color follows theme live. (KC-06)
- [ ] Creator has no scrolling and nothing clipped at 100%/150% DPI; usable at 200% scaling and 150% text size. (KC-09, KC-17)

**Regression**
- [ ] Explorer right-click "keycut this!" opens the creator pre-filled, name focused, window to front. (KC-08, KC-10, KC-20)
- [ ] Settings opens modally, saves; changing the output folder refreshes count and manager. Registry `OutputFolder` unchanged after Settings use with the override active. (KC-08, KC-16)
- [ ] CLI still creates keycuts (and `.bat` runs). (KC-20, KC-03, KC-04)
- [ ] Batmanager fully removed; clean checkout builds without a stale `keycuts.Batmanager` folder. (KC-20)
- [ ] Legacy Settings window still looks acceptable with the implicit styles. (KC-05)

### Deviations from tickets (consolidated)
- KC-01: `dotnet format --verify-no-changes` is not clean (pre-existing whitespace errors; now 28 in 7 files after KC-20). `-warnaserror` not used (27 CA1416 baseline).
- KC-05: Added `OnAccentBrush`, `ControlBorderBrush`, `SidebarBrush` for WCAG contrast; light TextTertiary only for placeholder/decorative text; `CA1416` suppressed in `ThemeManager`.
- KC-06: `moon`/`sun` icons not ported (no theme toggle); icons have no automation peer.
- KC-08: Settings now opens modally; creator not resizable.
- KC-09: no letter spacing on "NEW KEYCUT"; no shadow on the Browse menu; the success auto-reset and submit were left to KC-11.
- KC-11: destination errors reuse KC-10 messages instead of "This file no longer exists."; invalid-name reuses duplicate visuals; beep only on success.
- KC-12: "Clear search" uses `SecondaryButton`.
- KC-13: no `ICollectionView`; `Rows` is rebuilt from a pure predicate/comparer.
- KC-17: helper line reads "Saving…" while saving; Settings gear placed last in Tab order.
- KC-20: dropped Open destination location, read-only output-folder display, single-letter row shortcuts; Refresh becomes auto-refresh + F5.
- KC-04: `Runner` creation failure returns `CannotCreateOutputFolder` instead of throwing.

### Unticked acceptance criteria remaining for manual verification
- KC-05: live light/dark switching.
- KC-09: no scrolling at 100%/150% DPI; long-destination tooltip.
- KC-10: all drag/paste/typing/Browse/focus criteria (10 boxes).
- KC-11: end-to-end create, Create click, Saving state, duplicate-name behavior.
- KC-12: empty/loading/no-results/error states in both themes.
- KC-14: actions on all four types; keyboard-only menu; row click does nothing.
- KC-16: design-time preview badges.
- KC-17: focus-within ring, Browse menu keys, Ctrl+V/Enter behavior, menu keys, Escape precedence, focus rings in all themes, Narrator, DPI/text scaling.
- KC-18: prototype-folder decision (user) and screenshots.
- KC-20: the `[~]` GUI/CLI/Explorer flows (covered only by build/tests).
- KC-01: format-clean criterion (by design; baseline recorded).
