# keycuts

Open any file, folder or URL by typing a nickname: in Win+R on Windows, in keycuts' Run box on a Mac.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/walkthrough-dark.gif">
  <img src="docs/images/walkthrough-light.gif" alt="Creating a keycut: a Gmail link is the target, the name gmail is typed, Enter creates it. Then Win+R, gmail, Enter opens it." width="580">
</picture>

Each keycut is a small script in a folder on your `PATH` (a `.bat` file on Windows, a shell script on macOS): `npp` opens Notepad++, `gmail` opens your email, `db` opens Dropbox.

## Install

Grab the MSI from the [latest release](https://github.com/andrewralon/keycuts/releases/latest) (`x64`, or `arm64` for Windows on ARM). No admin rights needed; .NET is included. Uninstalling keeps your keycuts.

Silent: `msiexec /i keycuts-setup-x64.msi /qn OUTPUTFOLDER="C:\Shortcuts"`

**On macOS** (13 or later): `brew install --cask andrewralon/tap/keycuts`, or the `.pkg` from the [latest release](https://github.com/andrewralon/keycuts/releases/latest) (`arm64` for Apple Silicon, `x64` for Intel). It puts `keycuts.app` in Applications and makes `~/Shortcuts` the first time it starts. Finder's right-click item **keycut this!** starts out switched off: turn it on in System Settings → General → Login Items & Extensions → Extensions → Finder.

## Uninstall

**Windows:** remove keycuts in Settings → Apps. Your keycuts stay.

**On macOS**, take out what keycuts added, then the app:

1. Turn off **Open at login** and **Run keycuts in Terminal** in Settings (⌘,), or in Terminal run `keycuts path remove`. That takes the marked block out of your shell profile and leaves the rest of the file as it was.
2. Run `keycuts uninstall-shell-integration` to take **keycut this!** out of Finder.
3. Quit keycuts from the menu bar, then `brew uninstall --cask keycuts`. Without Homebrew, drag `keycuts.app` from Applications to the Trash and run `sudo pkgutil --forget com.teamralon.keycuts`.
4. To also remove the settings and run counts, delete `~/Library/Application Support/keycuts` (`brew uninstall --zap --cask keycuts` does this too).

Your keycuts in `~/Shortcuts` stay. The `keycuts` and `keypaste` commands in that folder only work with the app installed, so delete those two files if you like.

## Use

**Create:** drop, paste or browse to a target, type a name, press Enter. Or right-click anything → **keycut this!**

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/creator-dark.png">
  <img src="docs/images/creator-light.png" alt="The creator." width="580">
</picture>

**Manage:** add keycuts from the row at the top, search, sort, and right-click a keycut to open, rename, edit or delete it (with undo). Missing targets are flagged. Each keycut counts its runs, so sorting by **Runs** or **Last run** shows which ones you never use.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/manager-dark.png">
  <img src="docs/images/manager-light.png" alt="The manager: a row for a new keycut over a searchable table of keycuts." width="830">
</picture>

**Settings:** output folder, the Run box shortcut, open at login, theme, icon (with or without its background; Windows), right-click menu, update checks, and environment-variable paths (`%USERPROFILE%\...`) for a portable keycuts folder.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/settings-dark.png">
  <img src="docs/images/settings-light.png" alt="Settings, in two columns." width="830">
</picture>

**On macOS** the same window has the Mac's own buttons, ⌘ keys, `$HOME` paths, and a switch that puts the keycuts folder on your Terminal PATH:

<details>
<summary>The creator, the manager and Settings on macOS</summary>

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/creator-dark-mac.png">
  <img src="docs/images/creator-light-mac.png" alt="The creator on macOS." width="580">
</picture>

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/manager-dark-mac.png">
  <img src="docs/images/manager-light-mac.png" alt="The manager on macOS." width="830">
</picture>

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/settings-dark-mac.png">
  <img src="docs/images/settings-light-mac.png" alt="Settings on macOS, with the Run box shortcut, Open at login and Run keycuts in Terminal." width="830">
</picture>

</details>

The app checks GitHub for updates at most once a day. No telemetry: run counts are files in `%LOCALAPPDATA%\keycuts\usage` that never leave your PC.

**Keycuts from older versions:** the first time the app opens a keycuts folder, it brings keycuts written by older versions to the current format. It renames their `<destination>` line to `<target>`, and folder keycuts get `START "" /B` in front of their Explorer line, so their console window closes sooner. Nothing else in the files changes, and their date modified stays the same. The originals are copied to a hidden `.keycuts-backup\<date-time>` folder inside the keycuts folder first, and a message says where.

## How you run a keycut on macOS

Spotlight and the Finder don't run scripts by name, so keycuts has two launchers of its own, and on macOS 26 a third through Spotlight's quick keys:

- **The Run box.** keycuts stays in the menu bar (closing its window only hides it; **Open at login** in Settings has it waiting after a restart). Press **⇧⌘Space** from any app (change it under **Run Box** in Settings), type the name, press Return. The box opens on the keycuts you ran most recently (Return alone runs the last one again) and suggests matches as you type, Tab completes one, Esc closes it, and a name you haven't made offers **Create…**. When the keycut has run, focus goes back to the app you were in.
- **Terminal.** Turn on **Run keycuts in Terminal** in Settings and the keycuts folder goes on your `PATH`, through a marked block at the end of `~/.zprofile` (zsh) or `~/.bash_profile` (bash); then `gmail` works in any new Terminal window. For fish or nushell, Settings and `keycuts path` show the line to add yourself. Turning the switch off takes the block out again and leaves the rest of the file as it was.
- **Spotlight, with a quick key (macOS 26 or later).** Spotlight won't run a script when you type its name, but a quick key can: in Settings → **Spotlight**, press **add to Shortcuts** and confirm in Shortcuts (it adds a shortcut named **Run keycut**). Then open Spotlight, find **Run keycut**, press **Add quick keys** on it and type `k` (macOS keeps quick keys itself, so keycuts can't add this one). From then on, `k gmail` in Spotlight runs the keycut, with no window; a name that isn't a keycut shows a notification with the error. It runs the same files as the Run box, from the output folder in Settings.

## Windows and macOS

| | Windows | macOS |
|---|---|---|
| Install | MSI (`x64`, `arm64`) | `.pkg` (`arm64`, `x64`), or `brew install --cask andrewralon/tap/keycuts` |
| Run a keycut | Win+R; the Run box (Ctrl+Alt+Space) | The Run box (⇧⌘Space); Terminal |
| A keycut is | a `.bat` file | a shell script with no extension (mode 755) |
| Keycuts folder | `%USERPROFILE%\Shortcuts` | `~/Shortcuts` |
| On `PATH` | the installer adds the folder to your user `PATH` | opt in: **Run keycuts in Terminal** in Settings |
| Create from a drop, paste or Browse; the manager, search, rename, edit, run counts | yes | yes |
| Right-click **keycut this!** | files, folders and a folder's background, in File Explorer | files and folders, as a Finder Quick Action (switched off until you turn it on in System Settings) |
| Targets: files, folders, URLs, apps | yes | yes (apps are `.app` bundles; a `.webloc` file is its URL) |
| `.lnk` and `.url` files as targets | yes | no |
| Control Panel and other CLSID items, the hosts file | yes | no |
| Paths that move between accounts | `%USERPROFILE%`-style variables | `$HOME` |
| Delete with undo | Recycle Bin | Trash |
| Open at login | yes (the tray) | yes (the menu bar) |
| Themes, icons, update check | yes | yes |
| Command line | `keycuts`, `keypaste` | the same, plus `keycuts path` |
| Spotlight | not applicable | a quick key, `k gmail` (macOS 26 or later; see above) |

**Known gaps on macOS:** no Spotlight launch by plain name (the quick key above is the way, and needs macOS 26); no right-click on a folder's background; no CLSID or hosts-file keycuts; no `.lnk` handling; a Finder alias can't be a target (use the original item).

## Command line

```
keycuts -t <target> -s <name> [--description "text"] [-f]
keypaste <name>      # copy a keycut's target to the clipboard
keycuts list [<search>] [--usage] [--sort name|runs|last-run] [--json]   # list keycuts, with run counts
keycuts rename <old> <new> [-f]   # rename a keycut, as the manager does
keycuts rm <name>                 # delete a keycut, to the Recycle Bin
keycuts open <name>               # open a keycut, as Win+R does
keycuts              # open the app
```

On macOS there are two more: `keycuts path [status|add|remove]` (the Terminal `PATH` block above) and `keycuts install-shell-integration` / `uninstall-shell-integration` (Finder's **keycut this!**). The Windows ones add or remove the File Explorer menus.

## Development

.NET SDK 10:

```
dotnet build keycuts.sln
dotnet test keycuts.sln
pwsh -File tools/Build-Installer.ps1
```

To release: bump `<Version>` in `Directory.Build.props`, run `tools/New-Screenshots.ps1` (the screenshots and walkthrough GIFs in `docs/images`) and commit what changed, run `tools/mac/sign-spotlight-shortcut.sh --refresh` on a Mac signed into iCloud (it re-signs the Spotlight shortcut, whose certificate lasts about 13 months) and commit its two files, merge `develop` into `main`.
