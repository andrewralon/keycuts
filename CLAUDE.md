# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What keycuts is

keycuts creates **generated script files** that let you open anything on your computer by nickname.

On Windows, it writes a `.bat` file into a folder that is on the system `PATH`, so Windows Run (`Win+R`) resolves a bare name: `npp`, `rb`, `gmail`, `db`. The generated script is the entire product — there is no database and no config file holding the shortcut list.

Four ways to create one: the WPF GUI (drag a file in, name it), the Windows right-click context menu (opens the GUI with the destination pre-filled), the CLI, or by referencing `keycuts.Common` directly.

## ⚠️ Current state: the repo does not build

Do not assume a clean build. The only NuGet dependency is `CommandLineParser20` version `2.0.0.0`, an unofficial/repackaged build that **is not on nuget.org**. `packages/` is gitignored and absent, so restore fails. Its API does not match modern CommandLineParser 2.9 — it uses `DefaultValue =` on `[Option]` and reads `parsedArgs.Value` / `parsedArgs.Errors` directly.

`keycuts.Common` carries a reference to that package and a `packages.config` entry but contains **zero** `using CommandLine` statements. Removing it there is safe and unblocks the library.

There are no tests, no CI, no installer, no `.editorconfig`, and no `Directory.Build.props`.

## Current layout

All five projects are non-SDK-style, `TargetFrameworkVersion v4.6.1`, `AnyCPU`, VS2017 solution format.

| Project | Output | Notes |
|---|---|---|
| `keycuts.Common` | Library | All domain logic. Has a `COMReference` to Shell32. |
| `keycuts.CLI` | Exe | Assembly is named **`keycuts.exe`**, not `keycuts.CLI.exe`. |
| `keycuts.GUI` | WinExe (WPF) | `App.xaml.cs` takes `e.Args[0]` as the destination — this is the context-menu entry point. |
| `keycuts.Batmanager` | WinExe (WPF) | DataGrid listing existing `.bat` files. |
| `keypaste` | Exe | Copies a keycut's destination to the clipboard via `System.Windows.Forms`. |

### The core files, in the order they matter

- `keycuts.Common/Runner.cs` — the **emitter**. `CreateShortcutFile` builds the `.bat` line list and writes it.
- `keycuts.Common/ShortcutFile.cs` — the **parser**. Regex-based; reads a `.bat` back into a model.
- `keycuts.Common/Shortcut.cs` — classifies a destination into `ShortcutType`; resolves `.lnk` and `.url`.
- `keycuts.Common/RegistryStuff.cs` — `HKCU` settings plus `HKCR` context-menu verbs.
- `keycuts.Common/PathSetup.cs` — machine-level `PATH` management. Coupled into every shortcut creation, but rarely actually touches `PATH` in practice — see below.

## Three things that will surprise you

Read these before touching the emitter or parser.

**1. The metadata sidecar already exists, and the parser ignores it.**
`Runner.CreateShortcutFile` writes `REM <shortcut>…</shortcut>`, `REM <type>…</type>`, and `REM <destination>…</destination>` into every generated file. `ShortcutFile`'s constructor filters lines down to those containing `explorer.exe` or starting with `START`, then regex-parses the *command* to recover the type and destination. The structured metadata is written on every create and read on none.

**2. `ShortcutType.Command` is parse-only.**
`Shortcut.GetShortcutTypeFromDestination` never returns `Command`, and `Runner.CreateShortcutFile`'s `if/else` ladder has no branch for it. A `Command` shortcut emits an **empty line** followed by `EXIT`. Only `ShortcutFile` produces the value, on read. The emitter and the parser have asymmetric type domains.

**3. `PATH` is coupled to shortcut creation, but almost never actually touches `PATH`.**
`Runner.Run` calls `PathSetup.AddToOrReplaceInSystemPath(oldPath, newPath)` on every creation, at `EnvironmentVariableTarget.Machine` scope (requiring elevation) — but that call is a no-op unless `oldPath != newPath`, and `oldPath`/`newPath` are both derived from the same registry-stored output folder. In the ordinary flow (no `-o` flag, Settings already synced), `Environment.SetEnvironmentVariable` is never reached. The real problem is the opposite of what it looks like: **the app essentially never proactively puts its own output folder on `PATH`.** The deleted installer (`cdf5115`, "Remove terrible installer") almost certainly did that job, and nothing replaced it. The narrow case where `AddToSystemPath` *is* reached (an explicit `-o` pointing somewhere new) does have a real bug — it returns `false` when the folder is already present, which propagates to `ExitCode.CannotUpdatePath` and aborts the creation — but this is a rare trigger, not a per-create one. Separately, `ExistsInSystemPath` does a substring `Contains` over the whole `PATH` string, so `C:\Shortcuts` matches `C:\Shortcuts2`.

## Other known landmines

- `Shortcut.GetShortcutTypeFromDestination` calls `File.GetAttributes(destination)` **before** checking existence, so a missing or permission-denied path throws instead of yielding `ShortcutType.Unknown`.
- The `HostsFile` check compares against `%windir%\System32\drivers\etc\hosts` *after* `Path.GetFullPath`, which does not expand environment variables. That branch is dead.
- `RegistryStuff.CreateSubKey` is get-or-create and is used on read paths, so **reads have write side effects**.
- Context-menu registration writes `Process.GetCurrentProcess().MainModule.FileName` as the handler, so whichever executable toggles the setting becomes the shell handler.
- `Runner.OpenBatmanager` locates its sibling exe by guessing paths, with a `#if DEBUG` `..\..\..` walk.
- `SettingsWindow.RightClickContextMenus` raises `NotifyPropertyChanged("RightClickContextMenu")` — the name does not match the property, so the binding never updates.

## Conventions in the existing code

`#region` blocks throughout; `var` everywhere; `out` parameters as the primary return idiom (`IsLink`, `MatchesRegex`, `IsValidUrl`, `GetShortcutTypeFromDestination`); `Console.WriteLine` used as logging *inside the library*. No `async` anywhere. No nullable annotations. Match the surrounding style when editing existing files.

## Where this is going

A modernization backlog is tracked in GitHub issues under milestones `M1 Foundation` through `M6 Polish`: .NET 10, SDK-style projects, an Avalonia UI replacing both WPF apps, macOS support, a WiX v6 MSI, and a signed macOS `.pkg`.

The target layout introduces `src/Keycuts.Core` plus `Keycuts.Platform.Windows` / `Keycuts.Platform.MacOS` behind a set of interfaces.

**The rule that makes it work:** `Keycuts.Core` must never call `Path.GetFullPath`, `File.*`, `Process.Start`, `Environment.SetEnvironmentVariable`, or `Microsoft.Win32.*` directly. Everything goes through injected abstractions (`IPathService`, `IFileSystem`, `IDestinationLauncher`, …). This is what lets Windows-specific logic be unit-tested from a Mac, which matters because development happens on macOS.

### Sequencing rule

There is a regex parser that has never had a test, and the plan changes both the file format and the platform underneath it. **Never move two variables in one commit.** A corpus of real `.bat` files already exists at `tests/corpus/legacy/` (see its own `README.md` — it includes a previously unknown second header schema found in the wild, `keycuts 0.1.1.5`, in addition to the current one). Pin current behavior with characterization tests against it — including the bugs — before changing any emitter or parser logic.

See `gameplan.md` at the repo root for the current phase-by-phase execution order and the reasoning behind it, including corrections made after reading the relevant code directly (in particular: the `PATH` behavior described above was originally mischaracterized as "rewritten on every create" — read the gameplan's Phase 3.1 notes before touching `PathSetup.cs`).
