# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What keycuts is

keycuts creates **generated `.bat` files** that let you open anything on your computer by nickname. The `.bat` is written into a folder on the system `PATH`, so Windows Run (`Win+R`) resolves a bare name (`npp`, `gmail`, `db`). The generated script is the whole product: there is no database and no config file holding the shortcut list. The "list" is whatever `.bat` files are in the output folder (default `%USERPROFILE%\Shortcuts`, chosen in the installer, stored in `HKCU\Software\TeamRalon\keycuts`).

Entry points for creating one: the WPF app (drag/paste/browse a destination, name it, Enter), the Explorer right-click verb (launches the GUI with the destination as `args[0]`, handled in `keycuts.GUI/App.xaml.cs`), the CLI, or referencing `keycuts.Common`.

## Commands

.NET SDK 10, Windows only (Common, CLI and keypaste target `net10.0-windows`; GUI and Tests target `net10.0-windows10.0.19041.0`). `global.json` pins the SDK to the 10.0.2xx feature band, version 10.0.203 or a newer 2xx patch (`latestPatch`), and CI installs the newest 2xx patch. A machine without a 2xx SDK fails with an SDK-resolution error; install one, or move the band deliberately in `global.json`. Run from the repo root:

- Build: `dotnet build keycuts.sln`
- Test: `dotnet test keycuts.sln` (xUnit, project `keycuts.Tests`)
- Single test: `dotnet test keycuts.Tests --filter "FullyQualifiedName~KeycutCreatorTests.SomeTest"`
- On macOS/Linux: `dotnet build keycuts.sln -p:EnableWindowsTargeting=true` and `EnableWindowsTargeting=true dotnet format ...` work, but the tests can't run there (the test host needs the WPF runtime), so CI is the test run.
- Format check: `dotnet format keycuts.sln --verify-no-changes --severity error` (keep it clean; without `--severity error` it also reports baseline warnings and exits non-zero)
- Installer: `pwsh -File tools/Build-Installer.ps1 [-Version x.y.z]` (the version defaults to `Directory.Build.props`) publishes self-contained to `artifacts/publish` and builds `artifacts/installer/keycuts-setup.msi`. Needs only the .NET SDK; WiX restores from NuGet. `installer/` is deliberately not in `keycuts.sln`, so normal builds stay fast. WiX builds into `en-US/` because of `Package.en-us.wxl`; the script moves the output back up.

Gotchas:
- The build has **0 warnings**. Each project's `AssemblyInfo.cs` declares `SupportedOSPlatform("windows")` by hand; `Directory.Build.props` lets the SDK generate only the version attributes for projects that have one. A new project with its own `AssemblyInfo.cs` needs the same line or CA1416 comes back. Count with `--no-incremental`; an incremental build skips compilation and reports 0.
- If `keycuts.GUI.exe` is running, the build fails with MSB3021/MSB3027 (file locked). That is not a code error; stop the process (the user has approved force-closing it). Close any instance you launch.
- Package versions live only in `Directory.Packages.props` (Central Package Management); `PackageReference` items carry no `Version`. `nuget.config` restores from nuget.org only. There are no lockfiles, because the installer's `-r win-x64` publish would rewrite them.
- Tests must use temp folders and must never touch the real output folder or the registry. Use `KeycutCreator`/`KeycutRepository` with an explicit `OutputFolder`, an explicit `Overwrite`, and a fake `IPathUpdater`; a null `Overwrite` falls through to `Runner.GetForceOverwrite()`, which reads `HKCU`. Use `ContextMenuRegistrar` with a fake `IClassesStore`.
- A Debug `keycuts.exe` ends with `Console.ReadKey()`, so it waits for a keypress and won't exit on its own in a script.
- Debug builds honor the `KEYCUTS_OUTPUT_FOLDER` env var (`DebugOverrides`) to redirect the output folder without touching registry/PATH; it is inert in Release. `tools/New-SampleKeycuts.ps1` generates fixture folders (refuses the real output folder).
- CI (`.github/workflows/ci.yml`) runs on PRs into `develop`/`main` and pushes to `develop`. Job `build` (Windows) builds and tests, and job `format` checks formatting on Linux beside it; both are required checks on `develop` and `main`. Each run's summary shows the version, SDK and runner image, the test results (`GitHubActionsTestLogger`, which also annotates failed tests), the diff of any changed characterization snapshot, and, on failure, the formatting issues by line. A failed run uploads a `test-results` artifact (TRX plus any `.received.txt` snapshots). PRs into `main` also run job `installer` beside `build` (required on `main`): it builds the MSI, including WiX ICE validation, uploads it as `keycuts-setup`, and puts the release preview with its size and SHA256 in the summary. Actions are pinned to commit SHAs; Dependabot (`.github/dependabot.yml`) bumps them and the NuGet packages monthly.

## Versioning and releases

`<Version>` in `Directory.Build.props` is the only version. It stamps every exe (`Runner.Version`, which goes into each `.bat` header) and the MSI. Every push to `main` runs `.github/workflows/release.yml`: if tag `v<Version>` doesn't exist yet it tests, builds the MSI, and publishes a GitHub release with `keycuts-setup.msi`, `keycuts-portable-win-x64.zip` and `SHA256SUMS.txt`; otherwise it does nothing. To release, raise the version on `develop` (three parts; MSI upgrades ignore a fourth), then merge `develop` into `main`. Don't create release tags by hand.

## Architecture

Five SDK-style projects in `keycuts.sln` (the old `keycuts.Batmanager` app was retired; its manager is now a view inside the GUI):

- `keycuts.Common`: all domain logic, no UI. Resolves `.lnk` files through late-bound `WScript.Shell` COM (`Type.GetTypeFromProgID`), so there is no COM reference in the csproj.
- `keycuts.GUI`: WPF, single window with two views (Creator, Manager) driven by `ViewModels/` (`ShellViewModel` switches views). Side-effects go through `IKeycutActions` so view models are testable. `Themes/` holds tokens, light/dark/high-contrast dictionaries and `ThemeManager`; `Controls/` has `Icon`, `TypeBadge`, `MenuButton`, `SortHeaderButton`.
- `keycuts.CLI`: assembly is named **`keycuts.exe`**, not `keycuts.CLI.exe`. Uses CommandLineParser 2.9.1. Calls `Runner` directly and has no description option.
- `keypaste`: copies a keycut's destination to the clipboard (WinForms).
- `keycuts.Tests`: references Common and GUI; covers creation, repository, resolver, view models, accessibility, sample data.

### Two layers of creation code in `keycuts.Common`

The newer code wraps the legacy code rather than replacing it. Every create ends in the same place: `KeycutCreator` (GUI) validates, then calls `Runner.Run`, as the CLI does directly; `Runner.Run` builds a `Shortcut` and calls `CreateShortcutFile`. Reads also share one parser: `KeycutRepository.TryParse` wraps `ShortcutFile`. So a change to the emitter or parser affects every entry point.

- **Legacy**: `Runner.Run` / `Runner.CreateShortcutFile` (the `.bat` emitter, returns `ExitCode`), `Shortcut` (classifies a destination into `ShortcutType`, resolves `.lnk`/`.url`), `ShortcutFile` (regex parser that reads a `.bat` back), `RegistryStuff` (`HKCU` settings; reads never write).
- **Newer, structured**: `DestinationResolver` (validates and classifies a destination, returns `DestinationResult`/`DestinationError`), `KeycutCreator` (returns `CreateResult` with a `CreateOutcome`, async, takes `CreateOptions` including an injectable `IPathUpdater`), `KeycutRepository` (loads/deletes `.bat` files in one folder, background destination checks), `Keycut`/`KeycutKind` (the manager's model). Validation differs by caller: the GUI gets `DestinationResolver` and name checks first, the CLI doesn't. The legacy `GetShortcutTypeFromDestination` still runs on every create (the `Shortcut` constructor) and in `MainFormLogic`.

### The `.bat` format

`Runner.CreateShortcutFile` writes a header (`REM keycuts <version>`) plus `REM <shortcut>`, `<type>`, `<destination>`, and `<description>` comment lines, then the launch command and `EXIT`. `ShortcutFile` reads the description, type and destination from those comments. Type and destination are used only when both are present and valid (`TryParseRemTypeAndDestination`), and then they win even if the launch command disagrees; `Command` and `OpenWithApp` always come from the launch line. Otherwise (the 0.1.x format has no such lines) type and destination are recovered by regex-parsing the launch command (lines containing `explorer.exe` or starting with `START`), tried in order: the emitter's `cmd /C` command, CLSID, folder, hosts, the 0.1.x `START "" /D "<folder>" "<file>"` form (joined into one path), file, command, URL. No other pattern splices quoted parts together, so a File destination may hold a program plus its quoted arguments (`app.exe" "arg`), which re-emits the same line. A URL's launch line is escaped for cmd (`Shortcut.SanitizeBatEscapeCharacters`: every `%` doubled, `^` before every `^ & | < >` outside double quotes, where cmd reads a `^` literally); the parser reads a launch line as written and doesn't unescape it (see the gameplan's Decisions), and File/Folder paths aren't escaped at all. Files without a description line show an empty description. A corpus of real `.bat` files, including a second legacy header schema (`keycuts 0.1.1.5`), lives in `tests/corpus/legacy/` (see its README).

## Surprises and known landmines

- `ShortcutType.Command` is still never produced by a create: `Shortcut.GetShortcutTypeFromDestination` never returns it. The emitter writes `START "" /B cmd /C "<command>"` for it, which the regex fallback reads back as Command (`IsEmittedCommand`, an exact inverse tried before the unanchored Explorer patterns). A `START "" /B "<anything>"` line still reads as File, because File is tried before Command.
- The emitter's launch line (`Runner.BuildCommand`) is a `switch` with no discard arm, so adding a `ShortcutType` without a case is a build error (CS8509). `Unknown` throws.
- **PATH management** (`UserPathUpdater`, reached from `Runner.Run` and `Runner.ChangeOutputFolder` through `IPathUpdater`) edits the user `PATH` in `HKCU\Environment` directly, because `Environment.SetEnvironmentVariable` would expand `%VARS%` and rewrite the value as `REG_SZ`. It is a no-op when old and new folders match, so the installer is what first puts the output folder on `PATH`. GUI creates never change `PATH`: `KeycutCreator` uses a fixed-folder `Runner`, which passes the same folder as both. Old entries on the machine `PATH` can't be removed without elevation and are left alone.
- `Shortcut.GetShortcutTypeFromDestination` returns `Unknown` for a missing or unreadable path, which `Runner.Run` reports as `ExitCode.NotStarted`. `DestinationResolver` is still the validator that gives a reason.
- The regex fallback's CLSID pattern starts with `[shell:]?`, a character class, not the word `shell:`. So a launch line for a `shell:::{...}` destination (which `DestinationResolver` accepts) doesn't match it and is read back as a Folder. Only files without valid REM type and destination lines hit this. (File and Folder destinations used to keep their surrounding quotes and CLSID keys used to lose their `::`; `ShortcutFile.Unquote` removes exactly one pair of quotes, the inverse of the emitter's wrapping, so don't add another trim on top.)
- **Nothing needs elevation.** Context-menu verbs live in `HKCU\Software\Classes` (`ContextMenuRegistrar`, handler resolved from `AppContext.BaseDirectory`). Never write through `Registry.ClassesRoot`: it resolves to `HKLM` for these keys, and that write crashed the Settings window. The MSI writes the same keys in `installer/Package.wxs`, and a test compares the two lists.
- The Settings checkbox shows whether the verbs exist, not the `RightClickContextMenu` value. That value only records the user's choice, so an MSI upgrade doesn't re-add a menu they turned off.
- The first-run welcome panel (`WelcomeViewModel`, on `CreatorViewModel.Welcome`) decides once, from the shell's first keycut count: it shows when `WelcomeDismissed` is unset in `HKCU` (the first run, upgrades included) or the folder is empty, and Settings → Show Welcome calls `ShellViewModel.ShowWelcome`. The self-keycut's default name is `kc`, not `keycuts`, because the CLI `keycuts.exe` is on PATH too and one would shadow the other. It is wired up only in `App.xaml.cs`; tests pass fakes for the HKCU read/write and the create call, and a `CreatorViewModel` without one shows no panel. The self-keycut points at `Environment.ProcessPath`, so a Debug run creates one to the Debug exe. GUI creates never change PATH, so after the self-keycut the panel checks `UserPathUpdater.IsOnPath` (registry user and machine PATH, as Win+R sees them) and offers **Add to PATH** if needed; with the Debug `KEYCUTS_OUTPUT_FOLDER` override it skips that check, so it never writes PATH.
- Windows 11 shows these classic verbs under "Show more options". The top-level menu needs a packaged `IExplorerCommand` handler.
- `OUTPUTFOLDER` in `installer/Package.wxs` is a directory so the stock `InstallDirDlg` can edit it, which means Windows Installer stores it with a trailing backslash; `RegistryStuff.GetOutputFolder` trims that. The dialog only shows on a first install. Windows Installer has no standard directory for the profile, so the default comes from `[%USERPROFILE]`; a test compares it to `Runner.DefaultOutputFolder`.
- Manager rows take no keyboard focus. `ManagerView_PreviewKeyDown` handles Up, Down and Enter for the whole view, so typing in the search box keeps working, and `ManagerViewModel.SelectedRow` holds the selection.
- The `HostsFile` check has two comparisons. The literal `C:\Windows\...\hosts` one works; the unexpanded `%windir%` one can never match, because the path has already been through `Path.GetFullPath`.

## Conventions

`#region` blocks, `var` everywhere, `out` parameters as the legacy return idiom, `Console.WriteLine` as logging inside the library. `.editorconfig` enforces Allman braces, 4-space C# / 2-space XAML and csproj. `keycuts.GUI` has `Nullable` enabled; `Common` does not. Match the surrounding style.

## Where things are documented

- `README.md`: user-facing behavior of the Creator and Manager views.
- `docs/tickets/` (`README.md`, `HANDOFF.md`, `KC-*.md`): the UI redesign breakdown (KC-01 to KC-20) and its recorded decisions.
- `docs/design-reference/`: the React/Figma prototype used as the visual spec. Port look and behavior, not code; don't add React/WebView to the app.
- `gameplan.md`: phased roadmap (installer and context menu, backlog, then macOS/Avalonia). The target layout (`src/Keycuts.Core` plus per-platform projects behind interfaces like `IPathService`/`IFileSystem`) is future work, not current.

## Sequencing rule

The parser has thin test coverage and the plan changes both the file format and the platform underneath it. **Never move two variables in one commit.** Current behavior (bugs included) is pinned by `keycuts.Tests/Characterization/`. `CharacterizationRenderer` renders the parse of every corpus file, the emitter output for each type, and a `Parse(Emit(Parse(f)))` round trip as text, twice: once reading the whole emitted file (`RoundTrip`), and once without its REM `<type>` and `<destination>` lines (`LaunchLineRoundTrip`, the one that shows whether the emitted command is right); `CharacterizationTests` compares each rendering with `Snapshots/*.verified.txt`. When one differs, the test writes a `.received.txt` next to it (gitignored; in CI, download it from the failed run's `test-results` artifact). If the change is intended, copy it over the `.verified.txt` in the same commit, so the behavior change is a reviewable diff. Known wrong answers are annotated in `ParserNotes` and the emitter cases. Pure entry points for this: `Runner.BuildShortcutLines`, `new ShortcutFile(path, lines)` and `Shortcut.FromParts`.
