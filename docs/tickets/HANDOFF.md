# Implementation handoff — Keycuts UI redesign

Read this file first, then [README.md](README.md) (ticket index + decisions), then the ticket you're working on.
This file holds context that isn't in the individual tickets: environment, baseline, safety rules, file ownership, and workflow.

## 1. Source of truth

| What | Where | Notes |
| --- | --- | --- |
| Requirements and decisions | `docs/tickets/README.md` and `KC-*.md` | The **Decisions** table is final. Don't reopen it. |
| Visual/interaction spec | `Figma Redesign Keycuts UI2/src/App.tsx` + `src/index.css` | React prototype. **UI2 supersedes `Figma Redesign Keycuts UI`**; ignore the old folder. |
| Icons | `Figma Redesign Keycuts UI2/public/icons/*.svg` + `iconPaths` in `App.tsx` | Convert to XAML geometry (KC-06). |

Reading the prototype:
- Port **look and behavior**, not code. Production is WPF on .NET 10. Don't add a WebView, React, or Tailwind to the app.
- Ignore preview-only chrome: the sidebar, state tabs (`default/hover/error/empty`), the fake `TitleBar`, `StyleGuide`, the
  `SettingsWindow` mock, and `setTimeout` fakes.
- CSS px sizes in the prototype are deliberately tiny (9–12px text) because of the preview canvas. Keep the **proportions** and use
  the readable WPF type ramp defined in KC-05.
- The `has-error` class on `.quick-drop` during a duplicate-name error is a preview artifact (see KC-09).
- Optional: to view the prototype, run `pnpm install` then `pnpm dev` in `Figma Redesign Keycuts UI2/` (Node + pnpm; versions in `.mise.toml`).
  Never commit `node_modules`.

## 2. Environment and baseline (captured 2026-10-15)

- Windows, .NET SDK **10.0.204**. Solution: `keycuts.sln` (CLI, GUI, Common, Batmanager, keypaste).
- Git branch at handoff: `modernize-sdk-style-net10`. Untracked: `docs/`, `Figma Redesign Keycuts UI/`, `Figma Redesign Keycuts UI2/`,
  and `keycuts-prototype/` (an empty folder with only `.git`). `bin/` and `obj/` are gitignored and not tracked.
- `dotnet build keycuts.sln` **succeeds** (re-verified after the app was closed). Baseline: **27 CA1416 warnings** (22 in `keycuts.Common`, 4 in `keycuts.GUI`, 1 in `keypaste`).
  They're pre-existing. Don't fix them in bulk, and don't add new ones. Count warnings with `dotnet build keycuts.sln --no-incremental`;
  an incremental build skips compilation and misleadingly reports 0.
- **Build lock gotcha:** if `keycuts.GUI.exe` or `keycuts.Batmanager.exe` is running, the build fails with MSB3021/MSB3027
  ("file is locked by keycuts.GUI (pid)"). That's not a code error. **The user has approved force-closing these processes at any time.**
  Find the PID with `Get-Process keycuts.GUI, keycuts.Batmanager -ErrorAction SilentlyContinue` and stop it with `Stop-Process -Id <pid>`.
  Close any instance you launched for manual checks before you finish a ticket.
- No tests, `.editorconfig`, or CI exist yet. KC-01 adds them, along with the canonical commands:
  - `dotnet format keycuts.sln --verify-no-changes`
  - `dotnet build keycuts.sln`
  - `dotnet test keycuts.sln`

## 3. Safety rules (important)

The production code touches real user state. Agents must not change the developer's machine as a side effect.

| Code | Side effect | Rule |
| --- | --- | --- |
| `Runner.Run` → `PathSetup.AddToOrReplaceInSystemPath` | Edits the user's **PATH** environment variable | Tests must never reach it. KC-04 adds a seam; use a fake. |
| `RegistryStuff.Set*` | Writes `HKCU\Software\TeamRalon\keycuts` (OutputFolder, ForceOverwrite, RightClickContextMenu) | Tests must not write the registry. Inject the output folder instead. |
| `RegistryStuff.CreateRightClickContextMenus` | Writes **HKCR** shell keys (may need admin) | Never call it from tests or verification scripts. |
| Output folder (default `C:\Shortcuts`) | Real user keycuts live here | Tests and fixtures use temp folders only (`Path.GetTempPath()`). Never delete or overwrite files in the real output folder. |
| `KeycutRepository.DeleteAsync` / Delete menu | Deletes files | Exercise it only against temp or fixture folders. |

For manual runs of the GUI against fixtures, use the debug-only `KEYCUTS_OUTPUT_FOLDER` override from KC-16. Don't change the
registry setting. If you must change it, record the original value and restore it.

## 4. Code facts that affect implementation

- `keycuts.GUI` has `<Nullable>enable</Nullable>`. `keycuts.Common` does **not** (it has no nullable annotations). Match each project.
- Existing style: block-scoped namespaces, `#region` blocks, 4-space indent, braces on new lines, `var` everywhere. GUI windows use
  code-behind with `INotifyPropertyChanged` on the window. KC-08 introduces lightweight MVVM; follow that pattern afterward.
- `Shortcut.GetShortcutTypeFromDestination` **throws** for missing paths (`File.GetAttributes`). The CLI depends on its current behavior,
  so add the non-throwing resolver alongside it (KC-02) instead of changing semantics.
- `ShortcutFile` regex-parses `.bat` lines. Line selection uses `Contains("\\explorer.exe\"")` or `StartsWith("START ")`, so `REM` lines must
  be excluded explicitly once the description line exists (KC-03). It also writes to `Console`, which is harmless; don't add more.
- `.bat` format written by `Runner.CreateShortcutFile`:
  ```
  @ECHO OFF
  REM keycuts <version>
  REM <shortcut>C:\Shortcuts\name.bat</shortcut>
  REM <type>url|file|folder|hostsfile|clsidkey</type>
  REM <destination>…</destination>
  REM <description>…</description>      <-- added by KC-03 (always written; empty by default)
  <command line>
  EXIT
  ```
- `App.xaml.cs` reads `e.Args[0]` as a destination. That's how Explorer's "keycut this!" right-click menu opens the GUI, so it must keep working.
- `Process.Start(path)` doesn't use the shell on .NET Core. Use `new ProcessStartInfo(path) { UseShellExecute = true }`.
- `Microsoft.Win32.OpenFolderDialog` is available in WPF on .NET 8+ (GUI targets `net10.0-windows10.0.19041.0`).

## 5. Where things will live

This is a map of the files each ticket is expected to create or change. Use it to find code that earlier tickets added.

| Ticket | Primary files (new unless noted) |
| --- | --- |
| KC-01 | `keycuts.Tests/*`, `keycuts.sln`*, `.editorconfig`, root `README.md`* |
| KC-02 | `keycuts.Common/DestinationResolver.cs`, `DestinationResult.cs`, `KeycutKind.cs`; tests |
| KC-03 | `keycuts.Common/Keycut.cs`, `KeycutRepository.cs`, `ShortcutFile.cs`*, `Runner.cs`* (description line only); tests |
| KC-04 | `keycuts.Common/KeycutCreator.cs`, `Runner.cs`* (PATH seam), `PathSetup.cs`*; tests |
| KC-05 | `keycuts.GUI/Themes/{Light,Dark,Tokens,Controls}.xaml`, `ThemeManager.cs`, `App.xaml`* |
| KC-06 | `keycuts.GUI/Themes/Icons.xaml`, `Controls/Icon.cs`, `App.xaml`* (merge only) |
| KC-07 | `keycuts.GUI/Controls/TypeBadge.*`, theme badge brushes in `Light/Dark.xaml`* |
| KC-08 | `keycuts.GUI/Mvvm/*`, `ViewModels/ShellViewModel.cs`, `MainWindow.xaml(.cs)`*, `App.xaml.cs`*, `MainFormLogic.cs`* |
| KC-16 | `tools/New-SampleKeycuts.ps1`, design-time data classes, the `KEYCUTS_OUTPUT_FOLDER` debug override |
| KC-09 | `keycuts.GUI/Views/CreatorView.xaml(.cs)`, `ViewModels/CreatorViewModel.cs` (state + layout bindings) |
| KC-10 | `CreatorView.xaml(.cs)`*, `CreatorViewModel.cs`*, `DragDrop.cs`* |
| KC-11 | `CreatorViewModel.cs`*, `CreatorView.xaml`*, `CommonLogic.cs`* |
| KC-12 | `keycuts.GUI/Views/ManagerView.xaml(.cs)`, `ViewModels/ManagerViewModel.cs`, `KeycutRowViewModel.cs` |
| KC-13 | `ManagerView.xaml`*, `ManagerViewModel.cs`* |
| KC-14 | `ManagerView.xaml`*, `ManagerViewModel.cs`*, `KeycutRowViewModel.cs`* |
| KC-15 | `ManagerViewModel.cs`* / `KeycutRowViewModel.cs`* (Edit command only) |
| KC-20 | Deletes `keycuts.Batmanager/`; `keycuts.sln`*, `Runner.cs`*, `MainFormLogic.cs`* |
| KC-17/18 | Cross-cutting fixes, `README.md`* |

`*` = an existing file, or one an earlier ticket created.

Names are suggestions. If you choose different names, update this table so later agents can find things.

## 6. Execution order (sequential — one ticket at a time)

Work through the tickets **one at a time, in this order**. Every ticket's dependencies come earlier in the list. Don't run subagents in parallel.

| Step | Ticket | Step | Ticket |
| --- | --- | --- | --- |
| 1 | KC-01 Test project and verification commands | 11 | KC-10 Creator: drop, paste/type, and Browse |
| 2 | KC-02 Destination detection and validation | 12 | KC-11 Creator: submit flow |
| 3 | KC-03 Repository and description metadata | 13 | KC-12 Manager: layout and table |
| 4 | KC-04 Create-keycut service | 14 | KC-13 Manager: search and sort |
| 5 | KC-05 Design tokens and themes | 15 | KC-14 Manager: row actions menu |
| 6 | KC-06 Icon library | 16 | KC-15 Manager: Edit action |
| 7 | KC-07 Type badge control | 17 | KC-20 Retire Batmanager |
| 8 | KC-08 Single-window shell | 18 | KC-17 Keyboard and accessibility pass |
| 9 | KC-16 Sample data and fixtures | 19 | KC-18 QA, docs, and cleanup |
| 10 | KC-09 Creator layout and states | — | KC-19 is already resolved; skip it |

After each ticket, the orchestrator runs the full verification commands (section 2) before starting the next.

## 7. Per-ticket workflow (every subagent follows this)

1. Read this file, `README.md`, and the assigned `KC-XX` ticket. Read the code you'll touch before editing.
2. Implement **only** that ticket's scope. Don't refactor unrelated code or fix pre-existing warnings.
3. Add or extend unit tests for any logic (Common services, view models). UI-only XAML doesn't need unit tests.
4. Run `dotnet build` and `dotnet test` for the whole solution. If a running keycuts process locks the output, force-close it (section 2).
   Run `dotnet format --verify-no-changes` once KC-01 has landed. Build warnings must not grow beyond the baseline.
5. Update the ticket file:
   - Tick the acceptance-criteria checkboxes you verified.
   - Append `## Implementation notes` with: files changed, key decisions, deviations from the ticket and why, and **manual QA still needed**
     (anything you couldn't verify headlessly, such as drag from Explorer or a browser, Narrator, live theme switching, DPI scaling, or real clipboard use).
6. Report back: a summary, changed files, test results, the manual-QA list, and any blockers or new questions.

If a ticket conflicts with the code or another ticket, choose the most conservative option that satisfies the spec. Record it under
`## Implementation notes` and in the README's **Decisions** table if it affects other tickets. Escalate to the user only for
product decisions that can't be defaulted safely.

## 8. What agents can't fully verify

Headless agents can build, unit-test, and launch the app, but can't reliably perform real drag-and-drop from Explorer or a browser, use Narrator,
toggle the Windows theme, or change DPI. Collect these from every ticket's notes into the **manual verification matrix** in KC-18 for
the user to run. UI-automation smoke tests (for example FlaUI) are optional and nice to have, not required.
