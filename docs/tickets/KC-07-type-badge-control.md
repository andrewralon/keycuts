# KC-07 — Type badge control

**Area:** keycuts.GUI · **Depends on:** 05, 06 · **Blocks:** 12

## Why
Users scan and group keycuts by type. Badges must be compact and distinct, and contain **icon + text**,
so color is never the only cue.

## Scope
- A `TypeBadge` control bound to the display kind from KC-02 (`Url | Folder | File | Command`).
  Labels: **URL**, **Folder**, **File**, **Command**. Icons: link, folder, file, play.
- 22px tall, 5px radius, 1px border, tight padding, and a small bold label.
- Colors are copied exactly from `index.css` and added as theme brushes in KC-05:

| Kind | Light fg / bg / border | Dark fg / bg / border |
| --- | --- | --- |
| URL | `#6d43b8` / `#f3edff` / `#dfd2f7` | `#c4a1ff` / `#33254c` / `#574279` |
| File | `#1c619d` / `#e9f4ff` / `#cfe6fa` | `#86c8ff` / `#18354e` / `#295879` |
| Folder | `#8a5700` / `#fff5dc` / `#f1dfae` | `#f0c365` / `#41351e` / `#62502a` |
| Command | `#14734a` / `#e8f7ef` / `#c8ead8` | `#75dca8` / `#193c2c` / `#285e45` |

- `AutomationProperties.Name` = "Type: URL" and so on.

## Acceptance criteria
- [x] All four badges render correctly in light and dark, and the label text is legible (AA) on its badge background. (Unit-tested: exact hex values and AA contrast; a throwaway STA test confirmed the template loads and measures 22px tall in all palettes. Visual check still manual.)
- [x] Badges are readable in grayscale, because the icon and label differ. (Unit-tested: distinct icon and label per kind.)
- [x] In high contrast mode the badges fall back to system colors and keep their icon and label. (Unit-tested: all 12 brushes in HighContrast.xaml use system colors; the template is shared.)

## Implementation notes

**Files changed**
- New: `keycuts.GUI/Controls/TypeBadge.cs`, `keycuts.Tests/TypeBadgeTests.cs`.
- Changed: `keycuts.GUI/Themes/Controls.xaml` (implicit `TypeBadge` style/template), and `Light.xaml`, `Dark.xaml`, `HighContrast.xaml` (12 `Badge{Url,File,Folder,Command}{Foreground,Background,Border}Brush` entries each).

**Decisions**
- `TypeBadge` is a templated `Control` with `Kind` (`KeycutKind`); `Label` and `IconKind` are read-only DPs derived from it. Use it as `<controls:TypeBadge Kind="{Binding Kind}" />`.
- Name "Type: URL" etc. is exposed via a custom automation peer (Text), so no `AutomationProperties.Name` is needed at use sites. The badge is not focusable or a tab stop.
- Label size uses the `FontSizeEyebrow` token (11px, bold); icon is 12px; padding 5,0,7,0 as in the prototype; radius is `RadiusSm` (5).
- High contrast: foreground and border use `WindowText`, background uses `Window`.

**Deviations:** none.

**Manual QA still needed:** visual check of all four badges in light, dark and high contrast; grayscale look; Narrator reads "Type: URL" etc.; DPI scaling.
