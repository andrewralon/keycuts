# keycuts
keyboard shortcuts for nerds.... Open any file/folder/URL with Win+R and the shortcut name.

keycuts is a tool for creating simple scripts to open **anything** on your computer using a nickname.

How do you launch a keycut?
* Type "Win+R", the shortcut name, and Enter.

Examples:
* "npp" to open Notepad++
* "rb" to open Recycle Bin
* "gmail" to open a new tab for email
* "timesheet" open a new tab for timesheet
* "db" to open Dropbox folder
* "ss" to open screenshots folder

Create keycuts several ways:
* GUI -- open the app, give it a destination, name it, and press Enter
* Right click menu -- opens the app with the destination filled in
* CLI -- for all the hard-core nerds and brogrammers
* Common -- for forking your own implementation

## The app

The app is one compact window with two views.

**Creator:** drop a file, folder, shortcut (`.lnk`/`.url`), or browser link onto the window, paste a path or URL
(Ctrl+V, including a file copied in Explorer), or use **Browse ▾** (File… / Folder…). Then type a name and press **Enter**.
The type icon updates as you go, and invalid destinations or duplicate names are shown inline.

**Keycut manager:** **View all keycuts** switches the same window to a table of your keycuts (it replaces the old separate
Batmanager app, which has been removed).
* Search by name, type, description, or destination. Escape clears the search.
* Sort by Shortcut, Type, or Description by clicking a header.
* The row menu (the `...` button) offers **Open**, **Edit** (opens the `.bat` in Notepad; a description added there shows in the table),
  **Copy destination**, and **Delete**. Delete has no confirmation.
* Missing destinations are flagged. The list refreshes when the window is activated and on F5.

The app follows the Windows light/dark setting and high-contrast themes.

Screenshots aren't included: they couldn't be captured headlessly and should be added by hand.

### Debug-only sample data

Debug builds honor the `KEYCUTS_OUTPUT_FOLDER` environment variable to point the app at another output folder without touching the
registry or PATH. Generate fixtures with `tools/New-SampleKeycuts.ps1`:

```powershell
.\tools\New-SampleKeycuts.ps1 -Folder "$env:TEMP\keycuts-fixtures-100" -Count 100
$env:KEYCUTS_OUTPUT_FOLDER = "$env:TEMP\keycuts-fixtures-100"
```

The script refuses to write into the real output folder (default `C:\Shortcuts`). The override is ignored in Release builds.

## Development

Requires the .NET SDK 10. Run from the repository root:

* Format check: `dotnet format keycuts.sln --verify-no-changes`
* Build (also the type-check): `dotnet build keycuts.sln`
* Test: `dotnet test keycuts.sln` (xUnit project `keycuts.Tests`; tests must use temp folders, never the real output folder or the registry)

Known baselines: the build has 27 CA1416 (Windows-only API) warnings (count with `--no-incremental`), so `-warnaserror`
isn't used. The format check reports 28 pre-existing whitespace errors in 7 files; new code must not add to them.
