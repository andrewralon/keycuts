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
* GUI -- open the app, give it a target, name it, and press Enter
* Right click menu -- opens the app with the target filled in
* CLI -- for all the hard-core nerds and brogrammers
* Common -- for forking your own implementation

## Install

Download the installer for your PC from the [latest release](https://github.com/andrewralon/keycuts/releases/latest) and run it:
`keycuts-setup-x64.msi` for most PCs, or `keycuts-setup-arm64.msi` for Windows on ARM (Snapdragon and other ARM laptops; the x64
one works there too, more slowly). Settings → System → About shows which you have. Installing one replaces the other. It installs for your account only and **doesn't need admin rights**: files go to
`%LOCALAPPDATA%\Programs\keycuts`, and the right-click menu and `PATH` entries go in your own registry hive. Only the
install folder's `bin` goes on your `PATH`: it holds just the `keycuts` and `keypaste` commands, small `.cmd` files that run
`keycuts.exe` one folder up, so the runtime's files beside it stay off your `PATH`. The .NET runtime
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

**Creator:** drop a file, folder, shortcut (`.lnk`/`.url`), browser link, or a shell item like the Recycle Bin or This PC
(saved as its `::{CLSID}` key) onto the window, paste a path or URL
(Ctrl+V, including a file copied in Explorer), or use **Browse ▾** (File… / Folder…). Then type a name and press **Enter**.
**Add a description** under the name if you want one: it's optional, and the manager shows and searches it.
The type icon updates as you go, and invalid targets and names are shown inline. A name that's already taken, by a
keycut or by another program, shows an amber warning and asks before creating (see [Name warnings](#name-warnings)).
For a file or folder under a known folder, a "Saved as" line shows the path exactly as it will be saved: with
environment variables (`%USERPROFILE%\Desktop\notes.txt`) if **Use environment variables in paths** is on in Settings,
or as typed if it's off. Holding **Shift** flips that for the keycut you're creating, and the line follows.

**First run:** the first time keycuts opens, and whenever your keycuts folder is empty, the creator shows a welcome
panel that explains the Win+R workflow and offers a keycut to keycuts itself, named `kc` unless you change it, so
Win+R → `kc` opens the app. (Not `keycuts`: that's the command-line tool, which is on your PATH too.)
**Got it** or creating that keycut records that you've seen it (`WelcomeDismissed` in `HKCU\Software\TeamRalon\keycuts`),
so it only comes back while the folder is empty, or when you choose **Show welcome** in Settings. If the keycuts folder
isn't on your PATH (a portable copy; the installer adds it), the panel says Win+R can't find the new keycut yet and
offers **Add to PATH**.

**Keycut manager:** **View all keycuts** switches the same window to a table of your keycuts (it replaces the old separate
Batmanager app, which has been removed).
* Search by name, type, description, or target. Escape clears the search.
* Up and Down select a row, even while you're typing in the search box, and Enter opens the selected keycut. Clicking a row selects it too.
* F2 (or **Rename** in the row menu) renames the selected keycut in place: Enter or clicking away saves, Escape cancels.
  A name that's already taken by another keycut is refused, a name another program uses asks first, and anything you
  added to the `.bat` by hand is kept.
* Sort by Name, Type, Description, Target, or Modified by clicking a header; click it again to reverse. **Modified** is when
  the `.bat` was last written ("3 days ago", with the exact time in a tooltip), and its first click puts the newest first.
  Renaming a keycut rewrites its file, so it counts as modified. The column hides itself when the window is narrow.
* The row menu (the `...` button) offers **Open**, **Edit** (opens the `.bat` in Notepad; a description added there shows in the table),
  **Open file location** (shows the `.bat` in Explorer), **Copy target**, and **Delete**. Delete has no confirmation.
* Missing targets are flagged. The list refreshes when the window is activated and on F5.

Settings (the gear button) holds the output folder (the folder button picks one, and the button beside it opens it in Explorer; hover
either to see what it does), **Ask before replacing a keycut**, **Right-click context menus**, **Use environment variables in paths**, the
theme, and **Check for updates**. **Save** (or Enter) checks that the folder can be written to, creating it if needed, and shows any
problem beside the **Output folder** label; Escape or the window's close button closes without saving.

Settings has a theme choice: System (the default), which follows Windows live, Light, or Dark. A click previews the theme, and Save keeps it. Windows high contrast always wins over the choice.

**Use environment variables in paths** (off by default) saves new file and folder keycuts with environment variables,
so a keycuts folder copied to another machine or account still works. It uses `%LOCALAPPDATA%`, `%APPDATA%`,
`%OneDrive%`, `%USERPROFILE%`, `%ProgramFiles(x86)%`, `%ProgramFiles%`, `%ProgramData%` and `%SystemRoot%`, plus any
variable you've defined yourself whose value is a folder (a `DROPBOX` variable, say); the longest match wins. Existing
keycuts aren't changed, and the command-line `keycuts.exe` follows the setting too.

### Update notice

When a newer version is out, the creator's footer says so ("keycuts 1.2.0 is available · Download"). **Download** opens
the release page in your browser; nothing is downloaded or installed by the app. Dismiss the line with **×** and it stays
hidden until the next release after that one. Settings shows the version you're running and has **Check now**, which asks
straight away and always says what it found, including when it couldn't check.

The app checks at most once a day, when it starts, and never slows startup; if the check fails (offline, a proxy, GitHub's
rate limit) it says nothing. Only the app checks; the command-line `keycuts.exe` never does. **No telemetry**: the check
is one unauthenticated request to `https://api.github.com/repos/andrewralon/keycuts/releases/latest`, carrying nothing
beyond any GitHub API call (a `User-Agent` of `keycuts/<version>` and an `Accept` header). To stop it, turn off
**Check for updates** in Settings. Prereleases never show up as updates.

### Name warnings

A keycut is a `.bat` in a folder on your PATH, so its name competes with every other command Windows finds by name.
keycuts checks the name as Win+R would look it up: the Windows folder, System32, each PATH folder (machine, then user;
each with every PATHEXT extension in order), and last the programs registered under App Paths. It warns about two cases:

* **The keycut would never run.** Something earlier wins, such as `notepad` (in the Windows folder), a program in a PATH
  folder listed before your keycuts folder, or a `.com` or `.exe` with the same name in the keycuts folder itself.
* **The keycut would hide a program.** Win+R would open the keycut instead of a program found later, such as `winword`
  (registered under App Paths), `code`, or an app alias like `winget`.

The warning names the program's full path. Replacing an existing keycut asks too, and shows what it opens now next to
what it will open. In the creator the warning shows as you type; **Create** (or Enter) then opens a confirmation, where
Enter goes ahead and Escape cancels. Renaming in the manager asks the same way, and a name taken by another keycut is
still refused.

**Ask before replacing a keycut** (on by default) controls only the replace question: turn it off to replace keycuts
without asking. It replaced the old **Force overwrite** toggle, and that setting carries over (Force overwrite on means
"don't ask"). Name warnings always show, whatever this setting says, because they affect other programs.

The command line asks the same questions: `keycuts -t <target> -s <name>` prints the warnings and asks `y / n`.
`-f` skips both questions. With redirected input (a script), nothing is created and it exits with code 14
(`ConfirmationNeeded`) instead of waiting for an answer.

cmd built-ins (`dir`, `start`, `copy`) aren't checked: they only matter when a name is typed in a cmd prompt, not in Win+R.

Screenshots aren't included: they couldn't be captured headlessly and should be added by hand.

### Debug-only sample data

Debug builds honor the `KEYCUTS_OUTPUT_FOLDER` environment variable to point the app at another output folder without touching the
registry or PATH. Generate fixtures with `tools/New-SampleKeycuts.ps1`:

```powershell
.\tools\New-SampleKeycuts.ps1 -Folder "$env:TEMP\keycuts-fixtures-100" -Count 100
$env:KEYCUTS_OUTPUT_FOLDER = "$env:TEMP\keycuts-fixtures-100"
```

The script refuses to write into `C:\Shortcuts`, `%USERPROFILE%\Shortcuts`, or your configured output folder. The override is ignored in Release builds.

## Copying a target: `keypaste`

`keypaste db` (or `keycuts paste db`), typed in Win+R or a terminal, copies the `db` keycut's target to the
clipboard and prints it. A file or folder saved with an environment variable (`%USERPROFILE%\Desktop`) is copied as the
real path, so it works when pasted into Explorer or a terminal; a URL is copied as written. Given a text file's name
instead, it copies the file's contents, as written. It looks in the current
folder, then your keycuts folder, then the keycuts install folder, and tries the name as typed, then with `.txt`, then
with `.bat`. `keypaste` used to be a separate program; it's now a small `keypaste.cmd` in the install folder's `bin` that runs
`keycuts paste`.

`keycuts` on its own, typed in Win+R or a terminal, opens the app (`kc` does too once you've made its keycut from the welcome
panel). Give it any argument, even `--help`, and it's the command line. `keycuts -h` (or `--help`) lists the commands and options and exits 0; a
mistake in the arguments prints a short message to stderr and exits 1.

`keycuts -t <target> -s <name> --description "text"` stores a description, as the creator's field does. It has no short
letter: `-d` was the old `--destination` (now `--target`), and a script that still passes it should fail rather than
quietly get a description.

A keycut can't be named `keycuts` or `keypaste`: Windows would find it instead of the command, or the other way round.
If your keycuts folder already has one (a `keypaste.bat` made for the old `keypaste.exe`, say), keycuts renames it to
`keypaste_previous` (or `keypaste_previous2`, and so on, if that's taken) when it starts, and tells you. It still works
under the new name; rename or delete it in the manager.

## Development

Requires the .NET SDK 10. Run from the repository root:

* Format check: `dotnet format keycuts.sln --verify-no-changes`
* Build (also the type-check): `dotnet build keycuts.sln`
* Test: `dotnet test keycuts.sln` (xUnit project `keycuts.Tests`; tests must use temp folders, never the real output folder or the registry)

Known baselines: the build has 27 CA1416 (Windows-only API) warnings (count with `--no-incremental`), so `-warnaserror`
isn't used. The format check is clean at error severity: `dotnet format keycuts.sln --verify-no-changes --severity error`
(without `--severity error` it also reports the 27 baseline CA1416 warnings and exits non-zero). Keep it clean.
