# KC-19 — Design: Browse file vs folder, and paste — RESOLVED

**Area:** Design · **Status:** Resolved by [`docs/design-reference`](<../design-reference/src/App.tsx>) (formerly `Figma Redesign Keycuts UI2`). No engineering work in this ticket.

## The questions
1. **File vs folder.** Windows has no native dialog that can pick *either* a file or a folder, so a single Browse button had to
   offer a choice.
2. **Paste.** The first redesign removed the destination text box, but paste is a required input method. It's the main way to add a URL
   copied from chat or email.

## Design decision (UI2)
- **Browse ▾ menu.** The Browse button gets a trailing chevron and opens a small menu:
  - **File…**: *Choose a single file*
  - **Folder…**: *Choose a directory*
- **Inline destination field.** The destination line inside the drop area is now a borderless text input with the placeholder
  **Drop or paste a file, folder, or URL here**. Users can paste or type directly, and it's still the single destination display.
- **Focus-within styling.** The drop area shows an accent border and soft glow while its contents have focus.
- **Name form always visible.** Create is disabled until both a destination and a name exist.

## Where it's implemented
- Layout and states: [KC-09](KC-09-creator-layout-and-states.md)
- Drop, paste/type, and Browse behavior: [KC-10](KC-10-creator-input-drop-paste-browse.md)
- Keyboard checks: [KC-17](KC-17-keyboard-focus-accessibility.md)
