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
- [ ] `dotnet build keycuts.sln` and `dotnet test keycuts.sln` pass with the project removed.
- [ ] A case-insensitive search for "batmanager" across `*.cs`, `*.xaml`, `*.csproj`, `*.sln`, and `*.md` finds only historical mentions in `docs/tickets/`.
- [ ] The GUI, CLI, and Explorer right-click flows still work.
- [ ] The PR description lists the dropped features from the parity table.
