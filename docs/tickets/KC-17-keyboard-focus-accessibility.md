# KC-17 — Keyboard, focus, and accessibility pass

**Area:** keycuts.GUI · **Depends on:** 11, 13, 14

## Why
The spec requires keyboard navigation and visible focus treatment to keep working. Each feature ticket has local
criteria; this ticket is the end-to-end pass across both modes and both themes.

## Checklist
**Creator**
- [ ] On launch, focus is on the destination field, or on the name field if launched with a path argument.
- [ ] Tab order: destination field → Browse → name field → Create → View all keycuts → Settings. Adjust as needed so it follows visual order.
      Create is skipped while it's disabled.
- [ ] The drop area shows the accent focus-within ring while the destination field or Browse has focus.
- [ ] Browse ▾ menu: Enter, Space, or Alt+Down opens it; arrows move; Enter picks; Escape closes and returns focus to Browse.
- [ ] Ctrl+V in the destination field sets the destination. Enter in the destination field moves to the name field. Enter in the name field creates.
      Escape closes the window, as it does today, unless the Browse menu is open.
- [ ] Error text is announced: use `AutomationProperties.LiveSetting="Assertive"` on the helper/error line, or raise a
      `LiveRegionChanged` event. The name field's `AutomationProperties.HelpText` reflects the current error.
- [ ] The Saving state is announced, and disabled controls are skipped by Tab.

**Manager**
- [ ] On open, focus is on the search box.
- [ ] Tab order: New keycut → search → sortable headers → each row's menu button → status. Rows themselves aren't tab stops.
- [ ] Every three-dot button has a unique accessible name ("Actions for l-vicki").
- [ ] The menu opens with Enter, Space, Shift+F10, or the Apps key. Arrows move between items, and Escape closes and restores focus (KC-14).
- [ ] Escape precedence: menu → clear search → back to the creator (KC-08).
- [ ] Badges and the "Missing" indicator expose text, not just color.

**Both themes, plus high contrast**
- [ ] Focus rings are clearly visible on every interactive control in light, dark, and high-contrast modes.
- [ ] Narrator reads headings ("Launch anything instantly" / "Your keycuts"), labels, and states sensibly.
      Set `AutomationProperties.HeadingLevel` on the headings.
- [ ] Usable at 100%, 150%, and 200% scaling, with text scaling (Settings → Accessibility → Text size) at 150%.

## Deliverable
Fix whatever the pass finds. Record any intentionally deferred items in the ticket.
