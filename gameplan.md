# keycuts — Prioritized Gameplan

This document orders **all 44 open issues** (#9–#108) into an execution sequence. The macro-ordering was set by the project owner: **.NET 10 first, then the installer and context menu, then the old bug/feature backlog (#9–#79), with macOS work (Avalonia, shell scripts, packaging) pushed to the very end.** Everything below fills in the reasoning and the fine-grained order within that constraint.

## How to read this

Four phases, executed in order. Within a phase, items are listed in recommended execution order, not by issue number. Each item is tagged with effort (**S**mall / **M**edium / **L**arge, rough gut-feel, not story points) and what it blocks or unblocks.

**One deliberate deviation from the GitHub issue tracker's dependency links, explained up front:** the M3-Port milestone (#90–#99) was originally written as one continuous cross-platform architecture pass, with #90 (the platform abstraction seam) gating almost everything after it. That made sense when macOS was in scope for the near term. It doesn't anymore. Several of those issues turn out to have a **cheap, direct fix** that doesn't need the full seam — the bug is a few lines in `RegistryStuff.cs` or `Shortcut.cs`, not an architecture problem. This plan takes the cheap fix now and defers the formal interface wrapping to Phase 4, where it's needed for macOS anyway. Where this happens, it's called out explicitly.

---

## Phase 1 — .NET 10 Foundation

**Goal: the repo builds again, on .NET 10, with nothing behaviorally different.** This is a pure mechanical port. No bug fixes, no features, no architecture changes ride along — keeping it that way is what makes it low-risk and fast to review.

| Order | Issue | Effort | Why here / notes |
|---|---|---|---|
| 1 | #82 Remove the unused CommandLineParser reference from `keycuts.Common` | S | Free. Zero `using CommandLine` in that project. Unblocks restore for the library immediately. |
| 2 | #80 `.editorconfig`, `Directory.Build.props`, `global.json` | S | Do before the SDK-style conversion so the new project files are created under the right rules from the start, not retrofitted. |
| 3 | #81 Central Package Management + lockfiles | S | Same reasoning — set up before new `PackageReference`-style `.csproj` files exist, so they're CPM-clean on day one. |
| 4 | #83 Migrate all projects to SDK-style, retarget to .NET 10 | L | The main event. Deletes ~400 lines of generated cruft (`AssemblyInfo.cs`, `*.Designer.cs`, `packages.config`). |
| 5 | #86 GitHub Actions CI | M | Do immediately after the port lands, while the "does it actually build clean" question is fresh. Windows-only matrix leg for now — the Linux/macOS legs from the original CI design are deferred to Phase 4. |
| 6 | *(new, from code review)* Fix `Runner.OpenBatmanager()`'s exe-location logic | S | Not optional — it's collateral damage from item 4, not a nice-to-have. Its `#if DEBUG` branch does `Directory.GetParent(cwd).Parent.Parent` + `Path.Combine(..., "keycuts.Batmanager\bin\Debug\keycuts.Batmanager.exe")`, which assumes the pre-SDK-style output layout and breaks the moment #83 changes it to `bin\Debug\net10.0-windows\`. Its release-mode branch isn't safe either — it launches from `Directory.GetCurrentDirectory()`, the process's working directory, not the exe's own folder, which is fragile regardless of the port. Fix both to resolve relative to `AppContext.BaseDirectory` while this file is already being touched for the SDK-style migration. |

### Deferred out of Phase 1 (moved to Phase 4)

- **#84** (replace Shell32 COM `.lnk` resolution with a managed parser) — its only payoff is enabling a single `net10.0` target framework instead of `net10.0-windows`, which only matters once macOS is real. `<COMReference>` works fine in an SDK-style `net10.0-windows` project, so #83 does **not** need #84 first, despite what the tracker's dependency link currently says. Retarget to `net10.0-windows` and keep the COM reference for now.
- **#85** (restructure into `src/Keycuts.Core` / `Platform.Windows` / `Platform.MacOS` / …) — this reorganization exists to support two platform implementations. With one platform, it's churn without payoff. Keep the current five-project layout (renamed/retargeted in place) until Phase 4.

### Verification

`dotnet build` clean on Windows with warnings-as-errors, `dotnet test` (empty for now), all five apps launch. This is the point where the repo goes from "does not build" back to "works exactly like it did before, just on .NET 10."

---

## Phase 2 — Installer & Context Menu

**Goal: a new user can install keycuts, find it, and right-click actually works — without admin rights.** This is the most user-visible phase and directly resolves three long-standing complaints (#61, #64, and the SettingsWindow binding bug that makes #64 confusing to diagnose).

| Order | Issue | Effort | Why here / notes |
|---|---|---|---|
| 1 | *(from #107 checklist)* Fix `SettingsWindow.RightClickContextMenus` `NotifyPropertyChanged` name mismatch | S | The Settings checkbox can silently fail to reflect real state. Fix this **before** touching the context-menu registration itself, or you can't trust the UI while testing the next item. |
| 2 | #99 Move context menu registration from `HKCR` to `HKCU\Software\Classes` (direct fix, not the full `IShellIntegration` abstraction) | M | This is the root cause of #64. Edit `RegistryStuff.CreateRightClickContextMenus`/`RemoveRightClickContextMenus` directly: change the hive, and fix the handler-path capture (`Process.GetCurrentProcess().MainModule.FileName` → resolve explicitly to the installed GUI exe, not whatever process happens to toggle the setting). No elevation required after this. |
| 3 | #64 BUG — listing not available in context menu | — | Closed by #99. Verify the specific repro (ctrl+shift right-click / non-admin install) before closing. |
| 4 | #105 WiX v6 MSI installer with code signing | L | Per-user install (no elevation prompt), owns the `PATH` entry via `<Environment>`, offers the context-menu checkbox, generates the `keypaste` shim at install time. |
| 5 | #11 CLI — create a keycut to the app itself on first run | S | Small, and belongs directly in the first-run flow the installer sets up. |
| 6 | #61 BUG — after install, user has no idea how to find/use it | — | Closed by #105 + #11 together: installer finishes with "launch keycuts," first run explains the Win+R workflow and offers to create the self-keycut. |

### Note on #99 vs. the tracker

The original #99 body describes wrapping this behind `IShellIntegration`, gated on #90 (the seam). That's the *right* long-term shape, but the bug fix itself — swap the registry hive, fix the handler path — is about 20 lines in the existing `RegistryStuff.cs` and doesn't need an interface to ship. Do the direct fix now; the `IShellIntegration` wrapper happens in Phase 4 as part of the general core rework, at which point it should be a refactor of already-correct behavior, not a new bug fix.

### Verification

Fresh VM (or a clean user profile), no admin rights: install via MSI, right-click a file, "keycut this!" appears and works, `keycuts` is typeable at Win+R immediately after first launch.

---

## Phase 3 — Old Bug/Feature Backlog (#9–#79)

**Goal: work through the backlog that's been sitting since 2019, using the .NET-10-and-installer-fixed codebase as the base.** Grouped into four waves: prerequisites, correctness bugs, CLI modernization, then GUI/UX polish. Bugs before features within each wave, per the priority the project owner set.

### 3.0 — Prerequisites (do once, unlocks the rest of this phase)

The regex-based parser in `ShortcutFile.cs` has never had a test, and several of the bugs below require touching the emitter/parser or the settings layer. Fix the safety net before fixing the parser, not after.

| Order | Issue | Effort | Notes |
|---|---|---|---|
| 1 | ~~#87 Freeze a `.bat` corpus as the behavioral specification~~ **Done, closed** | M | 36 files under `tests/corpus/legacy/`, committed in `a4f67a4`. Sourced from `andrewralon/scripts` (real, years-old keycuts output) rather than a live `C:\Shortcuts` pull — no PC needed. **Surfaced a previously unknown second header schema** (`keycuts 0.1.1.5`, `old-format/`) that predates the current `REM <type>` format; #91's header work now needs a `v0` mapping too, not just `v1`→`v2`. Also found real-world instances of two things the corpus checklist only guessed at: header/content drift (`old-format/gg.bat`) and a hand-edited file working around the exact problem #31 targets (`old-format/db.bat`). See `tests/corpus/legacy/README.md` for the full breakdown. |
| 2 | #88 Characterization tests: pin current emitter/parser behavior, bugs included | M | |
| 3 | #89 Round-trip property test | S | Its failures become the requirement list for #91. |
| 4 | #90 Platform abstraction seam — **Windows-only implementation, scoped down from its original cross-platform form** | M | Introduces `IShortcutScriptFormat`, `IShortcutStore`, `IPathManager`, `ISettingsStore`, `IShellIntegration`, `ILinkResolver`, `IDestinationLauncher`, `IDestinationClassifier` with **one (Windows) implementation each**. This is what makes the next wave's fixes testable and reviewable as isolated changes instead of edits scattered across `Runner`/`Shortcut`/`RegistryStuff`. The macOS implementations get added later in Phase 4 without touching this code. |

### 3.1 — Correctness bugs

| Order | Issue | Effort | Notes |
|---|---|---|---|
| 1 | #68 BUG — can't create keycut to `applicationHost.config` | S | **Quick, standalone fix** — doesn't actually need to wait for #90. `Shortcut.GetShortcutTypeFromDestination` calls `File.GetAttributes` before checking existence/permissions; guard it and catch `UnauthorizedAccessException`, returning `Unknown` instead of throwing. Can be cherry-picked as early as Phase 1 if you want a fast win, but sequenced here to keep this document's phases clean. |
| 2 | #93 Fix `ShortcutType.Command`: parseable but never emitted | S | The emitter's `if/else` ladder has no branch for `Command`; it silently writes an empty command line. Fix the ladder (ideally as an exhaustive `switch` so a missing case is a compiler warning, not a silent no-op). |
| 3 | #91 Add the `keycuts-meta` JSON header + managed-region markers | M | Makes round-trip exact; demotes the legacy regex parser to a frozen fallback path, guarded permanently by #88's snapshots. |
| 4 | #92 Replace `ShortcutType` with `DestinationKind` | M | Folds `HostsFile` into `File`+`OpenWith` (fixes a second dead-code bug: the hosts-file path comparison happens after `Path.GetFullPath`, which never expands `%windir%`), and turns `CLSIDKey` into `ShellObject` + a per-alias catalog. |
| 5 | #9 GUI — handle drag-and-drop for CLSID key items (Recycle Bin, etc.) | M | Was blocked on the predecessor issue (#8) being fixed; #92's `ShellObject`/alias catalog is exactly the missing piece — implement drag-and-drop detection of shell namespace items against that catalog. |
| 6 | #94 Decouple `PATH` mutation from shortcut creation | M | **Correction to the original framing** (from reading `PathSetup.cs`/`Runner.cs` directly): PATH is *not* rewritten on every creation. `AddToOrReplaceInSystemPath(oldPath, newPath)` is a no-op unless the registry-recorded output folder differs from the folder resolved for that specific call — which, with no `-o` flag and Settings already synced, is never. The real finding is the opposite problem: **the app essentially never proactively puts its own output folder on `PATH`.** The old, deleted installer must have been doing that job. Still worth doing — decoupling is structurally correct regardless of trigger frequency, and it fixes the substring-`Contains` bug in `ExistsInSystemPath` (`C:\Shortcuts` currently matches `C:\Shortcuts2`) — but it does **not** close #65 (see below), and it raises the stakes on #105 owning PATH at install time: without that, a fresh install may not put anything on `PATH` at all. |
| 7 | #31 Substitute environment variables in paths | S | Now safe to do cheaply, because #91's header preserves the literal destination separately from the emitted/displayed command — substitution is purely a display/normalization concern once the header exists. |
| 8 | #95 Move settings from the registry to `settings.json` | M | Fixes the read-has-write-side-effects bug in `RegistryStuff.CreateSubKey`, and un-couples the settings write from the context-menu registration side effect (the same entanglement that made #64 hard to diagnose). |

### 3.2 — CLI modernization

| Order | Issue | Effort | Notes |
|---|---|---|---|
| 1 | #96 Rewrite the CLI on `System.CommandLine` | L | **This is what actually closes #65**, not #94 (corrected above). Read `Program.cs` directly: the "no" branch of the overwrite prompt never resets `result` from `ExitCode.FileAlreadyExists`, so `Main()` prints `Error: FileAlreadyExists` for a correct, intentional decline. Two lines, unrelated to `PATH`. Cheap enough to cherry-pick standalone before #96 if a fast win is wanted, but the `System.CommandLine` rewrite is a fine place to fix it properly. |
| 2 | #25 CLI — resolve working directory from invocation path, not exe path | — | Fixed as part of #96's rewrite; verify the original repro (`keycuts.bat` wrapper invoking `keycuts.exe` in Program Files). |
| — | #65 BUG — answering "no" to overwrite brings up an error | — | Closed by #96 (see above), not #94. |
| 3 | #97 Retire `keypaste` as a project; ship `keycuts paste` + a generated shim | M | Drops the `System.Windows.Forms` dependency entirely. |
| 4 | #108 Expand environment variables in the destination before copying to clipboard | S | New issue, filed against `keycuts paste` rather than the legacy `keypaste` — its own body says to do it here, not in the project #97 is deleting, so the logic isn't written twice. Depends on #31 having landed (3.1) for there to be a placeholder in the destination worth expanding. |

### 3.3 — GUI/Batmanager UX polish (stays WPF — see note below)

| Order | Issue | Effort | Notes |
|---|---|---|---|
| 1 | #54 Make column widths fixed to match window | S | Cheapest, most visible annoyance fix. |
| 2 | #79 BAT — open file location | S | Add "Open File Location" to the right-click menu; rename existing "Open Destination Location" to "Open Destination" per the issue. Natural to implement against #90's `IDestinationLauncher.RevealInFileManager`. |
| 3 | #40 Keep DataGrid state the same when refreshing or deleting | M | Preserve columns shown, order, sort, and scroll position across a refresh. |
| 4 | #55 Option to rename a shortcut file | M | Needs a file-rename operation through `IShortcutStore` plus the immediate list refresh called out in the issue. |
| 5 | #75 Accept keystrokes in BatManager, jump to first match | M | Type-ahead selection in the DataGrid. |
| 6 | #63 "Navigate on desktop" for the step-1 field | S–M | Scope is underspecified (likely: an "Open file picker" button next to the destination field). Confirm intent before implementing — flagged as the one item in this phase worth a quick clarifying question rather than a guess. |
| 7 | *(from #107 checklist)* Remove `Thread.Sleep(400)` from `MainWindow.CreateShortcut` | S | Blocks the UI thread on every single creation. Currently only listed as an acceptance criterion of #98 (Avalonia, Phase 4) — pulled forward here because it's a plain WPF fix that shouldn't stay broken for the whole Phase 3/4 gap. |
| 8 | *(from #107 checklist)* Implement or remove `BatFormLogic.Copy(DataGrid)` | S | Currently an empty no-op — whatever UI affordance triggers it does nothing silently. Same reasoning as above: listed under #98 today, doesn't need to wait for it. |

**Why these stay in WPF instead of waiting for Avalonia:** the Avalonia rewrite (#98) is deferred to Phase 4 along with the rest of macOS. If these UX bugs waited for that, they'd stay broken for the entire Phase 4 timeline. Fixing them in WPF now means Phase 4's Avalonia port has a **correct** WPF app to port from — #98's acceptance criteria should explicitly require porting forward the fixed behavior from this phase, not the original 2019 bugs.

### Verification

Re-run the Phase 0 corpus + characterization + round-trip suites after every issue in 3.0/3.1 — they're the guardrail that makes this whole wave safe. Each old issue's original repro (screenshots/steps in its GitHub body) gets manually re-tested before closing.

---

## Phase 4 — Cross-platform / macOS (deprioritized, do last)

Everything here was explicitly deprioritized by the project owner. Listed for completeness and so Phase 1–3 work is done in a way that doesn't make this harder later (in particular, #90's interfaces from Phase 3.0 are designed so macOS only needs a second *implementation*, not a redesign).

| Issue | What it is |
|---|---|
| #84 | Managed `.lnk` parser, replacing Shell32 COM — enables single `net10.0` target |
| #85 | Restructure into `src/Keycuts.Core` / `Platform.Windows` / `Platform.MacOS` / `Composition` |
| #98 | Rewrite GUI + Batmanager as one Avalonia app (must port forward the Phase 3.3 fixes) |
| #100 | `Keycuts.Platform.MacOS`: script format, classifier, launcher |
| #101 | macOS `PATH` management |
| #102 | Finder Quick Action (right-click) |
| #103 | NSServices + Objective-C services-provider shim |
| #104 | Document cross-platform parity gaps |
| #106 | macOS `.pkg`, notarization, Homebrew cask |
| *(from #107)* | The macOS-flavored subset of the tech-debt checklist (none currently — the checklist is Windows/general only) |

No further ordering guidance is given here since this phase is explicitly "later."

---

## Full issue index

| # | Phase | Title |
|---|---|---|
| 82 | 1 | Remove unused CommandLineParser reference |
| 80 | 1 | `.editorconfig` / `Directory.Build.props` / `global.json` |
| 81 | 1 | Central Package Management + lockfiles |
| 83 | 1 | SDK-style + .NET 10 retarget |
| 86 | 1 | GitHub Actions CI |
| — | 2 | SettingsWindow `NotifyPropertyChanged` mismatch (from #107 checklist) |
| 99 | 2 | Context menu → `HKCU\Software\Classes` |
| 64 | 2 | BUG — context menu missing (closed by #99) |
| 105 | 2 | WiX v6 MSI installer |
| 11 | 2 | Create keycut to the app itself on first run |
| 61 | 2 | BUG — user can't find/use app after install (closed by #105+#11) |
| 87 | 3.0 | Freeze `.bat` corpus |
| 88 | 3.0 | Characterization tests |
| 89 | 3.0 | Round-trip property test |
| 90 | 3.0 | Platform seam (Windows-only for now) |
| 68 | 3.1 | BUG — crash on `applicationHost.config` |
| 93 | 3.1 | Fix `Command` type emitter hole |
| 91 | 3.1 | `keycuts-meta` JSON header |
| 92 | 3.1 | `DestinationKind` rework |
| 9 | 3.1 | Drag-drop CLSID/Recycle Bin items |
| 94 | 3.1 | Decouple `PATH` mutation from creation (does not close #65 — see corrected note) |
| 31 | 3.1 | Substitute environment variables in paths |
| 95 | 3.1 | Settings → `settings.json` |
| 96 | 3.2 | CLI rewrite on `System.CommandLine` |
| 65 | 3.2 | BUG — overwrite "no" causes error (closed by #96, not #94) |
| 25 | 3.2 | BUG — working directory from command path (closed by #96) |
| 97 | 3.2 | Retire `keypaste` project |
| 108 | 3.2 | Expand env vars in clipboard destination |
| 54 | 3.3 | Fixed column widths |
| 79 | 3.3 | Open file location |
| 40 | 3.3 | Preserve DataGrid state on refresh |
| 55 | 3.3 | Rename a shortcut file |
| 75 | 3.3 | Type-ahead selection in BatManager |
| 63 | 3.3 | "Navigate on desktop" for step 1 |
| 84 | 4 | Managed `.lnk` parser |
| 85 | 4 | `src/`/`tests/` restructure |
| 98 | 4 | Avalonia rewrite |
| 100 | 4 | macOS platform implementation |
| 101 | 4 | macOS `PATH` management |
| 102 | 4 | Finder Quick Action |
| 103 | 4 | NSServices shim |
| 104 | 4 | Parity documentation |
| 106 | 4 | macOS `.pkg` + Homebrew |
| 107 | 3.1 / ongoing | Tech-debt checklist — items are pulled into whichever phase touches that code; see inline notes above |
| 12 | — | Superseded by this backlog; safe to close |

## Open question

None remaining. #87 (the corpus) was the one blocking dependency in Phase 3.0 and is now resolved — no PC needed; see the note in the Phase 3.0 table. Phase 3.0 can proceed straight to #88.
