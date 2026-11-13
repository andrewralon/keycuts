# keycuts

Open any file, folder or URL on your PC by typing a nickname in Win+R.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/walkthrough-dark.gif">
  <img src="docs/images/walkthrough-light.gif" alt="Creating a keycut: a Gmail link is the target, the name gmail is typed, Enter creates it. Then Win+R, gmail, Enter opens it." width="424">
</picture>

A keycut is a tiny `.bat` file in a folder on your `PATH`, so Win+R finds it by name:

* `npp` opens Notepad++, `gmail` opens your email, `db` opens your Dropbox folder, `rb` opens the Recycle Bin.

Make one by dropping a target on the app and naming it, from the right-click menu (**keycut this!**), or from the CLI.

## Install

Download the MSI from the [latest release](https://github.com/andrewralon/keycuts/releases/latest): `keycuts-setup-x64.msi` for most PCs, `keycuts-setup-arm64.msi` for Windows on ARM. It installs for your account only (no admin rights) and includes the .NET runtime. It asks where to keep your keycuts (default `%USERPROFILE%\Shortcuts`) and adds that folder to your `PATH`. Silent install: `msiexec /i keycuts-setup-x64.msi /qn OUTPUTFOLDER="C:\Shortcuts"`. Uninstalling keeps your keycuts and settings.

## Using it

The screenshots follow your GitHub theme; the app follows Windows, or the choice in Settings.

**Creator.** Drop a file, folder, `.lnk` or link on the window (or paste, or **Browse**), type a name, press **Enter**. **Add a description** if you like. On the first run, and whenever your keycuts folder is empty, a welcome panel offers a keycut named `kc` that opens the app.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/creator-dark.png">
  <img src="docs/images/creator-light.png" alt="The creator, ready to create." width="424">
</picture>

**Manager.** **View all keycuts** shows a table you can search and sort by any column, including **Modified**. Up/Down, Home/End and Page Up/Page Down move the selection, Enter opens the row, and from the search box Ctrl+E edits it, Ctrl+Shift+E edits it in Notepad, Ctrl+Shift+O shows its file, Ctrl+C copies its target, F2 renames it, Delete deletes it and Ctrl+F searches. Delete and Home/End are the search box's while it holds text, and Ctrl+C while text is selected in it. The `...` menu has **Open**, **Edit**, **Edit in Notepad**, **Open file location**, **Copy target** and **Delete**, each with its key. Missing targets are flagged in red, and a hand-written launcher that still starts but names a start-in folder that doesn't exist gets a yellow warning.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/manager-dark.png">
  <img src="docs/images/manager-light.png" alt="The manager: a searchable table of keycuts, one flagged as missing its target." width="830">
</picture>

* **Rename and Edit description** change the name or the description where it is: click it on the selected row (a click on another row only selects it), or press F2 or Shift+F2. Enter saves, Escape cancels, and clicking away saves. A description change rewrites just that line of the `.bat`, so it works on a keycut you've changed by hand too.
* **Column widths** can be dragged: the divider after **Name** and the one between **Description** and **Target** (the line shows when you point at it). The other columns give way and the table never scrolls sideways; the whole text of a cell is in its tooltip. Widths go back to normal when the app restarts.
* **Edit** changes the target, name and description. It's offered only for a keycut the app would have written itself; one you've changed by hand says to use **Edit in Notepad**, so your edits are never lost.
* **Delete** moves the file to the Recycle Bin (drives without one, such as network shares, delete for good). **Undo** (or Ctrl+Z) shows in the status bar for a few seconds and restores the file's contents and modified date.

**Settings.** The output folder, **Ask before replacing a keycut**, the right-click menu (on Windows 11 under **Show more options**), the theme and update checks. **Use environment variables in paths** (off by default) saves paths like `%USERPROFILE%\Desktop`, so a keycuts folder still works on another PC; hold Shift in the Creator to flip it for one keycut.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/settings-dark.png">
  <img src="docs/images/settings-light.png" alt="The Settings window." width="484">
</picture>

**Updates.** When a newer release exists, the Creator's footer says so and **Download** opens the release page. The app checks at most once a day, with one unauthenticated request to GitHub and no telemetry; the CLI never checks. Turn it off in Settings.

## Command line

* `keycuts -t <target> -s <name> [--description "text"]` creates a keycut. It asks the same name questions as the app; `-f` skips them, and with redirected input it exits with code 14 instead of waiting.
* `keypaste db` (or `keycuts paste db`) copies the `db` keycut's target to the clipboard.
* `keycuts` alone opens the app; `keycuts -h` lists the options.

A keycut can't be named `keycuts` or `keypaste`; existing ones are renamed to `*_previous` at startup.

## Development

.NET SDK 10. From the repository root:

* Format: `dotnet format keycuts.sln --verify-no-changes --severity error`
* Build: `dotnet build keycuts.sln`
* Test: `dotnet test keycuts.sln`
* Installer: `pwsh -File tools/Build-Installer.ps1` (`-Runtime win-arm64` for ARM)
* Sample keycuts: `tools/New-SampleKeycuts.ps1` (use with `KEYCUTS_OUTPUT_FOLDER` in a Debug build)
* Screenshots and walkthrough: `pwsh -File tools/New-Screenshots.ps1`

**Releasing.** Raise `<Version>` in `Directory.Build.props`, run the screenshots script, then merge `develop` into `main`.
