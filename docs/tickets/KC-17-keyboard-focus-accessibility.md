# KC-17 — Keyboard, focus, and accessibility pass

**Area:** keycuts.GUI · **Depends on:** 11, 13, 14

## Why
The spec requires keyboard navigation and visible focus treatment to keep working. Each feature ticket has local
criteria; this ticket is the end-to-end pass across both modes and both themes.

## Checklist
**Creator**
- [x] On launch, focus is on the destination field, or on the name field if launched with a path argument.
- [x] Tab order: destination field → Browse → name field → Create → View all keycuts → Settings. Adjust as needed so it follows visual order.
      Create is skipped while it's disabled.
- [ ] The drop area shows the accent focus-within ring while the destination field or Browse has focus.
- [ ] Browse ▾ menu: Enter, Space, or Alt+Down opens it; arrows move; Enter picks; Escape closes and returns focus to Browse.
- [ ] Ctrl+V in the destination field sets the destination. Enter in the destination field moves to the name field. Enter in the name field creates.
      Escape closes the window, as it does today, unless the Browse menu is open.
- [x] Error text is announced: use `AutomationProperties.LiveSetting="Assertive"` on the helper/error line, or raise a
      `LiveRegionChanged` event. The name field's `AutomationProperties.HelpText` reflects the current error.
- [x] The Saving state is announced, and disabled controls are skipped by Tab.

**Manager**
- [x] On open, focus is on the search box.
- [x] Tab order: New keycut → search → sortable headers → each row's menu button → status. Rows themselves aren't tab stops.
- [x] Every three-dot button has a unique accessible name ("Actions for l-vicki").
- [ ] The menu opens with Enter, Space, Shift+F10, or the Apps key. Arrows move between items, and Escape closes and restores focus (KC-14).
- [ ] Escape precedence: menu → clear search → back to the creator (KC-08).
- [x] Badges and the "Missing" indicator expose text, not just color.

**Both themes, plus high contrast**
- [ ] Focus rings are clearly visible on every interactive control in light, dark, and high-contrast modes.
- [ ] Narrator reads headings ("Launch anything instantly" / "Your keycuts"), labels, and states sensibly.
      Set `AutomationProperties.HeadingLevel` on the headings.
- [ ] Usable at 100%, 150%, and 200% scaling, with text scaling (Settings → Accessibility → Text size) at 150%.

## Deliverable
Fix whatever the pass finds. Record any intentionally deferred items in the ticket.

## Implementation notes

**Files changed**
- `keycuts.GUI/Views/CreatorView.xaml(.cs)`, `Views/ManagerView.xaml`, `ViewModels/CreatorViewModel.cs`
- New: `keycuts.Tests/AccessibilityTests.cs` (view-model text tests plus STA tests that load the real views in an off-screen window)

**Changes**
- Tab order: the Settings gear was first in the visual tree, so Tab reached it before the destination field. It is now declared last
  (overlaid top right) in both views. Creator order is destination, Browse, name, Create (when enabled), View all keycuts, Settings.
  Manager order is New keycut, search, the three sort headers, each row's menu button, then Settings. Settings sits after the rows
  rather than between New keycut and search; the ticket's list omits it, so this is a deliberate placement.
- Creator: the destination and name fields expose `AutomationProperties.HelpText` (`DestinationHelpText`, `NameHelpText`). The helper line is
  `Assertive` while it shows an error and raises `LiveRegionChanged` whenever its text changes. While saving, the helper line reads
  "Saving…" and the Create button's accessible name is "Saving" (`CreateButtonName`). Shift+F10 / Apps on Browse now positions the menu
  like the click path.
- Headings: `HeadingLevel` on "Launch anything instantly" and "Your keycuts" (Level 1), and on the manager's empty and load-error titles (Level 2).
  The manager result count is a polite live region and the load error is assertive.
- Already correct and now covered by tests: launch focus (destination, or name when a path is given), search box focus in the manager,
  disabled Create/fields being skipped by Tab, unique "Actions for <name>" button names, `Type: <kind>` badge names and the visible "Missing" text.
- Escape precedence (menu, then clear search, then creator) and the menu Escape/focus-return handlers were reviewed in code and covered by the existing
  `ShellViewModel` tests; the focus ring styles (`AppFocusVisual`, drop-area `IsKeyboardFocusWithin`, sort header focus border) already exist.

**Deviation**
- The helper line text is "Saving…" while saving (previously the default "Press Enter to create it"), so the Saving state is announced.

**Manual QA still needed (not ticked above)**
- Drop-area accent ring while the destination or Browse has focus, and focus rings on every control in light, dark and high contrast.
- Browse menu: Enter/Space/Alt+Down open, arrows, Enter picks, Escape returns focus to Browse; row menu with Enter/Space/Shift+F10/Apps.
- Ctrl+V, Enter and Escape behavior in the fields; Escape precedence in the manager with a real keyboard.
- Narrator: headings, field names, error and Saving announcements (the `LiveRegionChanged` event is raised but not observed by a screen reader here).
- 100%, 150% and 200% display scaling and 150% text scaling.