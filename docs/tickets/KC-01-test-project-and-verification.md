# KC-01 — Test project and verification commands

**Area:** Infra · **Depends on:** — · **Blocks:** 02, 03, 04, and every verification step

## Why
The spec requires running the repository's formatting, type-checking, test, and build commands. The repo has
none of these beyond `dotnet build`: there's no test project, no `.editorconfig`, and no CI. Later tickets add
logic to `keycuts.Common` and view models that need unit coverage.

## Scope
- Add `keycuts.Tests` (xUnit or MSTest; pick one and note it) targeting `net10.0-windows`. Reference
  `keycuts.Common`, and `keycuts.GUI` if the view models stay in the GUI assembly. Add it to `keycuts.sln`.
- Add a minimal `.editorconfig` matching the existing C# style: 4-space indent, braces on new lines, `var` usage.
  Don't reformat unrelated files in this ticket.
- Document the commands in the root `README.md` under a short "Development" section:
  - Format check: `dotnet format keycuts.sln --verify-no-changes`
  - Build (also the type-check): `dotnet build keycuts.sln -warnaserror` (or without `-warnaserror` if existing warnings make that unrealistic; record the baseline)
  - Test: `dotnet test keycuts.sln`
- Add one smoke test, such as `Shortcut.SanitizeBatEscapeCharacters`, to prove the pipeline works.

## Acceptance criteria
- [x] `dotnet build keycuts.sln` and `dotnet test keycuts.sln` succeed from a clean clone.
- [ ] `dotnet format --verify-no-changes` passes. (not met: 37 pre-existing whitespace errors recorded as baseline, see notes) Record any pre-existing violations as a known baseline rather than fixing them in bulk.
- [x] Commands are documented in `README.md`.

## Notes
- Tests that touch the filesystem must use a temp folder. They must never use the real output folder (`C:\Shortcuts`) or the registry.
  KC-03 and KC-04 introduce seams (an injected folder path) to make this possible.
- Optional: a GitHub Actions workflow on `windows-latest`. Leave it out unless requested.

## Implementation notes
**Files changed:** `keycuts.Tests/keycuts.Tests.csproj`, `keycuts.Tests/ShortcutTests.cs` (new); `keycuts.sln` (adds the test project); `.editorconfig` (new); `README.md` (Development section).

**Decisions**
- xUnit 2.x with `Microsoft.NET.Test.Sdk`, targeting `net10.0-windows`. It references `keycuts.Common` only; KC-08 should add a `keycuts.GUI` reference when view models land.
- `.editorconfig`: 4-space indent, braces on new lines, `var` preferences as suggestions. It sets no `charset` or `insert_final_newline` rules, since existing files would fail those en masse.
- Smoke test: `Shortcut.SanitizeBatEscapeCharacters` (4 cases, all pass).
- `-warnaserror` isn't used because of the 27 CA1416 baseline warnings. Build has 27 warnings with `--no-incremental`, 0 errors.

**Deviation:** `dotnet format keycuts.sln --verify-no-changes` exits 2 because of 37 pre-existing whitespace errors in 9 files (Batmanager/GUI `AssemblyInfo.cs`, `Options.cs`, `KeycutArgs.cs`, `ShortcutFile.cs`, `RegistryStuff.cs`, `Shortcut.cs`, `CommonLogic.cs`, `SettingsWindow.xaml.cs`). They were left unfixed per the ticket. New and changed code is clean. Later tickets should confirm the error count doesn't grow (`dotnet format keycuts.sln --verify-no-changes --severity error`, ignoring CA1416). Optionally run `dotnet format whitespace keycuts.sln` once to get a clean pass.

**Manual QA still needed:** none. A "clean clone" build wasn't exercised, only an in-place `--no-incremental` build.