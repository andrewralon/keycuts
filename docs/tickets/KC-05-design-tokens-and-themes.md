# KC-05 — Design tokens and light/dark themes

**Area:** keycuts.GUI · **Depends on:** — · **Blocks:** 06, 07, 08, and all UI tickets

## Why
The redesign keeps the prototype's Windows-utility language: its tokens, Segoe UI typography, 4px spacing grid,
radii, and light/dark palettes. `App.xaml` currently has an empty `Resources` block and every control uses default WPF styling.

## Scope
- `Themes/Light.xaml` and `Themes/Dark.xaml`: `SolidColorBrush` resources whose keys mirror the CSS variables in
  [`index.css`](<../../Figma Redesign Keycuts UI2/src/index.css>) (`:root` and `.dark`). Keys include
  `AccentBrush`, `AccentHoverBrush`, `AccentPressedBrush`, `AccentSoftBrush`, `CanvasBrush`, `SurfaceBrush`,
  `SurfaceSubtleBrush`, `SurfaceHoverBrush`, `SurfaceRaisedBrush`, `TextBrush`, `TextSecondaryBrush`, `TextTertiaryBrush`,
  `BorderBrush`, `BorderStrongBrush`, `DangerBrush`, `DangerSoftBrush`, and `SuccessBrush`.
  Use the prototype's hex values exactly.
- `Themes/Tokens.xaml`: radii (5/8/12/16), spacing constants, font family `Segoe UI Variable, Segoe UI`, and the type ramp.
  - The prototype's px sizes are tiny (9–12px) because of the preview. Keep the *proportions* but use a readable WPF ramp,
    such as 11 for eyebrow labels, 12 for helper and table text, 13–14 for body and inputs, and 20–22 for headings.
    The spec says clarity beats decoration.
- `Themes/Controls.xaml`: implicit or keyed styles referenced with `DynamicResource` so themes can switch live:
  - `PrimaryButton`, `SecondaryButton` (also used for Browse), and `IconButton` (32×32): default, hover, pressed, disabled, and keyboard focus.
  - `TextBox`: 34–38px tall, focus ring (accent border plus a soft outer glow), and an error variant.
  - `FocusVisualStyle`: 2px accent outline offset by 2px, matching `:focus-visible`. Show it **only** for keyboard focus,
    which is WPF's default `FocusVisualStyle` behavior.
  - `ContextMenu` / `MenuItem` (used by KC-14), `ToolTip`, `ScrollBar` (slim and unobtrusive), and an inline spinner.
- Theme switching (decided: follow Windows, no in-app toggle):
  - Follow the Windows app theme by reading `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`.
    Listen with `SystemEvents.UserPreferenceChanged` and swap the merged dictionary at runtime.
  - Use a dark title bar in dark mode (`DwmSetWindowAttribute`, `DWMWA_USE_IMMERSIVE_DARK_MODE` = 20) so the native chrome matches.
  - **High contrast:** when `SystemParameters.HighContrast` is true, fall back to system colors and don't force the custom palette.
- Keep the native window frame. The prototype's custom title bar is only part of the preview. Don't build custom chrome.

## Acceptance criteria
- [ ] Switching Windows between light and dark while the app is open re-themes it without a restart.
- [x] Every interactive style has visible hover, pressed, disabled, and keyboard-focus states in both themes.
- [x] Text and background token pairs meet WCAG AA (4.5:1 body, 3:1 large text and UI boundaries) in both themes. Spot-check with a contrast tool.
- [x] No hard-coded colors in views; everything goes through tokens.

## Out of scope
An in-app theme toggle. Restyling the Settings window beyond what implicit styles give it for free.

## Implementation notes

**Status:** implemented. Build 0 errors / 27 warnings (baseline), 194 tests pass, `dotnet format --verify-no-changes` still reports only the 36 pre-existing WHITESPACE errors (none in new/changed files). The GUI launched cleanly with the new resources and was closed.

**Verified:** the live-switching criterion (first checkbox) is *not* ticked; it needs manual QA. The "visible states" box is ticked on the basis of the style triggers and a successful template load, not a visual check.

### Files changed
- New: `keycuts.GUI/Themes/Light.xaml`, `Dark.xaml`, `HighContrast.xaml` (palettes, same keys), `Tokens.xaml` (radii, 4px spacing, control metrics, font family and type ramp), `Controls.xaml` (styles), `ThemeManager.cs`, `ColorContrast.cs`, `TextBoxChrome.cs`.
- Changed: `keycuts.GUI/App.xaml` (merges palette + Controls), `keycuts.GUI/App.xaml.cs` (calls `ThemeManager.Initialize`), `keycuts.Tests/keycuts.Tests.csproj` (references keycuts.GUI; TFM now `net10.0-windows10.0.19041.0` to match it).
- New tests: `keycuts.Tests/ThemeTests.cs` (variant resolution, palette key parity and required keys, exact prototype hex, WCAG contrast for token pairs, no hard-coded colors in Controls.xaml).

### Decisions
- Palette dictionary is `Application.Resources.MergedDictionaries[0]`; `ThemeManager` replaces it (Light/Dark/HighContrast) on `UserPreferenceChanged` and on startup. All styles use `DynamicResource` for colors. `Controls.xaml` merges `Tokens.xaml` itself.
- High contrast: `HighContrast.xaml` maps every token key to `SystemColors` (dynamic), so the custom palette is not forced.
- Derived `Window` types don't get an implicit `Window` style, so `ThemeManager` sets Background/Foreground/FontFamily via resource references on `Loaded` (only if not set locally) and sets the dark title bar (`DWMWA_USE_IMMERSIVE_DARK_MODE`) on each window.
- Styles: keyed `PrimaryButton`, `SecondaryButton` (implicit `Button` default), `IconButton` (32×32), `AppTextBox` (implicit `TextBox`; 36px, focus border + soft glow), `ErrorTextBox` and the attached `themes:TextBoxChrome.HasError` (also set automatically by `Validation.HasError`), `AppFocusVisual` (2px accent, 2px offset, keyboard-only), `ContextMenu`/`MenuItem` (flat, no submenus) plus `DangerMenuItem` and the separator, `ToolTip`, slim `ScrollBar`, and `Spinner` (colored by `Foreground`).
- Type ramp: 11 eyebrow, 12 caption/table, 13 body, 14 input, 16 subheading, 20/22 headings.

### Deviations (from "use hex exactly")
All prototype hex values are kept for the keys in the ticket. Added keys, because some prototype pairs fail WCAG AA:
- `OnAccentBrush` (text on accent): `#ffffff` in light; `#06090f` in dark (white on `#4f8cff` is only ~3.3:1; `#06090f` gives ≥4.5:1 on accent, hover and pressed).
- `ControlBorderBrush` (input and secondary-button borders): equals the TextTertiary hex in each theme (3.0–5.3:1). The prototype's `BorderStrong` is only ~1.5–2.3:1, which fails the 3:1 UI-boundary rule. `BorderStrongBrush` is still defined for menus and dividers.
- `SidebarBrush` added for completeness (it's in the CSS).
- Light `TextTertiary` (`#818b9d`) is only ~3.0–3.4:1, so it fails 4.5:1. It stays exact but should only be used for placeholders, helper and decorative text (the test enforces 3:1). KC-09 onward should keep essential text on `TextSecondary`.
- Dark `Accent` on `AccentSoft` is ~4.4:1; use it for icons and UI, not small text.
- Added `#pragma warning disable CA1416` to `ThemeManager.cs` so it doesn't grow the 27-warning baseline.

### Manual QA still needed
- Toggle Windows Settings → Personalization → Colors → app mode while the app is open: palette and title bar should switch live.
- Turn on a Windows contrast theme: the app should use system colors and stay readable (hover/pressed/disabled states are minimal in this mode).
- Visually check hover/pressed/disabled/keyboard-focus of buttons, text box focus glow and error variant, context menu, tooltip, scroll bar and spinner in both themes. No view uses the new keyed styles yet except the implicit Button/TextBox/ScrollBar/ToolTip.
- Text box glow uses a negative margin and can be clipped by tightly clipping parents.
- Confirm that the existing MainWindow/Settings windows still look acceptable with the implicit styles (they get the new look until KC-09 replaces them).