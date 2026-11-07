# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What keycuts is

keycuts creates **generated `.bat` files** that let you open anything on your computer by nickname. The `.bat` is written into a folder on the system `PATH`, so Windows Run (`Win+R`) resolves a bare name (`npp`, `gmail`, `db`). The generated script is the whole product: there is no database and no config file holding the shortcut list. The "list" is whatever `.bat` files are in the output folder (default `C:\Shortcuts`, stored in `HKCU\Software\TeamRalon\keycuts`).

Entry points for creating one: the WPF app (drag/paste/browse a destination, name it, Enter), the Explorer right-click verb (launches the GUI with the destination as `args[0]`, handled in `keycuts.GUI/App.xaml.cs`), the CLI, or referencing `keycuts.Common`.

## Commands

.NET SDK 10, Windows only (Common, CLI and keypaste target `net10.0-windows`; GUI and Tests target `net10.0-windows10.0.19041.0`). Run from the repo root:

- Build: `dotnet build keycuts.sln`
- Test: `dotnet test keycuts.sln` (xUnit, project `keycuts.Tests`)
- Single test: `dotnet test keycuts.Tests --filter "FullyQualifiedName~KeycutCreatorTests.SomeTest"`
- Format check: `dotnet format keycuts.sln --verify-no-changes --severity error` (keep it clean; without `--severity error` it also reports baseline warnings and exits non-zero)

Gotchas:
- The build has **27 baseline CA1416** (Windows-only API) warnings, so `-warnaserror` isn't used. Don't add new ones. Count with `--no-incremental`; an incremental build skips compilation and reports 0.
- If `keycuts.GUI.exe` is running, the build fails with MSB3021/MSB3027 (file locked). That is not a code error; stop the process (the user has approved force-closing it). Close any instance you launch.
- Tests must use temp folders and must never touch the real output folder or the registry. Use `KeycutCreator`/`KeycutRepository` with an explicit `OutputFolder`, an explicit `Overwrite`, and a fake `IPathUpdater`; a null `Overwrite` falls through to `Runner.GetForceOverwrite()`, which reads (and can create) the `HKCU` key.
- A Debug `keycuts.exe` ends with `Console.ReadKey()`, so it waits for a keypress and won't exit on its own in a script.
- Debug builds honor the `KEYCUTS_OUTPUT_FOLDER` env var (`DebugOverrides`) to redirect the output folder without touching registry/PATH; it is inert in Release. `tools/New-SampleKeycuts.ps1` generates fixture folders (refuses the real output folder).
- No CI exists yet.

## Architecture

Five SDK-style projects in `keycuts.sln` (the old `keycuts.Batmanager` app was retired; its manager is now a view inside the GUI):

- `keycuts.Common`: all domain logic, no UI. Resolves `.lnk` files through late-bound `WScript.Shell` COM (`Type.GetTypeFromProgID`), so there is no COM reference in the csproj.
- `keycuts.GUI`: WPF, single window with two views (Creator, Manager) driven by `ViewModels/` (`ShellViewModel` switches views). Side-effects go through `IKeycutActions` so view models are testable. `Themes/` holds tokens, light/dark/high-contrast dictionaries and `ThemeManager`; `Controls/` has `Icon`, `TypeBadge`, `MenuButton`, `SortHeaderButton`.
- `keycuts.CLI`: assembly is named **`keycuts.exe`**, not `keycuts.CLI.exe`. Uses CommandLineParser 2.9.1. Calls `Runner` directly and has no description option.
- `keypaste`: copies a keycut's destination to the clipboard (WinForms).
- `keycuts.Tests`: references Common and GUI; covers creation, repository, resolver, view models, accessibility, sample data.

### Two layers of creation code in `keycuts.Common`

The newer code wraps the legacy code rather than replacing it. Every create ends in the same place: `KeycutCreator` (GUI) validates, then calls `Runner.Run`, as the CLI does directly; `Runner.Run` builds a `Shortcut` and calls `CreateShortcutFile`. Reads also share one parser: `KeycutRepository.TryParse` wraps `ShortcutFile`. So a change to the emitter or parser affects every entry point.

- **Legacy**: `Runner.Run` / `Runner.CreateShortcutFile` (the `.bat` emitter, returns `ExitCode`), `Shortcut` (classifies a destination into `ShortcutType`, resolves `.lnk`/`.url`), `ShortcutFile` (regex parser that reads a `.bat` back), `PathSetup`, `RegistryStuff` (`HKCU` settings plus `HKCR` context-menu verbs).
- **Newer, structured**: `DestinationResolver` (validates and classifies a destination, returns `DestinationResult`/`DestinationError`), `KeycutCreator` (returns `CreateResult` with a `CreateOutcome`, async, takes `CreateOptions` including an injectable `IPathUpdater`), `KeycutRepository` (loads/deletes `.bat` files in one folder, background destination checks), `Keycut`/`KeycutKind` (the manager's model). Validation differs by caller: the GUI gets `DestinationResolver` and name checks first, the CLI doesn't. The legacy `GetShortcutTypeFromDestination` still runs on every create (the `Shortcut` constructor) and in `MainFormLogic`.

### The `.bat` format

`Runner.CreateShortcutFile` writes a header (`REM keycuts <version>`) plus `REM <shortcut>`, `<type>`, `<destination>`, and `<description>` comment lines, then the launch command and `EXIT`. The optional description is the only metadata the parser reads from those comments (regex in `ShortcutFile`); type and destination are still recovered by regex-parsing the launch command (lines containing `explorer.exe` or starting with `START`), tried in order: CLSID, folder, hosts, file, command, URL. Files without a description line show an empty description. A corpus of real `.bat` files, including a second legacy header schema (`keycuts 0.1.1.5`), lives in `tests/corpus/legacy/` (see its README).

## Surprises and known landmines

- `ShortcutType.Command` is parse-only: `Shortcut.GetShortcutTypeFromDestination` never returns it and the emitter has no branch for it, so a `Command` shortcut would emit an empty line then `EXIT`.
- **PATH management** (`PathSetup`, reached from `Runner.Run` through `IPathUpdater`/`SystemPathUpdater`, Machine scope, needs elevation) is almost always a no-op, because old and new paths are usually equal. GUI creates never change `PATH`: `KeycutCreator` uses a fixed-folder `Runner`, which passes the same folder as both. Nothing proactively puts the output folder on `PATH` (the old installer did, and was deleted). In the rare `-o` case, `AddToSystemPath` returns `false` if the folder is already present, which surfaces as `ExitCode.CannotUpdatePath` and aborts creation. `ExistsInSystemPath` uses a substring `Contains`, so `C:\Shortcuts` matches `C:\Shortcuts2`. Read `gameplan.md` Phase 3.1 notes before touching `PathSetup.cs`.
- `Shortcut.GetShortcutTypeFromDestination` calls `File.GetAttributes` before checking existence, so a missing path throws (`DestinationResolver` is the safe alternative).
- `RegistryStuff.CreateSubKey` is get-or-create and is used on read paths, so reads have write side effects.
- Context-menu registration writes `Process.GetCurrentProcess().MainModule.FileName` as the handler, so whichever executable toggles the setting becomes the shell handler. `Runner.SetRightClickContextMenu` also reads the current value with `DefaultForceOverwrite` as its default instead of `DefaultRightClickContextMenu`.
- `SettingsWindow.RightClickContextMenus` raises `NotifyPropertyChanged("RightClickContextMenu")`; the name doesn't match the property, so that binding never updates.
- The `HostsFile` check has two comparisons. The literal `C:\Windows\...\hosts` one works; the unexpanded `%windir%` one can never match, because the path has already been through `Path.GetFullPath`.

## Conventions

`#region` blocks, `var` everywhere, `out` parameters as the legacy return idiom, `Console.WriteLine` as logging inside the library. `.editorconfig` enforces Allman braces, 4-space C# / 2-space XAML and csproj. `keycuts.GUI` has `Nullable` enabled; `Common` does not. Match the surrounding style.

## Where things are documented

- `README.md`: user-facing behavior of the Creator and Manager views.
- `docs/tickets/` (`README.md`, `HANDOFF.md`, `KC-*.md`): the UI redesign breakdown (KC-01 to KC-20) and its recorded decisions.
- `docs/design-reference/`: the React/Figma prototype used as the visual spec. Port look and behavior, not code; don't add React/WebView to the app.
- `gameplan.md`: phased roadmap (installer and context menu, backlog, then macOS/Avalonia). The target layout (`src/Keycuts.Core` plus per-platform projects behind interfaces like `IPathService`/`IFileSystem`) is future work, not current.

## Sequencing rule

The parser has thin test coverage and the plan changes both the file format and the platform underneath it. **Never move two variables in one commit.** Pin current behavior (including bugs) with characterization tests against `tests/corpus/legacy/` before changing emitter or parser logic. No test reads the corpus yet, so that harness has to be written first.
