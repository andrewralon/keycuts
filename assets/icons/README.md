# keycuts icons

The app icon is a single keycap with a K, lying on the table. `tools/New-AppIcons.ps1` makes everything here; don't edit the files by hand.

| Folder | What it is | In the app |
|---|---|---|
| `porcelain` | Charcoal keycap on a light background | Porcelain; Black & White in light mode; the exe's and the installer's icon |
| `onyx` | White keycap on a dark background | Onyx; Black & White in dark mode |
| `cobalt` | Blue keycap on a light background | Cobalt; Accent in light mode |
| `twilight` | Blue keycap on a dark background | Twilight; Accent in dark mode |
| `classic` | The original Team Ralon monogram on a white square (`source.ico` is keycuts' old icon) | Classic |
| `key-white`, `key-charcoal`, `key-blue` | The keycaps alone on a transparent background | not offered |
| `classic-transparent` | The monogram alone on a transparent background | not offered |

Each folder has a `.ico` with 16, 20, 24, 32, 40, 48, 64, 96 and 256 px, a PNG of each of those sizes, and (for the keycaps) a 1024 px PNG for anything bigger. Microsoft asks for at least 16, 24, 32, 48 and 256; the others are what the title bar, the taskbar and Explorer ask for at 125, 150 and 200 percent scaling, so Windows only ever scales down from a near size. At 24 px and below the key is drawn flat with a heavier K, since the shaded one turns to mush. The monogram's largest original is 256 px, so it has no 1024 px version.

## Regenerating

```powershell
pwsh -File tools/New-AppIcons.ps1
```

It draws the keycaps with `tools/icons/app-icon.html` and `tools/icons/keycap.js` in the Edge or Chrome already installed (nothing is downloaded), on a transparent page, and cuts the screenshot into sizes. `app-icon.html?preview` shows them all on light and dark. Look at the PNGs before committing. The design history is in `docs/mockups/logo-concepts-v*.html`.
