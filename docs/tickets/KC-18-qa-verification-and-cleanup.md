# KC-18 — QA verification, docs, and cleanup

**Area:** Release · **Depends on:** all previous tickets

## Why
This closes out the feature: verify everything the spec lists, run the repository's prescribed checks, and remove
leftovers from the old UI without touching unrelated working behavior.

## Automated checks (commands from KC-01)
- [ ] `dotnet format keycuts.sln --verify-no-changes`
- [ ] `dotnet build keycuts.sln` (no new warnings compared with the recorded baseline)
- [ ] `dotnet test keycuts.sln`

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
- [ ] Remove dead code from the old creator: the `Steps`/`Instructions` properties, duplicate per-grid drag handlers,
      `MainFormLogic.ClearDestination`, the "Open Batmanager" button, the `Thread.Sleep` and `MessageBox` paths, and `CommonLogic`
      if it's fully superseded. Keep anything the CLI still uses. Batmanager removal itself is KC-20.
- [ ] Don't commit either prototype's `node_modules`. Decide whether to keep `Figma Redesign Keycuts UI2` in the repo as the design
      reference. The superseded `Figma Redesign Keycuts UI` folder and the stray empty `keycuts-prototype/` folder (only a `.git`) can be removed.
      All three are currently untracked.
- [ ] Update the root `README.md`: describe the creator flow (drop, paste, or browse → name → Enter) and the in-app manager,
      and refresh screenshots if any are added.
- [ ] Capture before and after screenshots of the creator and manager in both themes for the PR.
