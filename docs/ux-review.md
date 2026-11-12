# keycuts UX review

Reviewed 2026-11-11 on `develop` at `9de7609`, from the README, `gameplan.md`, the Creator and Manager views and view models, the `.bat` emitter, and the open issues. The app wasn't run for the review, so items marked **check** need confirming on a real machine.

This is a living note. Each item below has a GitHub issue with the background and implementation hints; this file keeps the overview, and related notes and decisions as they come up.

## Summary

The idea is good and the core is solid: one nickname, Win+R, no background process, and plain `.bat` files as the only storage. That last part is a real selling point. Keycuts can be copied, backed up, checked into git and edited by hand, and they keep working with keycuts uninstalled.

The implementation is careful:

- errors show inline, next to the field they belong to;
- the name check mirrors how Win+R actually looks names up (#153);
- every Manager state has a design: empty, loading, no results, load error;
- high contrast, keyboard navigation and screen-reader names are handled;
- nothing needs admin rights.

Most of what's left is in two places: the moment a keycut runs, and the edges of the Manager.

**Top picks**, in order:

1. Check the console flash on launch (#160).
2. Recycle Bin plus Undo for delete (#179).
3. Name suggestions in the Creator (#178).
4. A description field and an Edit form (#162, #180).
5. Arguments and URL templates (#172, #181).
6. winget (#182).
7. A license, which unblocks free signing (#169).

The first four make the existing workflow feel finished. The last three are what would make the app worth recommending to other people.

## Issues

| Item | Issue |
|---|---|
| Improvements 1: console flash on launch | #160 |
| Improvements 2: delete to the Recycle Bin, with Undo | #179 |
| Improvements 3: description field (Creator and CLI) | #162 |
| Improvements 3: Edit form in the Manager | #180 |
| Improvements 4: name suggestions | #178 |
| Improvements 5: close after a right-click create | #163 |
| Improvements 5: "Try it" after a create | #164 |
| Improvements 6: Shift hint | #165 |
| Improvements 7: name check across the Manager | #166 |
| Improvements 8: multi-select | #161 |
| Improvements 8: sort by Destination, Modified column | #167 |
| Improvements 9: "keycut" wording, header copy | #168 |
| Improvements 10: license | #169 (signing is #118) |
| Improvements 11: README screenshots and stale note | #170 |
| Improvements 12: update notice | #171 |
| Features 1: argument pass-through | #172 |
| Features 1: URL templates | #181 |
| Features 2: winget and Scoop | #182 |
| Features 3: CLI verbs | #173 |
| Features 4: open with, run as admin, start in | #174 |
| Features 5: import | #183 |
| Features 6: group keycuts | #184 |
| Features 7: PowerToys Command Palette | #175 |
| Features 7: top-level Windows 11 menu | #176 |
| Features 8: quick-create hotkey | #177 |

## Improvements

### High impact

#### 1. Check whether a console window flashes on every launch

Issue: #160

Each keycut is a `.bat`, so Win+R starts `cmd.exe`, which runs `START` and then `EXIT` ([Runner.cs](../keycuts.Common/Runner.cs), `BuildCommand`). That usually shows a brief console window. On Windows 11 with Windows Terminal as the default terminal, it can open a Terminal window instead, which is more noticeable.

This happens every time the product is used, so it's the most important thing to **check**. If it does flash, investigate other options briefly before committing to one:

- registering each name under `HKCU\Software\Microsoft\Windows\CurrentVersion\App Paths`, which Win+R already searches (`NameConflicts` knows about it);
- `.lnk` files plus `.LNK` in a user-level `PATHEXT`;
- a tiny windowless launcher that the `.bat` hands off to.

Each has tradeoffs against "the `.bat` is the whole product," and any change to the launch line falls under the sequencing rule in `CLAUDE.md`.

#### 2. Delete has no confirmation and no undo

Issue: #179

Delete calls `File.Delete` ([KeycutRepository.cs](../keycuts.Common/KeycutRepository.cs), `DeleteAsync`). Current practice prefers undo over a confirmation dialog: move the file to the Recycle Bin and put **Undo** in the Manager's status bar for a few seconds. It's cheap, and it's the only action in the app that loses data.

#### 3. There's no way to edit a keycut in the app

Issues: #162 (description field), #180 (Edit form)

**Edit** opens the raw `.bat` in Notepad, and descriptions can only be added that way: the Creator has no description field, and the CLI has no description option. Yet the Manager displays and searches descriptions. Two steps:

- add an optional description field to the Creator, and a `--description` option to the CLI;
- add an **Edit** form for the destination, either limited to files the emitter wrote unchanged, or with a warning that hand edits will be overwritten. (Rename deliberately never re-emits a file; an Edit form would, so the hand-edit case needs a decision.)

#### 4. Suggest a name from the destination

Issue: #178

For example:

| Destination | Suggested name |
|---|---|
| `C:\Program Files\Notepad++\notepad++.exe` | `notepad++` |
| `https://github.com/andrewralon/keycuts` | `github` |
| `C:\Users\me\Dropbox` | `dropbox` |

Pre-select the suggestion so Enter accepts it and typing replaces it. That turns "drop, think of a name, Enter" into "drop, Enter." Keep the rules predictable rather than clever; abbreviations like `npp` are out of scope. The current placeholder, `e.g. l-vicki`, is cryptic and reads as personal.

#### 5. Finish the right-click flow

Issues: #163 (close after create), #164 ("Try it")

When the app opens from "keycut this!", the job ends once the keycut is created. Right now the Creator resets after 2 seconds (`CreatorViewModel.SuccessDuration`) and the window stays open. Close the window after a successful create on this path.

For the normal path, the success message could teach the habit: "Created gmail. Try it: Win+R → `gmail`", with a **Run it** link.

### Medium

#### 6. Hidden gesture

Issue: #165

Holding Shift to flip **Use environment variables in paths** can't be discovered. Add a hint to the "Saved as" line, such as "hold Shift to save the full path".

#### 7. Use the name check across the whole Manager

Issue: #166

`NameConflicts` already exists, so the Manager could show "shadowed by `C:\…\code.exe`" badges on rows, plus a filter for missing or shadowed keycuts. Today a missing destination is flagged, but you can't filter by it or act on it.

#### 8. Manager basics

Issues: #161 (multi-select), #167 (sort by Destination, Modified column)

- Only one row can be selected (`SelectionMode="Single"`), so there's no bulk delete. Group keycuts (#184) need multi-select too.
- The Destination column can't be sorted.
- There's no Modified column.

#### 9. Inconsistent words

Issue: #168

The UI mixes "shortcut" and "keycut": the **Shortcut** column header and the "Search shortcuts" accessibility name sit next to "Your keycuts." Use "keycut" for the user's items everywhere; on Windows, "shortcut" means a `.lnk`.

The marketing lines ("Launch anything instantly", "Scan, search, and manage every shortcut in one place") also take vertical space in a small utility window. Consider dropping them or shortening them to the heading alone.

#### 10. Trust at install time

Issues: #169 (license), #118 (signing)

SmartScreen's warning on an unsigned MSI is the first thing a new user sees (#118). SignPath Foundation is free but needs an OSI license, and the repo has none yet. Adding a license is cheap, worth doing for an open-source project anyway, and unblocks free signing.

#### 11. README fixes

Issue: #170

- It has no screenshots or GIF. For a GitHub project, that's the biggest single factor in whether people try it.
- The Development section is stale: it says the build has 27 CA1416 warnings, but the build has had 0 for a while (see `CLAUDE.md`).

#### 12. No update notice

Issue: #171

Users on old versions never learn there's a new one. Options: a lightweight check against GitHub Releases at startup (opt-out in Settings), or let winget handle updates (Features 2).

Decided (#171): the startup check, notify only. The app never downloads or installs; Download opens the release page. The last release a check saw is stored with the check time, so the footer line shows on every start rather than only on the day a request is made. Dismissing hides the line until a newer release. An MSI property to turn the check off for managed installs is left until someone asks.

## Features that fit where the app is going

Ordered by fit and value.

### 1. Keycuts that take arguments

Issues: #172 (pass-through), #181 (URL templates)

Win+R already passes extra words to the `.bat`.

- **URL template:** `https://google.com/search?q={query}`, so `g cats` searches for cats. This is browser keyword search, but system-wide.
- **Pass-through:** an option that adds `%*` to a File keycut, so `npp notes.txt` opens that file in Notepad++. People already add `%*` by hand, which is why rename preserves hand-edited launch lines.
- **Catch:** URL-encoding in batch is awkward, so the template would probably go through a helper (`keycuts open-url ... %*`).

This is the biggest power-user feature available, and it's squarely on-brand for "keyboard shortcuts for nerds."

### 2. winget and Scoop manifests

Issue: #182

This is how the target audience installs tools, and it brings updates for free. It's mostly a packaging task now that releases are automated.

### 3. CLI verbs to match the Manager

Issue: #173

`keycuts list [--json]`, `keycuts rm`, `keycuts rename`, `keycuts open`. That makes the keycuts folder scriptable and easy to manage from dotfiles.

### 4. Open with, run as admin, and working folder in the GUI

Issue: #174

The `.bat` format already supports `OpenWithApp`, but the GUI can't set it. Running as administrator and a start-in folder have no support yet.

### 5. Import

Issue: #183

- browser bookmarks, picking which ones and suggesting names;
- Start menu and desktop `.lnk` files.

This solves the empty-folder problem on day one, better than the welcome panel can. (A folder of existing `.bat` files isn't an import: files in the keycuts folder already are keycuts.)

### 6. Group keycuts: one keycut that opens several others

Issue: #184

A group is a keycut whose only job is to open other keycuts at the same time. `cals` opens `gcal`, `cal` and `cal2`, so Win+R → `cals` brings up every calendar.

**Why.** People already write these scripts by hand; the maintainer's `cals.bat` is three `START <name>` lines. Hand-made groups have problems that a built-in type would fix:

- **The Manager shows them wrong.** `cals.bat` was copied from `cal.bat` and still has its REM lines, so the parser (where REM lines win) reads it as a URL keycut that opens one calendar.
- **`START gcal` looks the name up on PATH**, so a program with the same name earlier on PATH runs instead. The name warnings (#153) never see these lines.
- **Nothing notices when a member is renamed or deleted.** The group fails silently, and its console window closes before anyone can read the error.

**Decisions**, proposed:

1. **Members are keycut names, not copies of their destinations.** Change where `gcal` points and every group that uses it follows. Raw URLs as members (like a hand-made script that opens four related URLs) could come later. For v1, they'd each need their own keycut.
2. **Each member runs as `START "" /B cmd /C "%~dp0<name>.bat"`.**
   - `%~dp0` is the group's own folder, so the group always runs the sibling keycut, never something else on PATH. It also keeps working if the keycuts folder moves.
   - `CALL` doesn't work: every keycut ends with `EXIT`, which closes the whole group after the first member. Tested on 2026-11-11: `CALL` ran only the first member, while `START "" /B cmd /C` ran all of them.
   - `/B` gives each member no console window of its own.
3. **The file format: one `SET` line holds the members, and a loop runs them.** Prototyped and tested locally on 2026-11-11:

   ```bat
   @ECHO OFF
   REM keycuts 1.1.1
   REM <shortcut>C:\Shortcuts\cals.bat</shortcut>
   REM <type>group</type>
   REM <description>All calendars</description>

   REM Opens these keycuts, in order. Separate names with spaces or commas,
   REM and put a name that has a space in quotes: "my cal".
   SET KEYCUTS=gcal, cal, cal2

   REM Stops a group that opens itself (directly or through another group) after 5 levels.
   SET /A KEYCUTS_DEPTH+=1
   IF %KEYCUTS_DEPTH% GTR 5 EXIT

   REM %~dp0 is this file's folder, so each name runs the keycut beside it, not something else on PATH.
   FOR %%K IN (%KEYCUTS%) DO START "" /B cmd /C "%~dp0%%~K.bat"
   EXIT
   ```

   - **Easy to edit by hand.** Adding or removing a member means changing one line of plain names, and `%~dp0` appears only once.
   - **One source of truth.** The `SET KEYCUTS=` line is both what runs and what the parser reads, so there is no `REM <destination>` line that can drift from the launch lines (the `cals.bat` problem). The parser change: for `group`, read the members from that line instead of requiring `REM <destination>`.
   - **Tested:** commas and spaces both separate names; `%%~K` strips quotes, so a quoted name with a space works; and a group that lists itself stopped after 6 runs.
   - An earlier draft listed the members on a `REM <destination>gcal | cal | cal2</destination>` line, with one launch line per member. That works, and cmd ignores `|` on a `REM` line, but it repeats each name twice.
   - This is a format change, so it follows the sequencing rule: one change per commit, with new corpus files and characterization snapshots.
4. **Cycles must be refused.** A group that contains itself, or `A → B → A`, would launch processes without end. Check for this when a group is created, edited or renamed. As a runtime guard, every child `cmd` inherits the environment, so a depth counter stops a cycle the creator check missed: `SET /A KEYCUTS_DEPTH+=1` and `IF %KEYCUTS_DEPTH% GTR 5 EXIT`.
5. **Renames and deletes.** When a member is renamed, offer to update the groups that use it. That means re-emitting the group files, which is fine because keycuts generated them. When a member is deleted, say which groups use it; with Undo (#179) this needs no confirmation dialog. In the Manager, a group with a missing member gets flagged, the same way a missing destination is.
6. **How to create one:**
   - **Manager:** select several rows (#161), then **Group…**, then type a name. This is the natural path.
   - **CLI:** `keycuts group cals gcal cal cal2`, next to the other verbs (#173).
   - **Creator:** typing `gcal + cal + cal2` as the destination could work too, but it's less discoverable than the Manager.
7. **Out of scope for v1:** passing arguments to members, delays between launches, and raw destinations as members.

### 7. Show up where Windows users search now

Issues: #175 (Command Palette), #176 (Windows 11 menu)

- A PowerToys Command Palette or Run extension that lists keycuts with fuzzy search.
- The Windows 11 top-level context-menu entry, which needs a packaged `IExplorerCommand` handler (see `CLAUDE.md`). Sparse packages must be signed, so it depends on #118.

### 8. Quick create from anywhere (tradeoff)

Issue: #177

A global hotkey that opens the Creator with the clipboard or the current Explorer selection already filled in. It needs a resident tray process, which goes against the current "nothing running in the background" design. Try first without one: a Start menu shortcut's **Shortcut key** that opens the Creator, which reads the clipboard when it opens empty.
