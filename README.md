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

## Install

Download `keycuts-setup.msi` from the [latest release](https://github.com/andrewralon/keycuts/releases/latest) and run it. It installs for your account only and **doesn't need admin rights**: files go to
`%LOCALAPPDATA%\Programs\keycuts`, and the right-click menu and `PATH` entries go in your own registry hive. The .NET runtime
is included. Turn the right-click menu off and on in Settings; on Windows 11 it appears under **Show more options**.

The installer asks where to keep your keycuts and adds that folder to your `PATH`. The default is `%USERPROFILE%\Shortcuts`, and any
folder you can write to works, such as `C:\Shortcuts`. Upgrades keep your folder without asking; change it later in Settings. For a
silent install, pass it on the command line: `msiexec /i keycuts-setup.msi /qn OUTPUTFOLDER="C:\Shortcuts"`.

Uninstalling removes the app, the menu, and the `PATH` entries. Your keycuts and settings stay.

To build the installer, run `pwsh -File tools/Build-Installer.ps1`. It needs only the .NET SDK.

Releases are automatic: raise `<Version>` in `Directory.Build.props`, then merge `develop` into `main`. The release workflow tags
`v<Version>` and publishes the MSI, a portable zip, and SHA-256 checksums. Merges that don't change the version don't release.

## The app

The app is one compact window with two views.

**Creator:** drop a file, folder, shortcut (`.lnk`/`.url`), or browser link onto the window, paste a path or URL
(Ctrl+V, including a file copied in Explorer), or use **Browse ▾** (File… / Folder…). Then type a name and press **Enter**.
The type icon updates as you go, and invalid destinations or duplicate names are shown inline.

**Keycut manager:** **View all keycuts** switches the same window to a table of your keycuts (it replaces the old separate
Batmanager app, which has been removed).
* Search by name, type, description, or destination. Escape clears the search.
* Up and Down select a row, even while you're typing in the search box, and Enter opens the selected keycut. Clicking a row selects it too.
* Sort by Shortcut, Type, or Description by clicking a header.
* The row menu (the `...` button) offers **Open**, **Edit** (opens the `.bat` in Notepad; a description added there shows in the table),
  **Copy destination**, and **Delete**. Delete has no confirmation.
* Missing destinations are flagged. The list refreshes when the window is activated and on F5.

Settings has a theme choice: System (the default), which follows Windows live, Light, or Dark. A click previews the theme, and Save keeps it. Windows high contrast always wins over the choice.

Screenshots aren't included: they couldn't be captured headlessly and should be added by hand.

### Debug-only sample data

Debug builds honor the `KEYCUTS_OUTPUT_FOLDER` environment variable to point the app at another output folder without touching the
registry or PATH. Generate fixtures with `tools/New-SampleKeycuts.ps1`:

```powershell
.\tools\New-SampleKeycuts.ps1 -Folder "$env:TEMP\keycuts-fixtures-100" -Count 100
$env:KEYCUTS_OUTPUT_FOLDER = "$env:TEMP\keycuts-fixtures-100"
```

The script refuses to write into `C:\Shortcuts`, `%USERPROFILE%\Shortcuts`, or your configured output folder. The override is ignored in Release builds.

## Development

Requires the .NET SDK 10. Run from the repository root:

* Format check: `dotnet format keycuts.sln --verify-no-changes`
* Build (also the type-check): `dotnet build keycuts.sln`
* Test: `dotnet test keycuts.sln` (xUnit project `keycuts.Tests`; tests must use temp folders, never the real output folder or the registry)

Known baselines: the build has 27 CA1416 (Windows-only API) warnings (count with `--no-incremental`), so `-warnaserror`
isn't used. The format check is clean at error severity: `dotnet format keycuts.sln --verify-no-changes --severity error`
(without `--severity error` it also reports the 27 baseline CA1416 warnings and exits non-zero). Keep it clean.
