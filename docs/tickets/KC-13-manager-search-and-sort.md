# KC-13 — Manager: search and sort

**Area:** keycuts.GUI · **Depends on:** 12 · **Blocks:** 17

## Why
With 20–100 items, users need to find a keycut quickly and order the list. The search and sort must run on real data, not prototype mocks.

## Scope
- **Search**
  - A case-insensitive substring match across **shortcut name, type label, description, and destination**.
    The prototype concatenates all four fields; do the same.
  - Live filtering as the user types. Debounce isn't needed at 100 items.
  - Use an `ICollectionView` filter over the loaded list, so search doesn't touch the disk.
  - The result count (KC-12) updates live. The status bar total stays unchanged.
  - Escape in a non-empty search box clears it (see KC-08 for the Escape order).
  - The search text is kept while the manager is open, and resets when returning to the creator.
- **Sort**
  - Sortable headers: **Shortcut**, **Type**, and **Description**, as in the prototype. Destination and the menu column aren't sortable.
  - The default is Shortcut ascending. Clicking the active header toggles asc/desc; clicking another header sorts it ascending.
  - Use a culture-aware, case-insensitive comparison, with the name as a stable tie-breaker.
  - The header shows a chevron for the active column and direction, and the active header uses the accent color (prototype `.sort-button.active`).
  - Headers are keyboard operable: Tab to the header, then Enter or Space to sort. Expose the sort state through UI Automation,
    for example `AutomationProperties.HelpText` set to "Sorted ascending".
  - **Don't** show "Sorted by …" text anywhere.

## Acceptance criteria
- [ ] Searching "vicki" finds `l-vicki` by name and by its URL. Searching "folder" finds every Folder-type item. Searching "logs" matches the description.
- [ ] All three sortable columns sort both ways, and the sort persists while searching.
- [ ] View model unit tests cover the filter predicate and sort comparers.
