# KC-06 — Icon library

**Area:** keycuts.GUI · **Depends on:** 05 · **Blocks:** 07, 09, 12, 14

## Why
The design uses 16–20px outline icons that inherit the text color: file, folder, link, play (command), settings, search,
more (three dots), copy, delete, external, arrow, and spark (brand/"New keycut"). The app currently uses one PNG
(`Images/Antu_codelite.svg_30x30.png`) for settings.

## Scope
- Convert the SVGs in [`public/icons/`](<../../Figma Redesign Keycuts UI2/public/icons/>) to `Geometry` resources in
  `Themes/Icons.xaml`. Also convert the icons defined only in `App.tsx` `iconPaths`: `arrow`, `check`, `chevron`, `close`, and `spark`.
  Keep the 24×24 viewbox, 1.7 stroke width, and round caps and joins.
- Add a small `Icon` control (or a `Path` style) with `Kind` and `Size` properties. Its stroke binds to `Foreground`
  so icons follow theme and state colors.
- Icons are decorative. Hide them from UI Automation unless they're the only content of a button, in which case the
  button carries `AutomationProperties.Name`.

## Acceptance criteria
- [x] (geometry/parse verified by unit tests; visual check pending, see notes) All the icons above render crisply at 12, 16, 18, and 23px in both themes, with no blurry raster.
- [x] The old settings PNG is no longer referenced by the new creator view. Delete the file only if nothing else uses it.

## Implementation notes
**Files changed**
- New `keycuts.GUI/Themes/Icons.xaml`: 15 `PathGeometry` resources keyed `Icon.<Kind>` (arrow, check, chevron, close, copy, delete, external, file, folder, link, more, play, search, settings, spark) on a 24x24 viewbox.
- New `keycuts.GUI/Controls/Icon.cs`: `IconKind` enum and `Icon` control (`Kind`, `Size` default 18, inherited `Foreground`). Draws a 1.7 round-cap/round-join stroke scaled by `Size/24`; `More` is filled. Resources resolve via `TryFindResource`, and the foreground follows `DynamicResource` brushes, so icons follow theme changes.
- `keycuts.GUI/App.xaml`: merged `Themes/Icons.xaml` (before Controls.xaml; ThemeManager's palette swap is unaffected).
- New `keycuts.Tests/IconTests.cs`: every kind has a geometry, no stray keys, all geometries parse and fit the viewbox, filled/key helpers.

**Decisions / deviations**
- Usage: `<controls:Icon Kind="Folder" Size="16" Foreground="{DynamicResource TextSecondaryBrush}"/>`; by default it inherits Foreground from the parent (Button/TextBlock).
- Geometries are vector, so they stay crisp at any size. `settings` uses the SVG file's geometry (the `iconPaths` version has a transform hack). `moon`/`sun` were not ported (theme toggle is out of scope per decision 6). Circles/rects were converted to arc path data so each icon is one string.
- Icons have no automation peer, so UI Automation skips them. Icon-only buttons must set `AutomationProperties.Name` (KC-09/12/14).
- The old settings PNG (`Images/Antu_codelite.svg_30x30.png`) is still referenced by the legacy `MainWindow.xaml` only. KC-09 replaces that view, and the file should be deleted then; the new creator view doesn't use it.

**Manual QA still needed**
- Visually check every icon at 12, 16, 18, and 23px in Light, Dark, and High Contrast (nothing renders headlessly here), including at 125%/150% DPI.
- Check that the icon color changes live on a theme switch.