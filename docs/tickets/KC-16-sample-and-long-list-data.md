# KC-16 — Sample and long-list development data

**Area:** Dev tooling · **Depends on:** 03

## Why
Several things need realistic data without touching a developer's real `C:\Shortcuts`: design-time previews, manual QA of
empty, populated, error, and long-list states, and the 20–100 item usability check.

## Scope
- **Canonical sample entries.** Use these wherever sample data is needed:

| Shortcut | Type | Description | Destination |
| --- | --- | --- | --- |
| `l-vicki` | URL | logs for vicki | an example logs URL, e.g. `https://logs.example.com/vicki` |
| `downloads` | Folder | Downloads folder | `C:\...\Downloads` (use `%USERPROFILE%\Downloads` when generating real files) |

- **Design-time data:** `d:DataContext` sample view models for `CreatorView` (one per state) and `ManagerView`
  (empty, two items, and ~25 items), so the XAML designer and Hot Reload show realistic content.
- **Fixture generator:** `tools/New-SampleKeycuts.ps1 -Folder <path> -Count <n>` writes `.bat` files in the exact format
  that `Runner` produces, including the KC-03 description line. It includes the two canonical entries, plus mixed
  URL, Folder, File, and Command items, long names, long descriptions, very long destinations (200+ chars), and a few File items
  whose targets don't exist (for the Missing state). Generate 20 and 100 item sets.
- **How to point the app at fixtures:** document temporarily changing the output folder in Settings. Alternatively, add a
  debug-only override such as the `KEYCUTS_OUTPUT_FOLDER` env var, read only in `DEBUG` builds, so QA never overwrites the real registry value.
- The test project (KC-01) reuses the same generator logic, or a C# equivalent, for repository tests.

## Acceptance criteria
- [ ] One command produces a 100-item fixture folder that the manager loads.
- [ ] Design-time previews show `l-vicki` and `downloads` with the correct badges.
- [ ] No sample data ships in release builds or is written to the real output folder by default.
