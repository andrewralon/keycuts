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
- [ ] Unit tests cover URL, scheme-less URL, `.url`, `.lnk` (skip if WScript is unavailable), CLSID, existing folder,
      existing file, the hosts file, a missing path, a quoted path, a whitespace-padded path, an env-var path, and an empty string.
- [ ] No code path throws for user input.
- [ ] `Shortcut.GetShortcutTypeFromDestination` keeps its current behavior for the CLI. Either refactor it to delegate
      to the resolver without changing its output, or leave it untouched.

## Out of scope
UI. Creating files (KC-04).
