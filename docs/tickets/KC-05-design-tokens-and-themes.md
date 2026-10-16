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
- [ ] Every interactive style has visible hover, pressed, disabled, and keyboard-focus states in both themes.
- [ ] Text and background token pairs meet WCAG AA (4.5:1 body, 3:1 large text and UI boundaries) in both themes. Spot-check with a contrast tool.
- [ ] No hard-coded colors in views; everything goes through tokens.

## Out of scope
An in-app theme toggle. Restyling the Settings window beyond what implicit styles give it for free.
