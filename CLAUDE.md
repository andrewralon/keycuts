# CLAUDE.md

## What keycuts is

keycuts writes **generated `.bat` files** into a folder on `PATH` so Win+R opens anything by nickname (`npp`, `gmail`). The `.bat` is the product: no database, no list file. The output folder (default `%USERPROFILE%\Shortcuts`) is stored in `HKCU\Software\TeamRalon\keycuts`. Entry points: the WPF app, the Explorer right-click verb (passes the target as `args[0]`, `keycuts.GUI/App.xaml.cs`), the CLI, or `keycuts.Common`.

## Commands

.NET SDK 10, Windows only. `global.json` pins the 10.0.4xx band (`latestPatch`). Run from the repo root:

- Build: `dotnet build keycuts.sln`
- Test: `dotnet test keycuts.sln` (xUnit, `keycuts.Tests`); one test: `dotnet test keycuts.Tests --filter "FullyQualifiedName~KeycutCreatorTests.SomeTest"`
- Format: `dotnet format keycuts.sln --verify-no-changes --severity error` (keep `--severity error`)
- Installer: `pwsh -File tools/Build-Installer.ps1 [-Version x.y.z] [-Runtime win-x64|win-arm64]` → `artifacts/installer/<runtime>/`. `installer/` is not in the sln. Don't install a local MSI over a release whose bundled .NET runtime is newer than your SDK's: the upgrade won't downgrade the runtime's files but the old product's removal deletes them, so `keycuts.exe` can't start (`hostpolicy.dll` missing). Uninstall first or `msiexec /fa`, or publish with the release's runtime (`KnownFrameworkReference` `LatestRuntimeFrameworkVersion`) to test a real upgrade.
- Screenshots and GIFs: `pwsh -File tools/New-Screenshots.ps1` rewrites `docs/images` (the README's PNGs and its `walkthrough-*.gif`) via `ScreenshotTests` (inert unless `KEYCUTS_SCREENSHOTS_OUT` is set). Commit only images that visibly changed, GIFs included. The GIF's frames must share one size.
- Mockups: `pwsh -File tools/Render-Mockup.ps1 -Html docs/mockups/<file>.html [-Out <png>] [-Width W -Height H]`. UI changes are sketched and approved this way first.
- Icons: `pwsh -File tools/New-AppIcons.ps1` rewrites `assets/icons`.
- macOS/Linux: build/format with `-p:EnableWindowsTargeting=true`; tests only run on Windows (CI).

## Gotchas

- **0 warnings.** A new project with its own `AssemblyInfo.cs` needs `SupportedOSPlatform("windows")` or CA1416 returns. Count with `--no-incremental`.
- Build fails with MSB3021/MSB3027 if `keycuts.GUI.exe` is running: kill it (approved). Close any instance you launch.
- Package versions only in `Directory.Packages.props` (CPM). No lockfiles.
- **Tests never touch the real output folder, registry, PATH, network or Recycle Bin.** Use explicit `OutputFolder` and `Overwrite` (null reads HKCU), a fake `IPathUpdater`, fake `NameConflictEnvironment`/`checkName`, fake `IClassesStore`, `ISettingsStore`, `FakeReleaseSource`/`FakeUpdateState`. Never `KeycutRepository.CreateDefault()` (recycles, can hang on a shell dialog) or `UpdateChecker.CreateDefault()`. `PathVariables` has an injectable overload.
- WPF-hosting test classes go in `[Collection(WpfCollection.Name)]`.
- Key tests use `AccessibilityTests.Press` / `FakeKeyboardDevice`; handlers read `e.KeyboardDevice.Modifiers`, never static `Keyboard.*` (flaky if a real key is held). `CreatorView` still reads static state.
- Fakes of `IKeycutActions` (`FakeActions`, `RecordingActions`) must implement every member.
- **Line endings:** `* text=auto`; mixed CRLF/LF is normal, don't convert. Byte-sensitive: `*.cmd` shims (CRLF) and `tests/corpus/**/*.bat`. Never rewrite those through anything that normalizes newlines.
- Debug builds honor `KEYCUTS_OUTPUT_FOLDER`; `tools/New-SampleKeycuts.ps1` makes fixture folders.
- CI (`.github/workflows/ci.yml`): `build` (Windows) and `format` (Linux) are required on `develop`/`main`; PRs into `main` also build both MSIs. Failed runs upload `test-results` (TRX + `.received.txt`). Actions pinned to SHAs.

## Versioning and releases

`<Version>` in `Directory.Build.props` is the only version (three parts). Pushing to `main` runs `release.yml`, which publishes `v<Version>` if the tag doesn't exist. To release: bump the version on `develop`, **regenerate the screenshots and GIFs** and commit visible changes, merge `develop` into `main`. Never tag by hand.

## Architecture

- `keycuts.Common`: domain logic, no UI, no logging (`Runner`'s "Shortcut created" line excepted). `.lnk` via late-bound `WScript.Shell` COM.
- `keycuts.GUI`: WPF, one window, three screens (Creator, Manager, Settings) switched by `ShellViewModel`. No Windows title bar: `Views/RailView` is a folder-tab bar with own caption buttons under `WindowChrome`. **The window is never maximized** (owner's decision; `NoMaximizeHook`). `NoSystemCaptionHook` stops Windows painting classic caption buttons over the bar. Side effects go through `IKeycutActions`. Settings save immediately (no Save button); the folder only on commit.
- `keycuts.CLI`: assembly is **`keycuts.exe`**. Verbs `create` (default; `-t`/`-s`, old `-d` deliberately rejected), `paste <name>`, `list [<search>] [--usage] [--sort] [--json]` (`Common.KeycutListing`), and `rm`, `rename`, `open` (`Common.KeycutCommands`, through `KeycutRepository` like the Manager). Bare `keycuts` launches the GUI. PATH gets only the output folder; the `keycuts.cmd`/`keypaste.cmd` shims live there (`Common.CommandShims`, #248) and find `keycuts.exe` through HKCU `InstallFolder`.
- `keycuts.Tests`: references Common and GUI.

**UI rules:** follow `docs/styleguide.md` (update it first for a design change). Views use its tokens and `Themes/Controls.xaml` styles, never literal sizes/colors; `StyleGuideTests` enforces it, with deliberate exceptions listed in its `Exceptions`.

### Creation and parsing

All creates end in `Runner.Run` → `CreateShortcutFile` (GUI goes through `KeycutCreator` first, which validates with `TargetResolver` and name checks; the CLI doesn't). All reads go through `ShortcutFile` (`KeycutRepository.TryParse`). Changing the emitter or parser affects every entry point.

`.bat` format: header `REM keycuts <version>`, then `REM <shortcut>`, `<type>`, `<target>` (pre-1.3: `<destination>`, still read), `<description>`, launch line, the two run-counting lines (`UsageLog.BatLines`, #187), `EXIT`. REM type+target win when valid; otherwise the launch line is regex-parsed (0.1.x files). Corpus of real files in `tests/corpus/legacy/`.

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

**Never move two variables in one commit.** Behavior (bugs included) is pinned by `keycuts.Tests/Characterization/` snapshots. A mismatch writes `.received.txt`; if intended, copy it over `.verified.txt` in the same commit.

## Conventions

`#region` blocks, `var` everywhere, `out` params in legacy code. Allman braces, 4-space C#, 2-space XAML/csproj. GUI has `Nullable` on, Common doesn't. Match surrounding style.

## Docs

`README.md` (user-facing), `docs/internals.md` (subsystem behavior), `docs/styleguide.md`, `docs/mockups/` (approved sketches), `docs/ux-review.md`, `docs/tickets/`, `docs/design-reference/` (visual spec only; never add React/WebView), `gameplan.md` (roadmap; `src/Keycuts.Core` layout is future work).
