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
- [ ] All four badges render correctly in light and dark, and the label text is legible (AA) on its badge background.
- [ ] Badges are readable in grayscale, because the icon and label differ.
- [ ] In high contrast mode the badges fall back to system colors and keep their icon and label.
