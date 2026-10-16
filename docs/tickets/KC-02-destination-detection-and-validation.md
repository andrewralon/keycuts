# KC-02 — Destination detection and validation service

**Area:** keycuts.Common · **Depends on:** 01 · **Blocks:** 04, 10

## Why
The creator must detect the destination type right after a drop, paste, or Browse, show the matching icon, and
report "invalid or inaccessible destination" inline. Today `Shortcut.GetShortcutTypeFromDestination` **throws**
for missing paths, because `File.GetAttributes` throws `FileNotFoundException`/`DirectoryNotFoundException`. It also
doesn't report *why* a destination is invalid, and it may block on slow or unreachable UNC paths.

## Scope
Add a non-throwing resolver in `keycuts.Common`, for example `DestinationResolver`:

```csharp
DestinationResult Resolve(string rawInput);              // pure + fast checks
Task<DestinationResult> ResolveAsync(string rawInput, CancellationToken ct); // with filesystem access + timeout
```

`DestinationResult` contains:
- `IsValid`
- `Kind`: `Url | File | Folder | Command`, the 4 display kinds (mapping below)
- `RawType`: the existing `ShortcutType`
- `Destination`: the normalized value
- `DisplayText`: what the creator shows
- `Error`: `None | Empty | NotFound | AccessDenied | Unsupported | Timeout`

Behavior:
- Trim whitespace, surrounding quotes, and trailing newlines. Clipboard and browser drags often include these.
- Reuse the existing rules unchanged: URL (including scheme-less URLs such as `amazon.com`), `.url` files (read `URL=`),
  `.lnk` files (resolve the target), CLSID keys, the hosts file, folders, and files.
- Accept `file:///C:/...` URIs as local paths.
- Expand environment variables, for example `%USERPROFILE%\Downloads`.
- Missing path → `NotFound`. `UnauthorizedAccessException` → `AccessDenied`. Anything else → `Unsupported`.
  None of these may throw to the caller.
- Filesystem checks run off the UI thread with a short timeout (~2s) so an unreachable network path can't freeze the UI.

### Kind mapping (single source of truth, reused by badges in KC-07)
| `ShortcutType` | Display kind |
| --- | --- |
| `Url` | URL |
| `Folder`, `CLSIDKey` | Folder |
| `File`, `HostsFile` | File |
| `Command` | Command |
| `Unknown` | not valid for creation; in the manager, show as Command with the raw text |

## Acceptance criteria
- [x] Unit tests cover URL, scheme-less URL, `.url`, `.lnk` (skip if WScript is unavailable), CLSID, existing folder,
      existing file, the hosts file, a missing path, a quoted path, a whitespace-padded path, an env-var path, and an empty string.
- [x] No code path throws for user input.
- [x] `Shortcut.GetShortcutTypeFromDestination` keeps its current behavior for the CLI. Either refactor it to delegate
      to the resolver without changing its output, or leave it untouched.

## Out of scope
UI. Creating files (KC-04).


## Implementation notes

**Files added:** `keycuts.Common/DestinationResolver.cs`, `DestinationResult.cs` (also holds `DestinationError`), `KeycutKind.cs` (enum + `KeycutKinds.FromShortcutType` mapping), `keycuts.Tests/DestinationResolverTests.cs`. `Shortcut.cs` is untouched, so CLI behavior is unchanged.

**Decisions / deviations**
- `Resolve(string)` is fully synchronous and does the filesystem checks (it is not "pure"); it can block on unreachable UNC paths. `ResolveAsync(raw, ct)` runs it on a worker thread with a 2s timeout (`DefaultTimeout`; an overload takes a timeout) and returns `Error = Timeout`. UI code must use the async one. Cancellation via `ct` throws `OperationCanceledException` (not user input).
- Scheme-less URLs (`amazon.com`) are only accepted when nothing exists on disk at that path, the text has no spaces/backslashes/`%`, and the TLD isn't a common file extension (so a missing `notes.txt` is NotFound, not a URL). The existing `Shortcut.IsValidUrl` never actually matched these (its `uriResult` is null), so this is new logic; normalized via `Uri.AbsoluteUri` (`http://amazon.com/`).
- Invalid results use `RawType = Unknown` and `Kind = Command` (mapping table), with `Destination`/`DisplayText` = cleaned input.
- A `.url` file without a `URL=` line falls back to File, as the existing code does. `.lnk` resolves to its target via `Shortcut.IsLink`.
- Quotes (`"` or `'`) and whitespace/newlines are stripped; env vars expanded for paths only (not URLs).

**Verification:** build 27 warnings (baseline); 36 tests pass; `dotnet format` still reports only the 37 pre-existing WHITESPACE errors in the 9 old files, none in new files.

**Manual QA still needed:** real clipboard/browser/Explorer drag input (e.g. URLs with trailing newline); a genuinely unreachable UNC path (e.g. `\\10.255.255.1\share`) to confirm the 2s timeout in the UI (KC-10).
