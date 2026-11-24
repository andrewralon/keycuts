# CLAUDE.md

## What keycuts is

keycuts writes **generated `.bat` files** into a folder on `PATH` so Win+R opens anything by nickname (`npp`, `gmail`). The `.bat` is the product: no database, no list file. The output folder (default `%USERPROFILE%\Shortcuts`) is stored in `HKCU\Software\TeamRalon\keycuts`. Entry points: the app (Avalonia), the Explorer right-click verb (passes the target as `args[0]`, `keycuts.App/App.axaml.cs`), the CLI, or `keycuts.Common`.

**On macOS** the product is an extensionless `#!/bin/sh` script (mode 755, LF) in `~/Shortcuts`, with the same header lines as the `.bat` behind `# ` (see "macOS keycuts" below); you run it from the app's Run box (a global hotkey, ⇧⌘Space) or by name in Terminal (the folder on `PATH` through a marked block in `~/.zprofile`). Settings are JSON in `~/Library/Application Support/keycuts/settings.json`. Windows and macOS are one codebase: `ScriptFormats.Current` (`BatFormat` or `ShFormat`), `ISettingsBackend`, `IDesktop`.

## Commands

.NET SDK 10, Windows only. `global.json` pins the 10.0.4xx band (`latestPatch`). Run from the repo root:

- Build: `dotnet build keycuts.sln`
- Test: `dotnet test keycuts.sln` (xUnit v3 on VSTest: `keycuts.Common.Tests` and `keycuts.App.Tests`); one test: `dotnet test keycuts.Common.Tests --filter "FullyQualifiedName~KeycutCreatorTests.SomeTest"`
- Format: `dotnet format keycuts.sln --verify-no-changes --severity error` (keep `--severity error`)
- Installer: `pwsh -File tools/Build-Installer.ps1 [-Version x.y.z] [-Runtime win-x64|win-arm64]` → `artifacts/installer/<runtime>/`. `installer/` is not in the sln. Don't install a local MSI over a release whose bundled .NET runtime is newer than your SDK's: the upgrade won't downgrade the runtime's files but the old product's removal deletes them, so `keycuts.exe` can't start (`hostpolicy.dll` missing). Uninstall first or `msiexec /fa`, or publish with the release's runtime (`KnownFrameworkReference` `LatestRuntimeFrameworkVersion`) to test a real upgrade.
- Screenshots and GIFs: `pwsh -File tools/New-Screenshots.ps1` rewrites `docs/images` (the README's PNGs and its `walkthrough-*.gif`) via `ScreenshotTests` (inert unless `KEYCUTS_SCREENSHOTS_OUT` is set). Commit only images that visibly changed, GIFs included. The GIF's frames must share one size.
- Mockups: `pwsh -File tools/Render-Mockup.ps1 -Html docs/mockups/<file>.html [-Out <png>] [-Width W -Height H]`. UI changes are sketched and approved this way first.
- Icons: `pwsh -File tools/New-AppIcons.ps1` rewrites `assets/icons`.
- macOS bundles: `tools/Build-MacPackage.sh [-v x.y.z] [-a arm64|x64|both]` builds the signed (with `APPLE_*` set) or ad-hoc `.pkg`; `tools/mac/new-dev-bundle.sh` makes a real-id `keycuts.app` (default `~/Desktop`) that uses your real settings and `~/Shortcuts`; `tools/mac/new-test-bundle.sh <sandbox>` makes the throwaway Debug one `check-app.sh` and `run` use.
- macOS UI checks: `tools/mac/check-app.sh [menus|drops|browse|contrast|torn]` drives a throwaway bundle of the real app (Finder and Safari drags, the file panel, Increase contrast, the Dock). It hides your other apps and flips Increase contrast while it runs; read `tools/mac/README.md` first.
- macOS/Linux: build and test with `-p:EnableWindowsTargeting=true`; `dotnet format` takes no `-p:`, so set the environment variable: `EnableWindowsTargeting=true dotnet format keycuts.sln --verify-no-changes --severity error`. `dotnet test keycuts.Common.Tests` and `keycuts.App.Tests` run there (CI's `macos` job); a test that needs Windows behavior (cmd, `C:\` paths, PATH lookup, the registry) is `[WindowsFact]`/`[WindowsTheory]`; an `[AvaloniaFact]` measured in Segoe UI starts with `Assert.SkipUnless(OperatingSystem.IsWindows(), …)`.

## Gotchas

- **0 warnings.** A new project with its own `AssemblyInfo.cs` needs `SupportedOSPlatform("windows")` or CA1416 returns, except the `net10.0` ones (Common, CLI, `keycuts.Common.Tests`): there, mark each Windows-only type or member `[SupportedOSPlatform("windows")]` and guard its callers with `OperatingSystem.IsWindows()`. Count with `--no-incremental`.
- Build fails with MSB3021/MSB3027 if `keycuts.GUI.exe` is running: kill it (approved). Close any instance you launch.
- Package versions only in `Directory.Packages.props` (CPM). No lockfiles.
- **Tests never touch the real output folder, registry, PATH, network or Recycle Bin.** Use explicit `OutputFolder` and `Overwrite` (null reads HKCU), a fake `IPathUpdater`, fake `NameConflictEnvironment`/`checkName`, fake `IClassesStore`, `ISettingsStore`, `FakeReleaseSource`/`FakeUpdateState`. Never `KeycutRepository.CreateDefault()` (recycles, can hang on a shell dialog) or `UpdateChecker.CreateDefault()`. `PathVariables` has an injectable overload.
- Tests that only hold on one OS use `[WindowsFact]`/`[WindowsTheory]`/`[MacFact]`/`[MacTheory]` (`keycuts.Common.Tests/PlatformFacts.cs`); they're skipped elsewhere.
- Key tests press real keys through Avalonia.Headless (`MainWindowTests.Press`, `window.KeyTextInput`) to whatever has the focus; handlers read `e.KeyModifiers`. Avalonia has no static keyboard state: Shift held outside the window is read with `Modal.IsShiftDown()` (Windows only).
- The view models ask synchronously (`ConfirmName`, `ShowEditForm`, pickers): dialogs run in a nested dispatcher frame (`Modal.Show`/`Modal.Wait`); tests answer them with `Modal.Answer`.
- Avalonia traps: a style `Setter` holding a control shares one instance (the row menu is a template, built per row); a local `Margin` outranks a style, so a live line's margin goes in a style that `.silent` can override; a view takes its first focus on `Loaded`, not when attached.
- Fakes of `IKeycutActions` (`FakeActions`, `RecordingActions`) must implement every member.
- **macOS bundle data lives in `Contents/Resources`, never in `Contents/MacOS`** (`BundleResources.Folder(name)`, which falls back to the folder beside the executable for dev builds): the bundle seal treats every file in `Contents/MacOS` as code and Apple's notary refuses the app ("the signature of the binary is invalid"). `Build-MacPackage.sh` does the move; new shipped data goes through `BundleResources`. Internals: `docs/internals.md`, the macOS sections.
- Close any keycuts you launch to look at (it keeps running in the menu bar). Two copies run from the same executable path are one app to macOS (activation goes to the older one), so run a test copy from another folder.
- **Line endings:** `* text=auto`; mixed CRLF/LF is normal, don't convert. Byte-sensitive: `*.cmd` shims (CRLF) and `tests/corpus/**/*.bat`. Never rewrite those through anything that normalizes newlines.
- Debug builds honor `KEYCUTS_OUTPUT_FOLDER`; `tools/New-SampleKeycuts.ps1` makes fixture folders.
- CI (`.github/workflows/ci.yml`): a `changes` job skips the rest when only `docs/`, `README.md`, `CLAUDE.md` or `gameplan.md` changed (a new root doc goes in that `grep`) (a skipped job counts as passed for the required checks; never use `paths-ignore` here). `build` (Windows) and `format` (Linux) are required on `develop`/`main`, `macos` on `develop`; PRs into `main` also build both MSIs. Failed runs upload `test-results` (TRX + `.received.txt`). Actions pinned to SHAs.

## Versioning and releases

`<Version>` in `Directory.Build.props` is the only version (three parts). Pushing to `main` runs `release.yml`, which publishes `v<Version>` if the tag doesn't exist. It has five jobs: `version`, then `windows` (tests, MSIs) and `macos` (both signed, notarized `.pkg` files) in parallel, then `publish` (checks the files, `SHA256SUMS.txt`, a draft release published once everything is attached), then `cask` (a PR in `andrewralon/homebrew-tap`; its own job, so a failure leaves the release alone). `gh workflow run release.yml --ref <branch> -f dry_run=true` runs everything but the release and the cask PR. The `HOMEBREW_TAP_TOKEN` secret expires. To release: bump the version on `develop`, **regenerate the screenshots and GIFs** and commit visible changes, merge `develop` into `main`. Never tag by hand.

## Architecture

- `keycuts.Common`: domain logic, no UI, no logging (`Runner`'s "Shortcut created" line excepted). `.lnk` via late-bound `WScript.Shell` COM.
- `keycuts.App` (assembly **`keycuts.GUI`**, so `keycuts.GUI.exe`, the context menu, Start menu and MSI names stayed): the Avalonia app, `net10.0`, namespaces `keycuts.GUI.*`. It replaced WPF at the cutover (`docs/macos-port.md` Phase 3.12). One window, three screens (Creator, Manager, Settings) switched by `ShellViewModel`. No system title bar: `WindowDecorations=BorderOnly` with the client area extended, and `Views/RailView` is a folder-tab bar whose element roles (`TitleBar`, `User`, `MinimizeButton`, `CloseButton`) give Windows its caption and buttons. **The window is never maximized** (owner's decision): `CanMaximize=false`, and `NoMaximizeHook` refuses a posted `SC_MAXIMIZE`, drops the classic caption paints and adds `WS_SYSMENU`; the disabled Maximize is `User`, not `MaximizeButton` (Windows 11 snap layouts). Side effects go through `IKeycutActions`. Settings save immediately (no Save button); the folder only on commit. Themes: `Themes/*.axaml` (Fluent as the base under `Controls.axaml`), high contrast filled from `GetSysColor` through `SystemColor.Name`. The Manager's table is `Avalonia.Controls.DataGrid` with its own row, cell and header themes.
- `keycuts.CLI`: assembly is **`keycuts.exe`**. Verbs `create` (default; `-t`/`-s`, old `-d` deliberately rejected), `paste <name>`, `list [<search>] [--usage] [--sort] [--json]` (`Common.KeycutListing`), and `rm`, `rename`, `open` (`Common.KeycutCommands`, through `KeycutRepository` like the Manager). Bare `keycuts` launches the GUI. PATH gets only the output folder; the `keycuts.cmd`/`keypaste.cmd` shims live there (`Common.CommandShims`, #248) and find `keycuts.exe` through HKCU `InstallFolder`.
- `keycuts.Common.Tests`: references Common only, no GUI types (`net10.0`). Namespace `keycuts.Tests`. Shares `PlatformFacts`, `RepoFiles`, `TestNameChecks` and `UpdateFakes` with `keycuts.App.Tests` as linked files.
- `keycuts.App.Tests`: Avalonia.Headless (`[AvaloniaFact]`) on the real `App`, drawn with Skia (so geometry, `CaptureRenderedFrame` and the README screenshots are real); `App.LoadThemePreference` is faked so it never reads the registry. Runs on Windows and macOS.

**UI rules:** follow `docs/styleguide.md` (update it first for a design change). Views use its tokens and `Themes/Controls.axaml` themes, never literal sizes/colors; `StyleGuideTests` enforces it on `keycuts.App`'s `.axaml`, with deliberate exceptions listed in its `Exceptions`.

### Creation and parsing

All creates end in `Runner.Run` → `CreateShortcutFile` (GUI goes through `KeycutCreator` first, which validates with `TargetResolver` and name checks; the CLI doesn't). All reads go through `ShortcutFile` (`KeycutRepository.TryParse`). Changing the emitter or parser affects every entry point.

`.bat` format: header `REM keycuts <version>`, then `REM <shortcut>`, `<type>`, `<target>` (pre-1.3: `<destination>`, still read), `<description>`, launch line, the two run-counting lines (`UsageLog.BatLines`, #187), `EXIT`. REM type+target win when valid; otherwise the launch line is regex-parsed (0.1.x files). Corpus of real files in `tests/corpus/legacy/`.

### macOS keycuts

`~/Shortcuts/<name>`: line 1 `#!/bin/sh`, line 2 `# keycuts <version>` (that pair makes a file a keycut; the `keycuts`/`keypaste` shims and any other script are ignored), then `# <shortcut>`, `<type>`, `<target>`, `<description>`, the launch line (`open '<target>'`, `open -a '<app>' '<file>'`; single-quoted, `$HOME` in double quotes when variables are on), the run-count lines (`${0##*/}.runs` under `~/Library/Application Support/keycuts/usage`) and `exit $s`. Run as `/bin/sh <file>` (`MacDesktop`), so the execute bit isn't needed from the Run box. Reading and rewriting share the `.bat`'s code (`IScriptFormat`); `CanEditInForm` and the corpus rules apply the same way. A symlink is followed to its target except into `/System/Cryptexes/` (Safari keeps `/Applications/Safari.app`). No `.lnk`, `.url`, CLSID or hosts-file keycuts, and no folder-background right-click: Windows-only.

### Landmines

- Env-var substitution (`PathVariables.Substitute`) runs **after** classification. URLs are cmd-escaped; File/Folder paths are not (so `%VAR%` expands).
- Descriptions double every `%` (`SanitizeDescription`/`ParseDescription`); a `%~` on a REM line kills the script.
- Folder/CLSID launch lines use `START "" /B "%SystemRoot%\explorer.exe"`; their parser patterns are unanchored to read the old direct form too. **Don't anchor them.**
- `Runner.BuildCommand` switch has no discard arm on purpose (new `ShortcutType` = CS8509).
- `ShortcutType.Command` is never produced by create. The CLSID regex `[shell:]?` is a char class (known bug, pinned).
- `KeycutRepository.CanEditInForm` allows only files the emitter re-writes identically. **Don't loosen it.** Rename and description edits rewrite one line only and work on hand-edited files.
- Never write through `Registry.ClassesRoot` (resolves to HKLM). Context menu lives in `HKCU\Software\Classes`; a test matches it to `installer/Package.wxs`. Nothing needs elevation.
- `UserPathUpdater` edits `HKCU\Environment` directly (preserves `REG_EXPAND_SZ`). GUI creates never change PATH.
- Names `keycuts`/`keypaste` are refused and existing ones renamed to `*_previous` at startup.
- Run counts live in `%LOCALAPPDATA%\keycuts\usage`; tests use `new UsageLog(<temp>)`, never `UsageLog.CreateDefault()`.
- Manager, Settings, welcome, update check, app icon, target check, run counts and parser fallback: **read the matching section of `docs/internals.md` before changing them** (DataGrid column traps, store semantics, edit/undo safety rules).

## Sequencing rule

**Don't mix a behavior change with a refactor or port step.** A commit can hold as many changes as it needs; what matters is that a snapshot diff has one explainable cause. Behavior (bugs included) is pinned by `keycuts.Common.Tests/Characterization/` snapshots. A mismatch writes `.received.txt`; if intended, copy it over `.verified.txt` in the same commit.

## Conventions

`#region` blocks, `var` everywhere, `out` params in legacy code. Allman braces, 4-space C#, 2-space XAML/csproj. GUI has `Nullable` on, Common doesn't. Match surrounding style.

## Docs

`README.md` (user-facing), `docs/internals.md` (subsystem behavior), `docs/testing.md` (the test layers and lessons learned: flaky headless tests, driving the real app on a Mac, CI), `docs/styleguide.md`, `docs/mockups/` (approved sketches), `docs/ux-review.md`, `docs/tickets/`, `docs/design-reference/` (visual spec only; never add React/WebView), `gameplan.md` (roadmap), `docs/macos-port.md` (the macOS and Avalonia plan; keeps today's project names, no `src/` restructure).
