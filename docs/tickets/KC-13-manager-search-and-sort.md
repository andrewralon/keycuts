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
- [x] Searching "vicki" finds `l-vicki` by name and by its URL. Searching "folder" finds every Folder-type item. Searching "logs" matches the description.
- [x] All three sortable columns sort both ways, and the sort persists while searching.
- [x] View model unit tests cover the filter predicate and sort comparers.

## Implementation notes

**Files changed**
- `keycuts.GUI/ViewModels/ManagerViewModel.cs`: `SortColumn` enum, `SortColumn`/`SortDescending`/`SortCommand`, `SortBy`, static `CreateComparer`, and `ApplyFilter` now filters and sorts.
- `keycuts.GUI/ViewModels/KeycutRowViewModel.cs`: `TypeLabel` and `Matches(search)`.
- `keycuts.GUI/Controls/SortHeaderButton.cs` (new): header button exposing sort state and UIA help text ("Sorted ascending/descending").
- `keycuts.GUI/Views/ManagerView.xaml`: sortable Shortcut/Type/Description headers with chevron and accent styling.
- `keycuts.GUI/Mvvm/RelayCommand.cs`: added `RelayCommand<T>`.
- `keycuts.Tests/ManagerSearchSortTests.cs` (new): 24 test cases (predicate, comparers, sort toggling, persistence, no-op rebuild).

**Decisions**
- Search ignores leading/trailing whitespace; blank search matches everything. The type label searched is the badge label (`TypeBadge.GetLabel`).
- Descending reverses the primary column only; the name tie-breaker is always ascending.
- Search and sort state live on `ManagerViewModel`, so refreshes keep them. `ShowKeycuts` is only called when content changed, and `ApplyFilter` leaves `Rows` untouched when the visible sequence is identical, so scroll is preserved.
- Escape order and the reset on returning to the creator were already in `ShellViewModel` (KC-08/KC-12) and are unchanged.

**Deviations**
- No `ICollectionView`: `Rows` remains an `ObservableCollection` of the filtered, sorted rows, rebuilt from a pure predicate and comparer. This keeps the KC-12 API and the logic unit-testable. It still never touches the disk.

**Manual QA still needed**
- Live look of headers: hover chevron, accent on the active column, keyboard focus ring, light/dark and high contrast.
- Tab to a header and press Enter/Space; Narrator reads "Sorted ascending/descending".
- Scroll position kept when typing a search that doesn't change the rows, and on F5/window activation.
- Escape clears search; search clears on return to the creator; "Clear search" in the no-results state.
- Only a smoke launch (creator view) was done headlessly; the manager view itself was not visually inspected.
