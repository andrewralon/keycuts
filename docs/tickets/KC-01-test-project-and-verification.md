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
- [ ] `dotnet build keycuts.sln` and `dotnet test keycuts.sln` succeed from a clean clone.
- [ ] `dotnet format --verify-no-changes` passes. Record any pre-existing violations as a known baseline rather than fixing them in bulk.
- [ ] Commands are documented in `README.md`.

## Notes
- Tests that touch the filesystem must use a temp folder. They must never use the real output folder (`C:\Shortcuts`) or the registry.
  KC-03 and KC-04 introduce seams (an injected folder path) to make this possible.
- Optional: a GitHub Actions workflow on `windows-latest`. Leave it out unless requested.
