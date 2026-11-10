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

Download the installer for your PC from the [latest release](https://github.com/andrewralon/keycuts/releases/latest) and run it:
`keycuts-setup-x64.msi` for most PCs, or `keycuts-setup-arm64.msi` for Windows on ARM (Snapdragon and other ARM laptops; the x64
one works there too, more slowly). Settings → System → About shows which you have. Installing one replaces the other. It installs for your account only and **doesn't need admin rights**: files go to
`%LOCALAPPDATA%\Programs\keycuts`, and the right-click menu and `PATH` entries go in your own registry hive. The .NET runtime
is included. Turn the right-click menu off and on in Settings; on Windows 11 it appears under **Show more options**.

The installer asks where to keep your keycuts and adds that folder to your `PATH`. The default is `%USERPROFILE%\Shortcuts`, and any
folder you can write to works, such as `C:\Shortcuts`. Upgrades keep your folder without asking; change it later in Settings. For a
silent install, pass it on the command line: `msiexec /i keycuts-setup-x64.msi /qn OUTPUTFOLDER="C:\Shortcuts"`.

Uninstalling removes the app, the menu, and the `PATH` entries. Your keycuts and settings stay.

To build the installer, run `pwsh -File tools/Build-Installer.ps1` (add `-Runtime win-arm64` for the ARM one). It needs only the
.NET SDK.

Releases are automatic: raise `<Version>` in `Directory.Build.props`, then merge `develop` into `main`. The release workflow tags
`v<Version>` and publishes both MSIs, a portable zip for each (`keycuts-portable-win-x64.zip`, `keycuts-portable-win-arm64.zip`),
and SHA-256 checksums. Merges that don't change the version don't release.

## The app

The app is one compact window with two views.

**Creator:** drop a file, folder, shortcut (`.lnk`/`.url`), or browser link onto the window, paste a path or URL
(Ctrl+V, including a file copied in Explorer), or use **Browse ▾** (File… / Folder…). Then type a name and press **Enter**.
The type icon updates as you go, and invalid destinations or duplicate names are shown inline.
For a file or folder under a known folder, a "Saved as" line shows the path exactly as it will be saved: with
environment variables (`%USERPROFILE%\Desktop\notes.txt`) if **Use Environment Variables in Paths** is on in Settings,
or as typed if it's off. Holding **Shift** flips that for the keycut you're creating, and the line follows.

**First run:** the first time keycuts opens, and whenever your keycuts folder is empty, the creator shows a welcome
panel that explains the Win+R workflow and offers a keycut to keycuts itself, named `kc` unless you change it, so
Win+R → `kc` opens the app. (Not `keycuts`: that's the command-line tool, `keycuts.exe`, which is on your PATH too.)
**Got it** or creating that keycut records that you've seen it (`WelcomeDismissed` in `HKCU\Software\TeamRalon\keycuts`),
so it only comes back while the folder is empty, or when you choose **Show Welcome** in Settings. If the keycuts folder
isn't on your PATH (a portable copy; the installer adds it), the panel says Win+R can't find the new keycut yet and
offers **Add to PATH**.

**Keycut manager:** **View all keycuts** switches the same window to a table of your keycuts (it replaces the old separate
Batmanager app, which has been removed).
* Search by name, type, description, or destination. Escape clears the search.
* Up and Down select a row, even while you're typing in the search box, and Enter opens the selected keycut. Clicking a row selects it too.
* F2 (or **Rename** in the row menu) renames the selected keycut in place: Enter or clicking away saves, Escape cancels.
  A name that's already taken is refused, and anything you added to the `.bat` by hand is kept.
* Sort by Shortcut, Type, or Description by clicking a header.
* The row menu (the `...` button) offers **Open**, **Edit** (opens the `.bat` in Notepad; a description added there shows in the table),
  **Open file location** (shows the `.bat` in Explorer), **Copy destination**, and **Delete**. Delete has no confirmation.
* Missing destinations are flagged. The list refreshes when the window is activated and on F5.

Settings has a theme choice: System (the default), which follows Windows live, Light, or Dark. A click previews the theme, and Save keeps it. Windows high contrast always wins over the choice.

**Use Environment Variables in Paths** (off by default) saves new file and folder keycuts with environment variables,
so a keycuts folder copied to another machine or account still works. It uses `%LOCALAPPDATA%`, `%APPDATA%`,
`%OneDrive%`, `%USERPROFILE%`, `%ProgramFiles(x86)%`, `%ProgramFiles%`, `%ProgramData%` and `%SystemRoot%`, plus any
variable you've defined yourself whose value is a folder (a `DROPBOX` variable, say); the longest match wins. Existing
keycuts aren't changed, and the command-line `keycuts.exe` follows the setting too.

Screenshots aren't included: they couldn't be captured headlessly and should be added by hand.

### Debug-only sample data

Debug builds honor the `KEYCUTS_OUTPUT_FOLDER` environment variable to point the app at another output folder without touching the
registry or PATH. Generate fixtures with `tools/New-SampleKeycuts.ps1`:

```powershell
.\tools\New-SampleKeycuts.ps1 -Folder "$env:TEMP\keycuts-fixtures-100" -Count 100
$env:KEYCUTS_OUTPUT_FOLDER = "$env:TEMP\keycuts-fixtures-100"
```

The script refuses to write into `C:\Shortcuts`, `%USERPROFILE%\Shortcuts`, or your configured output folder. The override is ignored in Release builds.

## Copying a destination: `keypaste`

`keypaste db` (or `keycuts paste db`), typed in Win+R or a terminal, copies the `db` keycut's destination to the
clipboard and prints it. A file or folder saved with an environment variable (`%USERPROFILE%\Desktop`) is copied as the
real path, so it works when pasted into Explorer or a terminal; a URL is copied as written. Given a text file's name
instead, it copies the file's contents, as written. It looks in the current
folder, then your keycuts folder, then the keycuts install folder, and tries the name as typed, then with `.txt`, then
with `.bat`. `keypaste` used to be a separate program; it's now a small `keypaste.cmd` beside `keycuts.exe` that runs
`keycuts paste`.

If you made a keycut to the old `keypaste.exe` (a `keypaste.bat` in your keycuts folder, say), delete it once
`keypaste.exe` is gone. Windows finds a keycut named `keypaste` before the new command, so one that points at a missing
`keypaste.exe` stops `keypaste` from working.

## Development

Requires the .NET SDK 10. Run from the repository root:

* Format check: `dotnet format keycuts.sln --verify-no-changes`
* Build (also the type-check): `dotnet build keycuts.sln`
* Test: `dotnet test keycuts.sln` (xUnit project `keycuts.Tests`; tests must use temp folders, never the real output folder or the registry)

Known baselines: the build has 27 CA1416 (Windows-only API) warnings (count with `--no-incremental`), so `-warnaserror`
isn't used. The format check is clean at error severity: `dotnet format keycuts.sln --verify-no-changes --severity error`
(without `--severity error` it also reports the 27 baseline CA1416 warnings and exits non-zero). Keep it clean.
