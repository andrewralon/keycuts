# KC-20 — Retire the Batmanager app

**Area:** Cleanup · **Depends on:** 12, 14, 15 (the new manager must cover the actions people use first)

## Decision
`keycuts.Batmanager` is retired. The in-app Keycut Manager (KC-12 to KC-15) replaces it.

## Feature parity check
| Batmanager feature | New manager | Notes |
| --- | --- | --- |
| List keycuts in the output folder | KC-12 | |
| Run (Enter / Ctrl+R / menu) | **Open keycut** (KC-14) | Menu only. Rows have no Enter or click behavior by design. |
| Edit in Notepad (E / Ctrl+E / menu) | **Edit** (KC-15) | Same behavior. |
| Copy cell (C / Ctrl+C) | **Copy destination** (KC-14) | Destination only. |
| Delete (Del / menu) | **Delete** (KC-14) | Menu only, no confirmation (same as Batmanager). |
| Refresh (button / F5) | Auto-refresh plus F5 (KC-12) | |
| Open destination location (O / Ctrl+O) | **Dropped** | Out of scope ("no additional manager actions"). Note it in the PR. |
| Read-only output-folder display | **Dropped** | Shown in Settings. |
| Single-letter row shortcuts (E, O, C, Del) | **Dropped** | Rows aren't actionable in the new design. |

## Scope
- Remove `keycuts.Batmanager` from `keycuts.sln` and delete the `keycuts.Batmanager/` folder, including the committed `bin/` and `obj/` output if it's tracked.
- Remove the launch code:
  - `keycuts.Common/Runner.cs`: `OpenBatmanager()` and `FindBatmanager()`.
  - `keycuts.GUI/MainFormLogic.cs`: `OpenBatmanager()`.
  - `keycuts.GUI/MainWindow.xaml(.cs)`: the "Open Batmanager" button and `OpenBatmanager_Click`. These may already be gone after KC-09 and KC-12.
- Search the repo for any other references (installer scripts, README, shortcuts) and remove them.
- `ShortcutFile` and the `ShortcutType` parsing used by Batmanager stay in `keycuts.Common`, because the new repository (KC-03) uses them.

## Acceptance criteria
- [x] `dotnet build keycuts.sln` and `dotnet test keycuts.sln` pass with the project removed.
- [x] A case-insensitive search for "batmanager" across `*.cs`, `*.xaml`, `*.csproj`, `*.sln`, and `*.md` finds only historical mentions in `docs/tickets/`.
- [~] The GUI, CLI, and Explorer right-click flows still work.
- [x] The PR description lists the dropped features from the parity table.

## Implementation notes

**Files changed**
- Deleted `keycuts.Batmanager/` (13 tracked files; no bin/obj were tracked; no running Batmanager/GUI process).
- `keycuts.sln`: removed the project entry and its 12 configuration lines.
- `keycuts.Common/Runner.cs`: removed `OpenBatmanager()` and `FindBatmanager()`.
- `keycuts.GUI/MainFormLogic.cs`: removed `OpenBatmanager()`. The window button and click handler were already gone.
- `keycuts.Common/ShortcutFile.cs`: removed `[Browsable(false)]` on `Description` (and its Batmanager comment and the unused `System.ComponentModel` using). It only existed to hide the column in Batmanager's auto-generated grid.
- `README.md` (Development): format baseline updated to 28 errors in 7 files.

**Verification**
- `dotnet build keycuts.sln --no-incremental`: 0 errors, 27 warnings (unchanged; Batmanager contributed none).
- `dotnet test keycuts.sln`: 371 passed.
- `dotnet format keycuts.sln --verify-no-changes`: 28 WHITESPACE errors in 7 files, down from 37 in 9 (the removed Batmanager files held the difference). None added.
- Case-insensitive search of `*.cs/*.xaml/*.csproj/*.sln/*.md` outside `docs/tickets/` finds nothing. No installer, script, or registry references existed. Historical mentions remain in `docs/tickets/`.

**Dropped features (for the PR description)**: Open destination location (O / Ctrl+O), read-only output-folder display (shown in Settings), single-letter row shortcuts (E, O, C, Del). Run, Edit, Copy destination and Delete are kept as menu actions; Refresh becomes auto-refresh plus F5.

**Deviations**: none. The `[~]` criterion (GUI, CLI, Explorer right-click) is covered only by build/tests, since no code in those flows was changed beyond removing dead code.

**Manual QA still needed**
- Launch the GUI against a fixture folder (`KEYCUTS_OUTPUT_FOLDER`): create a keycut, and use Open keycut / Edit / Copy destination / Delete in the manager.
- CLI create and Explorer right-click "keycut this!" still open the GUI with the destination.
- Clean checkout builds without a stale `keycuts.Batmanager` folder.