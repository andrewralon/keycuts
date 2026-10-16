# keycuts
keyboard shortcuts for nerds.... Open any file/folder/URL with Win+R and the shortcut name.

keycuts is a tool for creating simple scripts to open **anything** on your computer using a nickname.

How do you launch a keycut?
* Type "Win+R", the shortcut name, and Enter.

Examples:
* "npp" to open Notepad++
* "rb" to open Recycle Bin
* "gmail" to open a new tab for email
* "timesheet" open a new tab for timesheet
* "db" to open Dropbox folder
* "ss" to open screenshots folder

Create keycuts several ways:
* GUI -- open the app, drag in the destination file, and name it
* Right click menu -- opens the app with the destination filled in
* CLI -- for all the hard-core nerds and brogrammers
* Common -- for forking your own implementation

## Development

Requires the .NET SDK 10. Run from the repository root:

* Format check: `dotnet format keycuts.sln --verify-no-changes`
* Build (also the type-check): `dotnet build keycuts.sln`
* Test: `dotnet test keycuts.sln` (xUnit project `keycuts.Tests`; tests must use temp folders, never the real output folder or the registry)

Known baselines: the build has 27 CA1416 (Windows-only API) warnings (count with `--no-incremental`), so `-warnaserror`
isn't used. The format check reports 28 pre-existing whitespace errors in 7 files; new code must not add to them.
