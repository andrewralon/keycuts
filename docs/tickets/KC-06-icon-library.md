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
- [ ] All the icons above render crisply at 12, 16, 18, and 23px in both themes, with no blurry raster.
- [ ] The old settings PNG is no longer referenced by the new creator view. Delete the file only if nothing else uses it.
