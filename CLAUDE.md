# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working in this repository.

## What keycuts is

keycuts creates **generated `.bat` files** that let you open anything on your computer by nickname. The `.bat` is written into a folder on the system `PATH`, so Windows Run (`Win+R`) resolves a bare name (`npp`, `gmail`, `db`). The generated script is the whole product: there is no database and no config file holding the shortcut list. The "list" is whatever `.bat` files are in the output folder (default `C:\Shortcuts`, stored in `HKCU`).

Entry points for creating one: the WPF app (drag/paste/browse a destination, name it, Enter), the Explorer right-click verb (launches the GUI with the destination as `args[0]`, handled in `keycuts.GUI/App.xaml.cs`), the CLI, or referencing `keycuts.Common`.

## Commands

.NET SDK 10, Windows only (all projects target `net10.0-windows`; WPF and `Microsoft.Win32` registry). Run from the repo root:

- Build: `dotnet build keycuts.sln`
- Test: `dotnet test keycuts.sln` (xUnit, project `keycuts.Tests`)
- Single test: `dotnet test keycuts.Tests --filter "FullyQualifiedName~KeycutCreatorTests.SomeTest"`
- Format check: `dotnet format keycuts.sln --verify-no-changes --severity error` (keep it clean; without `--severity error` it also reports baseline warnings and exits non-zero)

Gotchas:
- The build has **27 baseline CA1416** (Windows-only API) warnings, so `-warnaserror` isn't used. Don't add new ones. Count with `--no-incremental`; an incremental build skips compilation and reports 0.
- If `keycuts.GUI.exe` is running, the build fails with MSB3021/MSB3027 (file locked). That is not a code error; stop the process (the user has approved force-closing it). Close any instance you launch.
- Tests must use temp folders and must never touch the real output folder or the registry. Use `KeycutCreator`/`KeycutRepository` with an explicit `OutputFolder` and a fake `IPathUpdater`.
- Debug builds honor the `KEYCUTS_OUTPUT_FOLDER` env var (`DebugOverrides`) to redirect the output folder without touching registry/PATH; it is inert in Release. `tools/New-SampleKeycuts.ps1` generates fixture folders (refuses the real output folder).
- No CI exists yet.

## Architecture

Five SDK-style projects in `keycuts.sln` (the old `keycuts.Batmanager` app was retired; its manager is now a view inside the GUI):

- `keycuts.Common`: all domain logic, no UI. Has a Shell32 COM reference (for `.lnk` resolution).
- `keycuts.GUI`: WPF, single window with two views (Creator, Manager) driven by `ViewModels/` (`ShellViewModel` switches views). Side-effects go through `IKeycutActions` so view models are testable. `Themes/` holds tokens, light/dark/high-contrast dictionaries and `ThemeManager`; `Controls/` has `Icon`, `TypeBadge`, `MenuButton`, `SortHeaderButton`.
- `keycuts.CLI`: assembly is named **`keycuts.exe`**, not `keycuts.CLI.exe`. Uses CommandLineParser 2.9.1.
- `keypaste`: copies a keycut's destination to the clipboard (WinForms).
- `keycuts.Tests`: references Common and GUI; covers creation, repository, resolver, view models, accessibility, sample data.

### Two generations of creation code in `keycuts.Common`

- **Legacy**: `Runner.Run` / `Runner.CreateShortcutFile` (the `.bat` emitter, returns `ExitCode`), `Shortcut` (classifies a destination into `ShortcutType`, resolves `.lnk`/`.url`), `ShortcutFile` (regex parser that reads a `.bat` back), `PathSetup`, `RegistryStuff` (`HKCU` settings plus `HKCR` context-menu verbs).
- **Newer, structured**: `DestinationResolver` (validates and classifies a destination, returns `DestinationResult`/`DestinationError`), `KeycutCreator` (returns `CreateResult` with a `CreateOutcome`, async, takes `CreateOptions` including an injectable `IPathUpdater`), `KeycutRepository` (loads/deletes `.bat` files in one folder, background destination checks), `Keycut`/`KeycutKind` (the manager's model). The GUI uses the newer path; check which one a caller uses before assuming behavior. The legacy `GetShortcutTypeFromDestination` is still called by `MainFormLogic`.

### The `.bat` format

`Runner.CreateShortcutFile` writes a header (`REM keycuts <version>`) plus `REM <shortcut>`, `<type>`, `<destination>`, and `<description>` comment lines, then the launch command and `EXIT`. The optional description is the only metadata the parser reads from those comments (regex in `ShortcutFile`); type and destination are still recovered by regex-parsing the launch command (lines containing `explorer.exe` or starting with `START`). Files without a description line show an empty description. A corpus of real `.bat` files, including a second legacy header schema (`keycuts 0.1.1.5`), lives in `tests/corpus/legacy/` (see its README).

## Surprises and known landmines

- `ShortcutType.Command` is parse-only: `Shortcut.GetShortcutTypeFromDestination` never returns it and the emitter has no branch for it, so a `Command` shortcut would emit an empty line then `EXIT`.
- **PATH management** (`PathSetup`, called from `Runner.Run`, Machine scope, needs elevation) is almost always a no-op, because old and new paths come from the same registry value. Nothing proactively puts the output folder on `PATH` (the old installer did, and was deleted). In the rare `-o` case, `AddToSystemPath` returns `false` if the folder is already present, which surfaces as `ExitCode.CannotUpdatePath` and aborts creation. `ExistsInSystemPath` uses a substring `Contains`, so `C:\Shortcuts` matches `C:\Shortcuts2`. Read `gameplan.md` Phase 3.1 notes before touching `PathSetup.cs`.
- `Shortcut.GetShortcutTypeFromDestination` calls `File.GetAttributes` before checking existence, so a missing path throws (`DestinationResolver` is the safe alternative).
- `RegistryStuff.CreateSubKey` is get-or-create and is used on read paths, so reads have write side effects.
- Context-menu registration writes `Process.GetCurrentProcess().MainModule.FileName` as the handler, so whichever executable toggles the setting becomes the shell handler.
- `SettingsWindow.RightClickContextMenus` raises `NotifyPropertyChanged("RightClickContextMenu")`; the name doesn't match the property, so that binding never updates.
- The `HostsFile` check compares against an unexpanded `%windir%` path, so that branch is dead.
- `Runner.OpenBatmanager` may still exist in `Common`; the Batmanager exe it launches is gone.

## Conventions

`#region` blocks, `var` everywhere, `out` parameters as the legacy return idiom, `Console.WriteLine` as logging inside the library. `.editorconfig` enforces Allman braces, 4-space C# / 2-space XAML and csproj. `keycuts.GUI` has `Nullable` enabled; `Common` does not. Match the surrounding style.

## Where things are documented

- `README.md`: user-facing behavior of the Creator and Manager views.
- `docs/tickets/` (`README.md`, `HANDOFF.md`, `KC-*.md`): the UI redesign breakdown (KC-01 to KC-20) and its recorded decisions.
- `docs/design-reference/`: the React/Figma prototype used as the visual spec. Port look and behavior, not code; don't add React/WebView to the app.
- `gameplan.md`: phased roadmap (installer and context menu, backlog, then macOS/Avalonia). The target layout (`src/Keycuts.Core` plus per-platform projects behind interfaces like `IPathService`/`IFileSystem`) is future work, not current.

## Sequencing rule

The parser has thin test coverage and the plan changes both the file format and the platform underneath it. **Never move two variables in one commit.** Pin current behavior (including bugs) with characterization tests against `tests/corpus/legacy/` before changing emitter or parser logic.
