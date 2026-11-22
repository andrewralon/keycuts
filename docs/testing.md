# Testing keycuts: how, and what went wrong before

Commands are in CLAUDE.md. This file is the lessons: read it before writing a flaky-looking test or driving the real app.

## The layers

| Layer | Where | Runs on |
|---|---|---|
| Logic, parsing, emitters, snapshots | `keycuts.Common.Tests` (Characterization snapshots) | Windows, macOS, CI |
| Views and view models, real keys and mouse | `keycuts.App.Tests` (Avalonia.Headless, Skia) | Windows, macOS, CI |
| The real app: bundle, Finder/Safari drags, file panel, Dock, system settings | `tools/mac/check-app.sh` | A Mac, by hand (not CI) |
| Real login, VoiceOver, Narrator | by hand | the owner |

Tests never touch the real settings, registry, PATH, `~/Shortcuts`, network or Recycle Bin (CLAUDE.md, Gotchas).

## Headless tests

- **A `Task.Run` or `Task.Delay` in the code under test means one `RunJobs()` is not enough.** The continuation comes back to the UI thread when the
  pool finishes, which on a loaded runner can be later than the assert. `CreatorViewModel.CreateAsync` checks the name on the pool, and
  `Creator_EnterInTheName_Creates…` asserted after one pass: it failed on CI's macOS runner (`Expected ["tmp"], Actual []`). Pump until the thing has
  happened (`ViewTests.PumpUntil`, 5 s cap) instead of sleeping or asserting once. Sites with this hop: the creator, the edit form and the Manager's
  rename check (`Task.Run(() => check…)`).
- **Prove a race instead of guessing.** Run the test many times under load (it didn't fail in 25 runs), then make the slow part slow on purpose: a
  300 ms fake name check reproduced the CI failure at once. Check that the fix passes with the delay, then put the delay in every similar hop and run the
  whole suite to find other tests with the same flaw (none did).
- **A test that makes Avalonia objects must be `[AvaloniaFact]`, even if it looks like plain logic.** Each Avalonia test resets
  `Dispatcher.UIThread` and builds a new app; `Dispatcher.UIThread` belongs to whichever thread touches it first after the reset. A plain `[Fact]`
  runs on a pool thread in parallel, and anything that creates or sets an Avalonia object (`NativeMenuItem`, a control, a brush resource) touches it,
  so it can take it in that gap and the *next* Avalonia test fails at setup: `Test Case Cleanup Failure … The calling thread cannot access this object
  because a different thread owns it`, in `EnsureIsolatedApplication`, about 1 ms in, on a random test, passing on rerun. `MacMenusTests` did this
  (#284), which showed up as CI flakes on Windows and macOS. How it was found: the failure reproduced locally under CPU load (4 to 9 runs in 30),
  disappeared with `xUnit.ParallelizeTestCollections=false` (0 in 25), then bisecting the plain test classes by filter (20 runs per half) narrowed it to
  one class (12 of 20 with it alone, 0 of 20 without). With the fix: 0 in 30. A probe test that spins a pool thread on `Dispatcher.UIThread` makes the next
  tests fail the same way, which is a quick way to confirm the mechanism.
- Faking: `ThemeManager.ReadMacIncreaseContrast` is off by default so a test never reads the machine's setting; fakes for `ILoginItem`, `IRunBoxShortcut`,
  `ISettingsStore`. A seam that reads the machine defaults to "off".

## Driving the real app on a Mac (`tools/mac/`)

`tools/mac/README.md` says how to run it and what it changes. Lessons from building it:

- **Use a bundle.** `new-test-bundle.sh` copies the Debug build into a `.app` with its own identifier (`com.teamralon.keycuts.uitest`) and `LSEnvironment`
  (HOME, `KEYCUTS_OUTPUT_FOLDER`, `DOTNET_ROOT`), ad-hoc signed. LaunchServices ignores shell environment variables, so the plist carries them.
  An unbundled binary behaves differently (the app menu read "About Avalonia"; `SMAppService` registered the bare binary as a real login item).
  Never toggle Open at login outside the sandbox bundle without turning it off.
- **Accessibility (System Events):** use argv-style scripts that read each attribute in its own `try` (`read.applescript`, `find.applescript`);
  a combined read swallows errors and returns nothing. `set frontmost` first. Finder's own tree comes back empty, so Finder rows are found by position
  in list view (sorted: folders first, then by name). Click by screen position (`cliclick c:x,y`), not AX `click`, for menus that must open as a user's
  click would. Menu bar items and the tray menu are reachable through AX.
- **Drags:** `cliclick dd`/`dm`/`du`. Finder drags work with quick moves. Safari's drag stalls unless the pointer waits ~1.5 s after the lift
  animation starts; even then retry (up to 3). Windows of the other apps cover the drop target, so `desktop.sh` hides every other app and minimizes
  other Finder windows and restores them from a state file on exit.
- **Reading results without screenshots:** read the Target field through AX; for rendering, capture the window rectangle and count pixels
  (`black_pixels` for high contrast: its text is exactly black, so 0 → thousands → 0). Hash the Dock tile for the Dock icon. Sample at the window's
  rectangle from AX, never at fixed screen points.
- **System settings:** `defaults write com.apple.universalaccess …` is refused (protected), so Increase contrast is flipped in System Settings with a click at
  a fixed point on macOS 26, and the script always switches it back (an exit trap). Say so before doing it; the owner asked why it was changing.
- **A harness is only trusted once it fails on the bug.** Run the checks against the commit without the fix: contrast and Dock failed on `develop`, passed
  with the fixes.
- **The owner may touch the machine while it runs.** The banner (top of the screen) says what is running and has Pause and Stop. A failure seen while
  someone could have been clicking is not evidence: repeat once, or confirm with a non-UI probe, before reporting it as a bug.

- **A login start can't be faked from a shell (issue #294).** Measured on macOS 26: when the app decides, there is no launch Apple event for any
  start (a manual `open` included: the check runs before AppKit delivers it), launchd is the parent of every app LaunchServices starts, and the app is
  active in a manual and in a detached start alike. What tells a login item apart is time: it starts within moments of the login session, so the rule
  is "registered as a login item, parent launchd, and the user's session (the age of the Dock, from `ps`) under 3 minutes" (`LoginLaunch`). That
  rule is unit-tested; a real login (log out and in, with Open at login on) is the only end-to-end check. A cold `open` hours after logging in must
  show the window, and the Dock tile must show the keycap icon (checked). Avalonia's lifetime shows `desktop.MainWindow` itself when it starts, which is
  why that window is assigned only after it first opens in the login case; skipping the `Show()` call alone left the window up with the Dock icon
  hidden. The Dock ignores an icon set while the app was hidden from it, so `MacDockIcon.Refresh()` runs each time the app returns to the Dock.

## Things this found (so they aren't "fixed" again by accident)

- Avalonia 12.1.6 reports `ContrastPreference.NoPreference` on macOS with Increase contrast on, at startup too (probe in `ThemeManager.DetectPalette`
  printed `theme=Light contrast=NoPreference`). The app reads `NSWorkspace.accessibilityDisplayShouldIncreaseContrast` itself (`MacAccessibility`) and re-reads
  it every 2 s. `NSApplication.effectiveAppearance.bestMatch(…)` does report the high-contrast variant to a plain AppKit app; Avalonia just doesn't pass it on.
- `Window.Icon` does not reach the Dock on macOS (generic icon whatever was picked): `MacDockIcon` sets `NSApplication.applicationIconImage`.
- Avalonia builds the app menu at startup and ignores a menu set afterwards: `MacMenus.FillAppMenu` edits the existing one in place.
- Finder aliases are "not supported" on purpose (docs/macos-port.md); a `.webloc` resolves to its URL.

## CI

- `build` (Windows), `format` (Linux) and `macos` are required on `develop` and `main`. A `changes` job skips them when only `docs/`, `README.md`,
  `CLAUDE.md` or `gameplan.md` changed; a skipped job counts as passed (never `paths-ignore`, which leaves the checks pending).
- A push to `develop` re-runs CI on the merge; read the result of the run for the commit you pushed (`gh run list --branch develop`), because an
  older green run is not yours.
- `dotnet format` takes no `-p:` options; set `EnableWindowsTargeting=true` as an environment variable on macOS.
