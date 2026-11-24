# keycuts style guide

How the keycuts app looks and reads: spacing, layout, type, color, control sizes, corner radii, icons, headings and accessibility, and writing. It covers every screen and dialog: the main window's tab bar and its three screens (the Creator, the Manager and Settings, with its icon picker), the Edit and Confirm dialogs, the shared footer and the welcome panel.

**It describes the app after #218**, which made Settings a third screen in the main window, behind folder tabs that replace the title bar; the [Tabbed window](#tabbed-window-218) section describes them. #218's mockup (`window-tabs-v5`) is a picture of the layout; where its sizes, colors or type differ from this guide, the guide wins.

Each rule names the token or shared style that carries it. A view uses those, never a literal number, font size or color. Where the app doesn't follow a rule yet, #219 lists the change (see [Tracking](#tracking)).

The reference sheet, [`mockups/styleguide.html`](mockups/styleguide.html) rendered to [`mockups/styleguide.png`](mockups/styleguide.png), shows the type ramp, spacing scale, control sizes, the three palettes and a sample dialog. Like the other mockups, it is a picture of the rules, not a screenshot.

**Sources.** When this guide doesn't settle something, follow the [Windows 11 / Fluent design guidance](https://learn.microsoft.com/windows/apps/design/) for layout and controls, the [Microsoft Writing Style Guide](https://learn.microsoft.com/style-guide/welcome/) for text, and [WCAG 2.2 AA](https://www.w3.org/TR/WCAG22/) for contrast and accessibility. If the app already does two different things and the choice changes how a screen looks or reads, ask the owner and record the answer under [Decisions](#decisions).

## Contents

1. [Tokens](#tokens)
2. [Spacing](#spacing)
3. [Layout](#layout)
4. [Type](#type)
5. [Color and contrast](#color-and-contrast)
6. [Control sizes](#control-sizes)
7. [Corner radii](#corner-radii)
8. [Icons](#icons)
9. [States: hover, focus, selection, disabled](#states-hover-focus-selection-disabled)
10. [Headings and accessibility](#headings-and-accessibility)
11. [Writing](#writing)
12. [Decisions](#decisions)
13. [Cleanups that need no decision](#cleanups-that-need-no-decision)
14. [Tracking](#tracking)

## Tokens

Tokens live in `keycuts.App/Themes/Tokens.axaml` (sizes, type) and the three palettes `Light.axaml`, `Dark.axaml` and `HighContrast.axaml` (brushes, with the same keys in each; `PaletteTests` compares them). Shared control themes live in `Themes/Controls.axaml`. (Until the Avalonia cutover, Phase 3.12 of [the macOS plan](macos-port.md), they were WPF's `keycuts.GUI/Themes/*.xaml`; the history below names those files.) Sizes are `StaticResource`; brushes are always `DynamicResource`, so the palette can change while the app runs.

Changes this guide makes to `Tokens.xaml` and the palettes (#219 makes them):

| Token | Today | After | Why |
|---|---|---|---|
| `ControlHeight` | 34 | **36** | One height for text boxes and text buttons ([decision](#decisions)) |
| `InputHeight` | 36 (unused; views set 38) | **36**, used | Same |
| `FontSizeEyebrow` | 11 | **removed** | 12 is the smallest text ([decision](#decisions)) |
| `FontSizeHeadingLarge` | 22 | **removed** | Every heading is 20 ([decision](#decisions)) |
| `AccentTextBrush` | none | **new**: Light `#2563eb`, Dark `#6a9eff`, high contrast `HotTrack` | Accent-colored text passes 4.5:1 on every surface ([Color](#color-and-contrast)) |
| `ScrimBrush` | `#6B0F172A` in `SettingsWindow.xaml` | **moves to the palettes**: Light `#6B0F172A`, Dark `#99000000`, high contrast `Transparent` | No color literals in views |
| `ShadowColor` | `#0F172A` literal on the icon picker | **new `Color`**: Light `#0F172A`, Dark `#000000`, high contrast `Transparent` | Same |
| `RadiusMdValue`, `RadiusLgValue` | none | **new `sys:Double`**: 8, 12 | `Rectangle.RadiusX/Y` (the dashed drop and empty-state outlines) take a number, not a `CornerRadius` |
| `MainWidth`, `ConfirmWidth`, `EditWidth` | literals 440 (`WindowLayout.CreatorWidth`, Confirm), 480 (Edit), 500 (Settings) | **new `sys:Double`**: `MainWidth` is what the tab bar needs (about 600; #218 measures it), for the Creator and Settings; `ConfirmWidth` 440; `EditWidth` 480 | [Window widths](#window-widths) |
| `RailHeight`, `TabHeight`, `CaptionButtonWidth`, `TabCut`, `TabSlant` | none | **new** (#218): 48, 40, 46, 8, 16 | [Tabbed window](#tabbed-window-218) |
| `Rail*`, `Tab*`, `Socket*`, `Caption*` brushes | none | **new** (#218), in all three palettes | The tab bar's colors come from the palettes, not literals |
| `TileSize` | none | **new**: 40 | The inline icon tile ([decision](#decisions)) |
| `TextTertiaryBrush` (Light) | `#818b9d`, 3.0-3.4:1 | **`#646e80`**, 4.54:1 or better on every light surface | Placeholders are text, and WCAG 1.4.3 covers them ([decision](#decisions)); `ControlBorderBrush` keeps `#818b9d` |

Kept as they are: `RadiusSm/Md/Lg/Xl` (5/8/12/16), `Space1`-`Space8` and `Inset1`-`Inset4`, `IconButtonSize` (32), `AppFontFamily` (Segoe UI Variable, Segoe UI), `FontSizeCaption` (12), `FontSizeBody` (13), `FontSizeInput` (14), `FontSizeSubheading` (16), `FontSizeHeading` (20). Add `Inset6` (24) for the outer padding, and `Thickness` tokens for the composite values used below (`PagePadding` 24,16,24,24, `DialogFooterPadding` 24,12, `PanelPadding` 16).

### In Avalonia

The app is Avalonia since the cutover (Phase 3.12 of [the macOS plan](macos-port.md)), with the tokens WPF had, the same keys and values, so every rule in this guide holds. Its files: `keycuts.App/Themes/Tokens.axaml`, `Light.axaml`, `Dark.axaml`, `HighContrast.axaml`, `Icons.axaml` and `Controls.axaml`. `StyleGuideTests` applies the rules to the `.axaml` files. Avalonia's unit is a device-independent pixel on both platforms (a point on macOS), so the numbers stay as they are.

| WPF resource | Avalonia resource | Tokens |
|---|---|---|
| `<CornerRadius>` | `<CornerRadius>` | `RadiusSm`, `RadiusMd`, `RadiusLg`, `RadiusXl` |
| `<sys:Double>` | `<x:Double>` | `Space*`, `Radius*Value`, `FontSize*`, `MainWidth`, `ConfirmWidth`, `EditWidth`, `ControlHeight`, `InputHeight`, `IconButtonSize`, `TileSize`, `RailHeight`, `TabHeight`, `TabCut`, `TabSlant`, `CaptionButtonWidth`, `ShadowOpacity` |
| `<Thickness>` | `<Thickness>` | `Inset*`, `PagePadding`, `DialogFooterPadding`, `PanelPadding`, `FooterBarPadding` |
| `<FontFamily>` | **A platform token**: an `OnPlatform` resource, `Segoe UI Variable, Segoe UI` on Windows and `$Default` (the system font) on macOS | `AppFontFamily` |
| `<SolidColorBrush>`, `<LinearGradientBrush>`, `<Color>` | The same elements, but a gradient's `StartPoint` and `EndPoint` are percentages (`0%,0%` to `0%,100%`): Avalonia reads `0,1` as pixels, where WPF read it relative to the shape | The palette brushes, `ShadowColor` |
| `<DrawingBrush>` with `Viewport` and `Viewbox` | `<DrawingBrush>` with `DestinationRect` and `SourceRect` | `RailTextureBrush`, `RailGripBrush`, `TabTextureBrush` |

How the rest carries over:

- **Resources**: sizes are `StaticResource` and brushes `DynamicResource`, as in WPF.
- **Palettes**: `Light.axaml` and `Dark.axaml` are the theme dictionaries for `ThemeVariant.Light` and `Dark`, so a `DynamicResource` brush follows the theme without code. High contrast is a third, custom `ThemeVariant`, chosen when the platform reports `ContrastPreference.High`. Avalonia has no `SystemColors`. So `ThemeManager` reads the Windows system colors itself (`GetSysColor`), and each key takes the same system color as in `HighContrast.xaml`. In `HighContrast.axaml` a brush names its system color with `themes:SystemColor.Name="WindowText"`, and `ThemeManager` sets those brushes when high contrast comes on or Windows reports a change. On macOS, **Increase contrast** picks the same high-contrast variant; macOS has no `GetSysColor`, so its system colors are fixed: black on white with the light appearance, white on black with the dark one, `#2563eb`/`#6a9eff` for `HotTrack` and `Highlight` (approved with the macOS mockups, 2026-11-20).
- **Shared styles**: a keyed WPF `Style` (`Style="{StaticResource PrimaryButton}"`) becomes a `ControlTheme` with the same key (`Theme="{StaticResource PrimaryButton}"`). Its variants use `BasedOn`, as `PrimaryButton` does on `BaseButton`. An implicit style (check box, radio, scroll bar, tooltip, menu) becomes a `ControlTheme` keyed by its type. WPF triggers become nested styles on pseudo-classes: `^:pointerover`, `^:pressed`, `^:focus-visible`, `^:disabled`, `^:checked`, `^:selected`. They keep the triggers' order, so the later one still wins. Fluent is the base theme, as Aero is in WPF, for the controls keycuts has no theme of its own for (`Window`, `ScrollViewer`, `ListBox`); keycuts' themes are application resources, so they win over Fluent's. A text box's placeholder is `TextBox.PlaceholderText` (WPF's `TextBoxChrome.Placeholder`), and a validation error is the `:error` pseudo-class (WPF's `Validation.HasError`). `TextBoxChrome.HasError` and `HasWarning` stay.
- **Focus**: `AppFocusVisual` becomes the `FocusAdorner` template of the shared themes. Like WPF's focus visual, it shows only for keyboard focus. The rule against `FocusVisualStyle="{x:Null}"` applies to `FocusAdorner="{x:Null}"` too.
- **Headings**: `AutomationProperties.HeadingLevel` is a number in Avalonia: `1` for `Level1`, `2` for `Level2`, and `0` for none.
- **Section labels**: Avalonia has no text-transform either, so `SectionLabel` stays a control.
- **Live regions**: in Avalonia, `AutomationProperties.LiveSetting` announces by itself when the element's text changes, so `LiveRegion.Announce` has no counterpart. **A live line is never collapsed.** It stays in the tree with empty text, because a line that comes back from collapsed is never announced ([correction 10](macos-port.md#plan-corrections-found-on-windows)).
- **Shadows**: WPF's `DropShadowEffect` (`BlurRadius`, `ShadowDepth`, `Direction`) becomes `DropShadowDirectionEffect` with the same values. Avalonia's `DropShadowEffect` takes `OffsetX` and `OffsetY` instead. The sheet's shadow stays on a separate border behind it, so the effect never re-renders the sheet's text.
- **Same names**: `KeyboardNavigation.TabNavigation="Cycle"`, `IsDefault`, `IsCancel`, `SizeToContent` and `AutomationProperties.AcceleratorKey`. **Renamed**: `ToolTipService.ShowOnDisabled` is `ToolTip.ShowOnDisabled`, and a menu's `InputGestureText` is `MenuItem.InputGesture` (only the text shown; `HotKey` binds the key).
- **Windows only**: the caption buttons (`CaptionButtonWidth`, the `Caption*` brushes), because macOS keeps its own window buttons beside the tabs ([Tabbed window](#tabbed-window-218)). Key names in text are each platform's ([Keys](#keys)).

## Spacing

**Everything sits on a 4px grid.** Every margin, padding and gap is a multiple of 4 and comes from `Space*` (a `Double`, for one side or a gap) or `Inset*` (a `Thickness`). The scale:

| Token | px | Typical use |
|---|---|---|
| `Space1` / `Inset1` | 4 | Control to its helper or note; icon to 12px text; between a label and its own note |
| `Space2` / `Inset2` | 8 | Label to its control; between buttons; icon to 13-14px text; section label to its first row |
| `Space3` / `Inset3` | 12 | Between groups inside a panel; text column beside an icon tile |
| `Space4` / `Inset4` | 16 | Top padding of screens and dialogs; between fields and setting rows; heading to content; panel padding; text to the control at the end of its row |
| `Space5` | 20 | (spare; prefer 16 or 24) |
| `Space6` / `Inset6` | 24 | Outer padding at the sides and bottom of every screen and dialog; between sections |
| `Space8` | 32 | Large gaps in empty states |

**Allowed exceptions**, each with a comment saying why:

- **Optical offsets of 1-2px inside a control template or beside an icon**: nudging a glyph to line up with text (`Margin="0,1,8,0"`), a scroll thumb's 2px inset.
- **Selection borders**: a card whose border goes from 1 to 2px when selected takes 1px off its padding, so nothing moves.
- **Negative margins that cancel a known inset**: the welcome bubbles' `-4` that lets three cards with `4` margins line up with the panel edges, or a borderless text box's `-2` that lines its text up with a label.
- **Focus rings and glows** (see [States](#states-hover-focus-selection-disabled)): they sit outside the control and follow its radius.

## Layout

### Outer padding

Every screen and dialog body has **16 at the top and 24 at the sides and bottom** (`PagePadding`, `24,16,24,24`). The tab bar, or a dialog's title bar, already gives the top room ([decision](#decisions)). Today the Creator uses `20,20,20,16`, and the Manager and the dialogs use 24 at the top. The Manager's rows keep 24 at the sides. The table ends 12 above the footer, because the footer's top border already separates them.

### Vertical rhythm

| Between | Gap | Token |
|---|---|---|
| A dialog's heading (level 1) and the first content | 16 | `Space4` |
| Two sections | 24 | `Space6` |
| Section label and its first row | 8 | `Space2` |
| Two fields, or two setting rows | 16 | `Space4` |
| Field label and its control | 8 | `Space2` |
| Control and its helper, note or error | 4 | `Space1` |
| Setting name and its note (same block) | 4 | `Space1` |
| A row's text and the control at its end (toggle, button) | 16 (left margin of the control) | `Space4` |
| Two buttons side by side | 8 | `Space2` |

**No divider lines** between sections or between rows ([decision](#decisions)): the section label and the gap of 24 separate sections, and 16 separates rows. That holds for the Settings screen too (#218's item 16 and its v5 mockup; v1 to v4 drew a line under each setting). The last row of a section sets no bottom margin, so the gap isn't doubled.

### Dialog footer

Edit and Confirm end in the same footer, one shared style, `DialogFooter`: `SurfaceSubtleBrush` background, a 1px top border in `BorderBrush`, `Padding="24,12"` (`DialogFooterPadding`), buttons right-aligned with 8 between them. The primary button is **last, at the far right**, and cancel comes before it ([decision](#decisions)). A secondary action that isn't cancel sits at the left edge. Enter presses the primary button (`IsDefault`) and Escape cancels (`IsCancel`). The body above the footer scrolls when the window is capped to the screen, and the footer never does.

### Window widths

The main window has **two sizes** ([decision](#decisions), mockup [`window-sizes-v1`](mockups/window-sizes-v1.png)):

- **Small, the Creator**: `MainWidth`, 600: what the whole tab bar needed plus about 60 of empty, draggable bar (#218), and about 110 since the tabs lost their icons. It must also fit two icon cards side by side in Settings' picker. Fixed-width, and it fits its content's height.
- **Large, the Manager and Settings**: one size for both, resizable, opening at 768 × 470 (720 × 470 at the least), just wide enough for the Manager's Runs column, and as short as it goes, where the Manager's table is the header and 5 whole rows with 24 under them, and remembered while the app runs. Settings never grows the window to fit its content: it takes the size the Manager last had, lays itself out in two columns, and scrolls when the window is shorter than it. Switching between the Manager and Settings doesn't move or resize the window; resizing either resizes both.
- The window keeps its top-left corner as it changes size, and stays inside the work area.
- **Confirm**: 440 (`ConfirmWidth`); **Edit**: 480 (`EditWidth`). They keep their widths while the main window grows ([decision](#decisions)). Dialogs size to their content's height (`SizeToContent="Height"`) and can't be resized.

### The Manager's create row

The Manager starts with the Creator's fields in one row, so a keycut can be added without leaving the list. It is an [inset panel](#inset-panels) (16 below it, then the search box), with a label row, a control row and the Creator's helper line under them:

- Left to right: **Target**, **Name**, **Description**, then the Create button. The fields share the row 2 : 1.25 : 1.25, 12 apart (`Space3`): a long target has the most room, and Name and Description are as wide as each other. Only the ratio sets the widths: each field sits in a `StarCell`, so text that runs past a box's end never moves the boxes (#252).
- **Target** is the drop area squeezed to one line: a 36 high box (`RadiusSm`) with the Creator's dashed outline, fill and glow, the target's type icon (16) at its left and the text on one line, then the Browse square button 8 to its right. A drop on the row, a paste or Browse fills it.
- **Name** is a plain text box with the Creator's "Saving…"/"Created" status at the right of its label. **Description** is always shown here; there is no "Add a description" link.
- The Create button (`PrimarySquareButton`, the rocket) ends the row, 8 after the description.
- Enter creates from the name or the description; in the target it resolves the target and then creates if the name is filled, or moves on to the name. After a create the new keycut appears in the table and the fields clear, ready for the next one, with the focus back in Target. The screen still opens with the focus in the search box.
- Name warnings, name errors and target errors use the Creator's helper line, under the whole row. A name that needs confirming asks in the same Confirm dialog.

### The Manager's table

- **The count** is a pill (22 high, fully round, `Caption` SemiBold in `TextSecondaryBrush` on `SurfaceSubtleBrush`) 12 to the right of the search box: "355 keycuts", or "4 of 355 keycuts" while a search is typed ([decision](#decisions)).
- **The Manager's message** (Deleted, Copied, Restored, errors) sits between the search box and the count, 12 from each, only while there is one, and takes no room otherwise. It is at most 300 wide and trims with an ellipsis (its tooltip is the whole text). `undo` comes after "Deleted …", against the count, so it is always in the same place ([decision](#decisions)).
- Header and rows are 36 high (`ManagerTable`).
- **Columns**, left to right: Name (1.2 shares, at least 120), Type (104), Description (1 share, at least 100), Target (3.4 shares, at least 160), then Runs (64), Last run (112) and Modified (112), the last column ([decision](#decisions)). They make way when the table is narrow, Modified first. The shared columns can be dragged. Target gets the most room, since targets are the longest values; the fixed ones fit their widest value (the Command badge, "20 minutes ago", the Runs header with its chevron) with the padding at both sides. Text cells have 12 at each side; a type badge, which has its own border, and a header, whose sort chevron may run into it, have 4 at the right ([decision](#decisions)).
- **A column's label starts where its values do**, 12 in. The sort header's focus frame (1 border, 4 padding) would push the label 5 further; the header is pulled left by that much ([decision](#decisions)).
- **Right-clicking a row** opens its actions menu at the pointer, as in Explorer, and selects the row. **There is no `...` column** ([decision](#decisions)): Shift+F10 and the Menu key open the selected row's menu under the row, its left edge where the name starts (while the search box is empty, as for Delete), and every item has its own key, shown beside it. A screen reader names the menu "Actions for <name>".

### The app footer

All three screens end in `FooterView`, which is **there only while a newer version is out**: it shows on every screen until the notice is dismissed or the app is up to date, and takes no room otherwise ([decision](#decisions)). It is 48 high at least, with `Padding="24,0"` and a top border in `BorderBrush`, on `SurfaceSubtleBrush`, and holds the update line, right-aligned, with its dismiss button. The count and the Manager's messages are in the Manager's search row.

### Inset panels

A panel set into a page (the welcome panel, Confirm's "Opens now / Will open" box) has `RadiusLg` corners, `PanelPadding` (16) and 16 below it. Groups inside it are 12 apart. Its background is `SurfaceSubtleBrush` with a 1px `BorderBrush` border. The welcome panel is the one exception: it is `AccentSoftBrush` with no border, because it is a greeting, not content.

### Overlay sheets and cards

A sheet over a window (today only the icon picker; any future picker follows the same pattern) is one shared pattern, `OverlaySheet` in `Controls.xaml`:

- **Scrim**: `ScrimBrush` over the page and the tabs, which are disabled meanwhile (`IsBodyEnabled`) so Tab stays in the sheet. The caption buttons (minimize and close) and dragging the window stay available, as on any window ([decision](#decisions)). A click on the scrim closes the sheet.
- **Sheet**: `SurfaceRaisedBrush`, 1px `BorderBrush` border, `RadiusLg` corners, 40 from the top of the window and 16 from the bottom, centered, at most the window's width less 2 × 16.
- **Shadow**: a `DropShadowEffect` in `ShadowColor` (blur 28, depth 8, opacity 0.3) on a separate border behind the sheet, so the sheet's text keeps ClearType.
- **Header**: `Padding="16,16,16,8"`, the title as a level 2 heading at 16 (`Subheading`), an optional note under it (`Caption`, 4 below), and the close button at the top right: an `IconButton` (32) with a 16 `Close` glyph, tooltip "Close", and an automation name that starts with "Close" ("Close the icon picker").
- Escape, the close button and a scrim click all close the sheet. Whether closing undoes anything is up to the sheet; the icon picker applies each pick at once, so closing undoes nothing.

A **card** in a sheet or panel: `SurfaceBrush`, 1px `BorderBrush`, `RadiusMd` corners, `Padding` 12, 4 at the sides and 8 below (cards in a row are then 8 apart), and `SurfaceHoverBrush` on hover. Selected cards follow [Selection](#states-hover-focus-selection-disabled). Card widths are worked out from the list's width so that two fit in a row with 2px to spare (the arithmetic sits in a comment beside `IconCard`).

### Tabbed window (#218)

The main window has no Windows title bar. Its top row is the **tab bar** (#218 calls it the rail), and it does everything a title bar does. The values below are where #218 starts; if they need adjusting once drawn in real pixels, #218 records the final numbers here.

- **Height**: `RailHeight` (48). The tabs stand on its bottom edge; there is no line between the bar and the page.
- **Left to right**: the app icon, the keycap alone with no socket (36, 8 from the edge, so 6 above and below it; [decision](#decisions)), and the name `keycuts` (16, SemiBold, 8 from the icon), then 24 (`Space6`) to the first tab, so the name stands apart from the tabs ([decision](#decisions)); the tabs `creator` and `manager` (**text only**, the name at 16 SemiBold like `keycuts`, lowercase; [decision](#decisions)); the settings tab, **the gear alone** (20, the size of Browse and Create, no text; [decision](#decisions)). The icon and `keycuts` sit 2 lower than centered and the tab names 2 higher, so **every name in the bar is on one line**, 26 from the top; the empty, draggable bar; minimize, maximize (always disabled) and close.
- **Tabs**: `TabHeight` (40), all the same height, 4 apart at the base. Folder shape: a square left side with an 8 cut corner and a slanted right side. Padding 12 at the left and the slant plus 8 at the right. The selected tab is filled with `SurfaceBrush`, runs into the page, and casts a soft shadow to its right only; its text is `TextBrush`. Inactive tabs are shaded and textured, and their text is `TabTextBrush`, which must meet 4.5:1 on the whole tab. The mockup's colors don't: light `#5d687b` is 3.48:1 at the tab's base and dark `#8d97a8` 4.43:1 at its top; `#465064` and `#9aa4b5` pass. No accent line on the selected tab.
- **Bar**: brushed metal (a vertical gradient with fine horizontal lines) and a faint dot grid in the empty space that fades out at both ends. Every color comes from the `Rail*`, `Tab*` and `Socket*` palette keys. **High contrast drops the textures**: the bar is `Window`, the tabs are outlined in `WindowText`, and the selected tab is marked by its outline running into the page.
- **Caption buttons**: `CaptionButtonWidth` (46) by `RailHeight`, with 10px glyphs: the Windows standard sizes, an exception to the grid and the icon sizes. Hover is a light wash (`CaptionHoverBrush`); close turns `#c42b1c` with a white glyph in both themes, as Windows does (`CaptionCloseBrush`, the one fixed color). **Maximize is disabled on every screen**, the resizable Manager included, and the window can't be maximized any other way either: not by double-clicking the bar, Win+Up, dragging to the top of the screen or the window menu ([decision](#decisions)). The Manager is still resized from its edges and snapped to the side of the screen.
- **Accessibility**: the tabs are a tab list. Their names are `creator`, `manager` and `settings`, with their selected state; Tab and the arrow keys reach them. **Ctrl+1, Ctrl+2 and Ctrl+3** pick them left to right from anywhere in the window (not while the icon picker has them off); each tab's tooltip is its name and key, "creator (Ctrl+1)", and the key is its `AcceleratorKey`. The caption buttons are named `Minimize`, `Maximize` and `Close`, with matching tooltips. Each screen's content is announced with its tab's name.
- **No screen headings**: the selected tab names the screen, so `keycuts creator`, `keycuts manager` and the creator/manager buttons beside them go, and the Settings screen has no heading. Content starts `PagePadding` (16) below the bar.
- **On macOS** (mockup [`macos-window-v1`](mockups/macos-window-v1.png)): macOS's own **traffic lights** take the caption buttons' place, at the left: 12 across, 8 apart, 20 from the edge, centered in the bar. **Zoom is always off** (gray) on every screen, so there is no zoom and no full screen, as Maximize is off on Windows. Then the keycap and `keycuts`, 16 after the lights, and the tabs as above. `MainWidth` is unchanged: the lights take at the left about what the caption buttons take at the right, and the empty bar runs to the right edge. No caption buttons and no window menu. Tooltips and accelerators name ⌘: "creator (⌘1)".
- **The Settings screen** follows the rest of this guide: `SectionLabel`s, setting rows 16 apart with no lines, 36 high buttons and text boxes, the output folder's two icon-only square buttons, and a last row, 24 below the rest, with `show welcome` at the left and `restore defaults` at the right. No note says changes are saved: with no Save button, that goes without saying ([decision](#decisions)). At the large size it has **two columns** with a 1px `BorderBrush` rule between them, 24 (`Space6`) from each: Output and Behavior on the left; Appearance, Updates and the last row on the right. Each column starts with its section label at the top of the page. The columns never stack: at the window's minimum, 720 × 470, they still fit side by side without scrolling ([decision](#decisions)).
- **Settings on macOS** (mockup [`macos-settings-v1`](mockups/macos-settings-v1.png)), at the same size: Output adds **Run keycuts in Terminal** (the PATH switch, `~/.zprofile`); Behavior has **Finder Quick Action** where Windows has "Right-click context menus", and **Use $HOME in paths** for "Use environment variables in paths"; the right column starts with a **Run box** section (**Shortcut**, a key recorder, and **Open at login**) above Appearance; **Icon** and **Transparent icon background** aren't shown (the Dock shows the bundle's Porcelain icon, #314); on macOS 26 and later with a signed build, the right column has a **Spotlight** section between Appearance and Updates: a **Quick key** row with a note and an **add to Shortcuts** secondary button (#328). Rows are 12 apart and sections 20 there (on the 4px grid), so both columns fit 470 high.

### The Run box (macOS)

Mockup [`macos-settings-v1`](mockups/macos-settings-v1.png), parts 4 and 5. A borderless window, 600 wide (the `RunBox*` tokens), centered across and a third of the way down the active screen, with `RadiusXl` corners and a 1px `BorderStrongBrush` outline. One text box, 56 high, the input at 20 with the keycap (28) before it and no border of its own, and a 1px `BorderBrush` line under it. Below, up to 6 rows of 40, inset 8, with `RadiusMd` corners: name (13 SemiBold, 112 wide, on the grid), the type badge, the target in `TextSecondaryBrush` trimmed at the end, and the runs ("42 runs", caption, tertiary). The selected row is filled with `AccentBrush` and its text is `OnAccentBrush`. A 32-high hint line at the bottom on `SurfaceSubtleBrush` names the keys: **Return** run, **Tab** complete, **↑ ↓** choose, **Esc** close. With nothing typed the rows are the keycuts run most recently, latest first, with no heading and the first one selected (#332); with no run recorded only the text box shows. A name that matches nothing has one row, "Create “name”…", with the rocket (the create button's icon) where the badge goes, and the hint says **Return** create. Tab's completion shows as gray text after the caret. The text box is named "Run a keycut"; the list is a list box.

### Empty, loading and error states

These are centered in the space the content would take. Top to bottom: an illustration (a 56 tile, `RadiusXl`, `AccentSoftBrush`, with a 32 glyph), 12 gap, a level 2 heading (`Subheading`, 16), 4 gap, one line of `TextSecondaryBrush` body text, 16 gap, then one button (36). An error state's heading is `DangerBrush`. Loading is a spinner and a line of secondary text, 8 apart. Text wraps at 560 at most.

## Type

Font: `AppFontFamily`, Segoe UI Variable with Segoe UI as the fallback. In the Avalonia app it is a platform token: those on Windows, the system font on macOS, at the same sizes ([In Avalonia](#in-avalonia)). Set once on each window; views inherit it.

### Ramp

| Token | px | Weight | Used for |
|---|---|---|---|
| `FontSizeHeading` | 20 | SemiBold | The level 1 heading of a dialog (Fluent "Subtitle"); the main screens have none after #218 |
| `FontSizeSubheading` | 16 | SemiBold | Level 2 headings: the welcome panel, empty and error states, sheet titles; the name `keycuts` in the tab bar (Fluent "Body Large") |
| `FontSizeInput` | 14 | Regular | Text typed into a text box; welcome bubbles' keycut names |
| `FontSizeBody` | 13 | Regular | Body text, field labels (SemiBold), buttons and tabs (SemiBold), menu items, table cells |
| `FontSizeCaption` | 12 | Regular | Helper text, notes, errors under a field, status lines, tooltips, table headers (SemiBold), section labels (ExtraBold), badges and row markers (SemiBold) |

**12 is the smallest text** ([decision](#decisions)). Body text stays 13. Fluent's body is 14, and moving to it would be a bigger change, made only if the owner decides to.

### Weights

- **SemiBold**: headings, field labels, setting names, button labels, table headers, the Manager's name cell, badges, menu item titles.
- **ExtraBold**: only the section label.
- **Regular**: everything else.
- **No plain `Bold`** anywhere. Today it is on the Manager's name cell and its column and sort headers, the status line's Undo, the Browse menu's File… and Folder… titles, the welcome bubbles' names and the type badges; they become SemiBold.

### Shared text styles

In `Controls.xaml`, used by every window. Window-local copies (`SectionLabel` in `SettingsWindow` and `ConfirmWindow`, `FieldLabel` in `SettingsWindow` and `EditWindow`, `FieldError`, `SettingNote`, `HelperText` in `CreatorView`) go away.

| Style | Size, weight, color | Spacing | Accessibility |
|---|---|---|---|
| `Heading` | 20, SemiBold, `TextBrush` | 16 below | `HeadingLevel="Level1"` |
| `Subheading` | 16, SemiBold, `TextBrush` | 4 below | `HeadingLevel="Level2"` |
| `SectionLabel` (a control, see below) | 12, ExtraBold, `AccentTextBrush`, shown in capitals | 8 below | `HeadingLevel="Level2"` by default; `Level3` inside a level 2 (the icon picker's groups) |
| `FieldLabel` | 13, SemiBold, `TextBrush` | 8 below | The control's automation name starts with the label text |
| `HelperText` | 12, Regular, `TextSecondaryBrush`, wraps | 4 above | `LiveSetting="Polite"`; warning and error states switch to `Assertive` |
| `ErrorText` | 12, Regular, `DangerBrush`, wraps | 4 above (16 when it closes a form) | `LiveSetting="Assertive"` |
| `Caption` | 12, Regular, `TextSecondaryBrush` | none | For notes beside a label, status lines and the count |

`HelperText` has states for warning (`WarningBrush` with a 12 `Warning` icon before it), error (`DangerBrush`) and success (`SuccessBrush` with a 12 `Check` icon). A style can't add the icon, so it is a control too (`Controls/HelperText`, with `Text` and `State`), like `SectionLabel`. It never uses `TextTertiaryBrush`.

**The section label is a control** (`Controls/SectionLabel`, like `TypeBadge`). WPF has no text-transform, so typing `OUTPUT` into the XAML is the only way to get capitals from a `TextBlock`, and screen readers then get capitals too. The control takes sentence-case text (`Text="Output"`), draws it in capitals, and gives sentence case to the automation name, so the source, the tests and Narrator all see "Output". The icon picker's group names (`AppIcons.FollowsThemeGroup`, `AlwaysTheSameGroup`) become sentence case for the same reason.

## Color and contrast

**Colors come only from the palette brushes**, as `DynamicResource`, in every view and style. That keeps light, dark and high contrast working. No hex literal appears outside `Light.xaml`, `Dark.xaml` and `HighContrast.xaml`. `ThemeTests.ControlStyles_DoNotHardCodeColors` already checks `Controls.xaml`, and #219's enforcement test extends that to every view. High contrast maps each key to a system color; a new key needs a high-contrast entry too, and `ThemeTests` fails until it has one.

### Roles

| Brush | Role |
|---|---|
| `TextBrush` | Headings, labels, body text, values the user typed |
| `TextSecondaryBrush` | **All readable supporting text**: helper text, notes, captions, status lines, table cells, column headers, menu item second lines, intros |
| `TextTertiaryBrush` | **Only** placeholders, disabled text, and decorative glyphs (the search icon). It meets 4.5:1 like any text, but it is for hints, never for text someone needs to read |
| `AccentBrush` | Fills (primary button, toggle on, selection bar, check badge), focus ring, accent borders, icons |
| `AccentTextBrush` (new) | Accent-colored **text**: section labels, links (Download, Undo), the active sort header |
| `AccentSoftBrush` | Selected row, icon tile background, the welcome panel, drag-over fill |
| `OnAccentBrush` | Text and icons on an accent fill |
| `SurfaceBrush` / `CanvasBrush` | Window and page background |
| `SurfaceSubtleBrush` | Footers, table header, inset panels, the drop area |
| `SurfaceRaisedBrush` | Overlay sheets and menus |
| `SurfaceHoverBrush` | Hover on rows, cards and ghost buttons |
| `BorderBrush` / `BorderStrongBrush` | Dividers and panel borders / dashed outlines, scroll thumbs |
| `ControlBorderBrush` | The border of a text box, check box, radio or toggle (3:1 against its surface) |
| `DangerBrush`, `WarningBrush`, `SuccessBrush` | Error, warning and success text and icons; never the only cue (pair with an icon or words) |
| `Badge*Brush` | Type badges only |
| `ScrimBrush`, `ShadowColor` (new) | Overlay sheets only |
| `Rail*`, `Tab*`, `Socket*`, `Caption*` (new, #218) | The tab bar only ([Tabbed window](#tabbed-window-218)) |

### Contrast (WCAG 2.2 AA)

- **Text people read meets 4.5:1** against every surface it can sit on, at every size the app uses. Even 16-20px SemiBold is short of WCAG's "large text" (24px, or 18.7px bold), so 3:1 never applies to text here.
- **Non-text UI** (control borders, the focus ring, icons that carry meaning) meets 3:1.
- Measured today (WCAG relative luminance):
  - `TextTertiaryBrush` is 3.0-3.4:1 in the light palette, which fails for every text it colors: placeholders (WCAG 1.4.3 covers them even beside a label), the Creator's name helper line, the Manager's column headers, the Browse menu's second lines and the icon picker's group headers. Light tertiary becomes `#646e80` (4.54:1 on `CanvasBrush` and `AccentSoftBrush`, 5.14:1 on white), still lighter than secondary, so placeholders stay recognizably hints. The helper line, headers, menu lines and group headers move to `TextSecondaryBrush` (5.0-5.6:1) anyway, because they aren't hints. Dark tertiary `#8d97a8` already passes on the surfaces text sits on (4.76:1 or better).
  - `AccentBrush` as text in the dark palette is 4.48:1 on `SurfaceSubtleBrush` (the footer's Download link, Confirm's section labels, the active sort header on the table header) and 3.90:1 on `SurfaceHoverBrush`. `AccentTextBrush` at `#6a9eff` is 4.75-5.89:1 on every dark surface. The light palette's accent already passes (4.56:1 at worst, on `AccentSoftBrush`), so light `AccentTextBrush` is the same `#2563eb`.
- `ThemeTests.ContrastPairs` gains the pairs this adds: `AccentTextBrush` on every surface at 4.5, `TabTextBrush` on both ends of the inactive tab's gradient and `TextBrush` on the bar at 4.5 (#218), `TextSecondaryBrush` on `AccentSoftBrush` at 4.5 (the welcome panel's text), and `TextTertiaryBrush` rises from 3.0 to 4.5 on `CanvasBrush`, `SurfaceBrush`, `SurfaceSubtleBrush` and `SurfaceRaisedBrush`, with a comment naming the only uses it may have.

## Control sizes

| Control | Size | Token / style |
|---|---|---|
| Text box | 36 high | `AppTextBox`, `InputHeight` (views stop setting `Height="38"`) |
| Text button (primary or secondary) | 36 high, `Padding="12,0"` | `BaseButton`, `ControlHeight` |
| Icon-only button with a border or fill, beside a text box or in the drop area (Create, Browse, and Settings' choose a folder and open the output folder, which never show text; [decision](#decisions)) | 36 × 36 | `SquareButton` (new, based on `SecondaryButton`/`PrimaryButton`), `ControlHeight` |
| Ghost icon button (no border: dismiss, sheet close) | 32 × 32 | `IconButton`, `IconButtonSize` |
| Text box editing inside a Manager row (rename, description) | 30 high | The **only** 30; it has to fit a 36 row |
| Manager table: header / row | 36 / 36 (40 if a name plus a 12px marker doesn't fit; #219 checks) | `ManagerTable` |
| Menu item: one line / two lines (Browse menu) | 32 / 48 min | `AppMenuItem`, `BrowseMenuItem` |
| Toggle switch | 36 × 20 drawn, **36 × 24 to click** | `ToggleSwitch` (its template adds a transparent hit area around the track) |
| Check box and radio box | 18 drawn; the label is part of the click area | The implicit `CheckBox` and `RadioButton` styles |
| Type badge, count pill | 22 high | `TypeBadge`, the Manager's count pill |
| Icon tile | 40 × 40, `RadiusLg` | `IconTile` (new), `TileSize` |
| App footer | 48 min | `FooterView` |
| Tab bar, tab, caption button (#218) | 48 / 40 / 46 × 48 | `RailHeight`, `TabHeight`, `CaptionButtonWidth` |

The owner's decision says icon buttons are 32. This guide reads that as the ghost `IconButton`. An icon-only button with a border or fill that sits beside a 36 text box is square at the box's height, so the row lines up. That is today's pattern too (Create 38 beside a 38 box).

**Pointer targets are at least 24 × 24** (WCAG 2.5.8, Target Size (Minimum)). That is the area that takes the click, which isn't always the area that is drawn:

- **By size**: text boxes, text and square buttons (36), ghost icon buttons (32), menu items, tabs and caption buttons. Check boxes and radio buttons pass with their label, which WPF makes part of the click area; one without a visible label needs a 24 hit area of its own.
- **The toggle** is drawn 20 high, so its template wraps the track in a transparent border 24 high. It then passes by size wherever it is placed, rather than depending on the space around it.
- **Anything smaller than 24** passes only by WCAG's spacing exception, where a 24px circle centered on it overlaps no other target, or its inline exception for a link inside a sentence. Today that is the sort headers (inside a 36 header row, with the next header well clear), the status line's undo, and the footer's download link (inline in the update sentence). A new small target must show which exception it meets, or grow to 24.

## Corner radii

Only the tokens: `RadiusSm` (5) for buttons, text boxes, badges, menu items and tooltips; `RadiusMd` (8) for menus, cards, the table and the welcome bubbles; `RadiusLg` (12) for inset panels, the drop area, sheets and icon tiles; `RadiusXl` (16) for the empty-state illustration. `Rectangle` outlines use `RadiusMdValue`/`RadiusLgValue`. Literals 3, 9, 10, 11 and 15 snap to the nearest token.

Exceptions, each with a comment:

- **Fully round shapes** use half their height: the count pill (11 at 22), the toggle (10 at 20), the check badge (9 at 18), the radio box (9 at 18).
- **Rings outside a control** follow its radius plus their offset: the focus ring is `RadiusSm` + 2 = 7, the text box glow `RadiusSm` + 3 = 8, the drop-area glow `RadiusLg` + 3 = 15 (16 at the 4px drag-over glow).
- **Slim scroll thumbs**: 3.

## Icons

Icons are the app's own `controls:Icon` glyphs (`Themes/Icons.xaml`), drawn in the text color they sit beside unless they carry a status (`AccentBrush`, `DangerBrush`, `WarningBrush`, `SuccessBrush`).

| Size | Where |
|---|---|
| 12 | Beside 12px text: row markers (Missing, Warning), helper-line status icons, the sort chevron, the check badge's tick |
| 10 | Only the caption buttons' glyphs (Windows' size) |
| 16 | **The default**: in buttons with text, ghost icon buttons (dismiss, close), menu items, list warnings |
| 20 | Square buttons (Create's rocket, Browse, choose a folder, open the output folder), the settings tab's gear and the welcome helper line |
| 24 | In a 40 icon tile |
| 32 | In the 56 empty-state illustration |
| 36 | The app icon in the tab bar (a picture, not a glyph: the keycap alone) |

Today's 11, 14, 15, 17, 18 and 23 snap to these. Gap between an icon and its text: **8** beside 13-14px text, **4** beside 12px text.

An icon-only button always has a tooltip and an automation name, and the name starts with the tooltip's words (see [Accessibility](#headings-and-accessibility)). A disabled icon-only button keeps its tooltip (`ToolTipService.ShowOnDisabled="True"`), so its bare icon can still be identified.

## States: hover, focus, selection, disabled

- **Hover**: rows, cards and ghost buttons get `SurfaceHoverBrush`; bordered controls darken their border to `TextSecondaryBrush`; text-like buttons (the description label button, links) turn `AccentTextBrush`. A primary button goes to `AccentHoverBrush`, then `AccentPressedBrush` when pressed.
- **Keyboard focus**: `AppFocusVisual`, a 2px `AccentBrush` ring 2px outside the control, on every focusable control. Text boxes add their accent border and soft glow. Never set `FocusVisualStyle="{x:Null}"` unless the control shows focus another way (the drop area's border, the sort header's frame).
- **Selected card** ([decision](#decisions)): a 2px `AccentBrush` border (padding 1 less, so nothing moves) and the **check badge**: an 18 round `AccentBrush` badge with a 12 `OnAccentBrush` check, 8 in from the top right corner. The background doesn't change. That holds for the welcome bubbles (which gain the badge) and the icon cards (which lose their `AccentSoftBrush` fill). The check means selection never relies on color alone (WCAG 1.4.1), and the card reads the same on any panel.
- **Selected table row**: `AccentSoftBrush` and a 3px `AccentBrush` bar at its left edge. The bar keeps the selection visible in high contrast, where the fill disappears.
- **Disabled**: text and glyphs turn `TextTertiaryBrush`; the control keeps its shape. Don't hide controls just because they are unavailable; disable them, and say why in a tooltip when it isn't obvious (the Manager's greyed-out Edit item does).
- **Error on a field**: the text box's error chrome (`TextBoxChrome.HasError`), plus `ErrorText` under it. **Warning**: `TextBoxChrome.HasWarning`, plus the warning state of `HelperText`.

## Headings and accessibility

- **One level 1 heading per dialog**, the `Heading` style: `edit keycut`, and Confirm's question. After #218 the main screens have none; the tab list names them (until then, `keycuts creator`, `keycuts manager` and `settings` are their headings).
- **Level 2** for each section label and second-level heading (the welcome panel, the empty and error states, a sheet's title); **level 3** for a group inside a level 2 (the icon picker's groups). Settings' section labels aren't marked today; `SectionLabel` marks them.
- **The automation name starts with the visible label** (WCAG 2.5.3, Label in Name). Speech users say what they see. Extra words go after the label, or into `AutomationProperties.HelpText`. Today's misses: show welcome ("Show the welcome panel"), check now ("Check for updates now"), got it ("Dismiss welcome"), add to PATH ("Add the keycuts folder to PATH"), and the icon-only choose ("Choose", tooltip "Choose a folder") and open folder ("Launch", tooltip "Open the output folder").
- **Live regions**: status lines and helper text are `LiveSetting="Polite"`; errors and warnings are `Assertive`. `LiveSetting` alone raises nothing in WPF: a value that changes in code is announced through `LiveRegion.Announce`, as the footer's count is.
- **Keyboard**: everything works without a mouse. Enter presses a dialog's primary button, Escape cancels or closes (on the Manager and, after #218, the Settings screen, it returns to the creator), and Tab order follows reading order. A sheet traps Tab while open (`KeyboardNavigation.TabNavigation="Cycle"`).
- **Color is never the only cue**: errors and warnings have words and an icon, and selection has the check badge or the row bar.
- **High contrast**: every brush maps to a system color, and shapes that carry meaning (selection bar, borders) stay visible. Test new UI in a high-contrast theme.

## Writing

Follow the Microsoft Writing Style Guide: plain, short, friendly, second person ("you"), contractions welcome.

### Capitalization

- **Titles and tabs are lowercase**, in the brand style ([decision](#decisions)): the tabs `creator`, `manager` and `settings` (the gear tab's name and tooltip), and the dialog titles, window title and heading alike: `edit keycut`, and Confirm's `replace keycut`, `name already in use` and `restore defaults`. A heading that is a sentence or a question rather than a title, such as Confirm's "Replace “npp”?", is sentence case.
- **Button text is lowercase** ([decision](#decisions)), for text buttons and link buttons and their automation names: `cancel`, `save`, `replace`, `create anyway`, `rename anyway`, `check now`, `show welcome`, `restore defaults`, `got it`, `clear search`, `open settings`, `create a keycut`, `undo`, the footer's `download`. Proper names keep their case: `add to PATH`.
- **Sentence case** for everything else: menu items, labels, setting names, level 2 headings, section labels' source text, tooltips, column headers, messages. Capitalize the first word and proper nouns only: "Edit in Notepad", "Open file location", "Use environment variables in paths".
- **The words keycuts and keycut are always lowercase**, the app's name and the noun alike, even at the start of a sentence or label ([decision](#decisions)): "keycuts lets you open your apps…", "keycuts 1.4.1 is available", "keycut name", and "keycuts" as the table's automation name. Today "Keycut name" (two automation names) and "Keycuts" (the table) break this.
- **Section labels and group names are written in sentence case in the source** ("Output", "Follows the theme"). The `SectionLabel` control draws them in capitals.
- Product and platform names keep their own case: Windows, File Explorer, Win+R, PATH, GitHub, Notepad, URL.
- Names the owner chose are kept as written: the icon names and descriptions in `Themes/AppIcons.cs` ("Black & White", "Team Ralon FTW!"), which a test pins.

### Keys

Write key names as Windows does, joined with `+` and no spaces: `Ctrl+Shift+E`, `Shift+F2`, `Win+R`, `Enter`, `Escape`, `Delete`, `Page Up`. In a tooltip, the key goes in parentheses after the action: "Undo the delete (Ctrl+Z)". Menus put it in `InputGestureText`.

On macOS, write them as macOS does: the symbols in the order ⌃ ⌥ ⇧ ⌘ with no plus signs, then the key: `⌘1`, `⇧⌘E`, `⌘⌫`, `⇧F2`, `Return`, `Esc`, `⇧⌘Space`. ⌘ takes Ctrl's place (⌘C, ⌘E, ⌘F, ⌘Z, ⇧⌘E, ⇧⌘O); **Return runs** the selected keycut, as Enter does; F2 and ⇧F2 stay; **⌘⌫ or forward delete** moves to the Trash, and a plain ⌫ does nothing in the table; ⌘↑ and ⌘↓ go to the first and last row; the row menu opens by Control-click; ⌘, opens Settings and ⌘W closes the window (keycuts stays in the menu bar). The words: "Show in Finder" for "Open file location", "Edit in text editor" for "Edit in Notepad", Trash for Recycle Bin, Terminal for Win+R.

### Ellipsis

Use the single character `…` (U+2026), never three dots, in two cases only:

- A **command that asks for more input before it acts**: the Browse menu's "File…" and "Folder…" (they open a picker), and "Edit…" in the row menu (it opens a form; today it says "Edit"). Rename, Delete and Open act at once and take none.
- **Work in progress**: "Saving…", "Loading keycuts…", "Checking for updates…".

Not elsewhere: the Creator's target placeholder "Drop or paste a file, folder, URL ..." becomes "Drop or paste a file, folder, or URL".

### Punctuation

- **No period** on labels, buttons, menu items, tooltips that are fragments, column headers, headings and placeholders.
- **Periods** on full sentences: notes, helper text, messages, errors. A setting's note is a sentence ("Add “keycut this!” to File Explorer menus.").
- **Exclamation marks** only in the four places that have them ([decision](#decisions)): "Welcome to keycuts!", the Explorer verb "keycut this!", "Created. Now try it out!" and the Classic icon's description. Nowhere else.
- **Apostrophes and quotes** in the GUI are typographic: `’` and `“ ”`. Today the GUI mixes them with `'` ("Can't load your keycuts", "Couldn't delete"). The CLI's console output stays plain ASCII, because the console's code page may not show them.
- **Em dash** without spaces around it ("dashes, or dots—no \ / :"). Better still, rewrite the sentence without one.
- The serial (Oxford) comma: "a file, folder, or URL".

### Placeholders

A placeholder is a hint inside an empty box, in `TextTertiaryBrush` (4.5:1, like all text). Anything people need goes in the label or helper text instead. The Name and Description fields use **examples, the same in both forms** ([decision](#decisions)): "e.g. art" and "e.g. my paintings (optional)": the description's says it is optional, since its label doesn't. The Edit form changes from "What you type in Win+R" and "What is it for? Shown in the manager". "e.g." with no comma after it is allowed in placeholders only, where space is tight; elsewhere write "for example". A placeholder that isn't an example says what goes there ("Search names, types, descriptions, or targets", "A file, folder, or URL").

### Messages

- Say what happened, then what to do: "Couldn’t save to the output folder. Check Settings." Start a failure with "Couldn’t", not "Error" or "Failed to".
- Name the thing in curly quotes when it helps: "Deleted “npp”".
- Don't blame the user, don't say "please", don't apologize unless the app is at fault ("Something went wrong in keycuts.").
- Status lines are short and disappear on their own (`StatusMessageDuration`). Errors stay twice as long.

### Tooltips and automation names

- An icon-only button's tooltip is its action as a short phrase in sentence case ("Browse", "Choose a folder", "Open the output folder"). Its automation name starts with the same words. The tabs and caption buttons use the names in [Tabbed window](#tabbed-window-218).
- A tooltip that shows a full value (a trimmed path, a description) is the value itself, with no label in front.

## Decisions

Answers from the owner. Each settles a discrepancy; the date is when it was made.

| Date | Topic | Before | Decision |
|---|---|---|---|
| 2026-11-13 | Page and dialog heading size | 20 in Creator and Confirm, 22 in Manager, Settings and Edit | **20 SemiBold** (`FontSizeHeading`) everywhere; `FontSizeHeadingLarge` goes away |
| 2026-11-13 | Section labels | 11 ExtraBold accent capitals; 10 below in Settings, 4 in Confirm; the icon picker's groups in tertiary | **Keep the capitals eyebrow, at 12** (`FontSizeCaption`): one shared style, one spacing below it (8) |
| 2026-11-13 | Screen titles and capitalization | Mixed | Lowercase brand style for the two main screens and their nav buttons; sentence case for everything else; `keycuts` always lowercase |
| 2026-11-13 | Dialog titles (when approving the reference sheet) | "Settings", "Edit keycut", "Replace keycut", "Name already in use" | **Lowercase too**, window title and title heading: `settings`, `edit keycut`, `replace keycut`, `name already in use`. Replaces "sentence case for dialog titles" in the row above |
| 2026-11-13 | Control heights | Inputs 30/36/38, text buttons 32/34/38, icon buttons 38/40 | **36** for every text box and text button; **32** for icon buttons (read as the ghost `IconButton`; bordered square buttons beside a box match its 36); **30** only for editing inside a Manager row |
| 2026-11-13 | Top padding (when approving the reference sheet) | 24 above the heading (20 in the Creator) | **16 at the top**, 24 at the sides and bottom: the heading was too far below the title bar |
| 2026-11-13 | Window widths | Main 440, Confirm 440, Edit 480, Settings 500 | **Keep as they are**, now as tokens. Amended for the main window by the #218 rows below |
| 2026-11-13 | Capitalizing "keycut" first in a label | "Keycut name", "Keycuts" | **Always lowercase**, the brand and the noun alike |
| 2026-11-13 | Exclamation marks | Four | **Keep all four** ("Welcome to keycuts!", "keycut this!", "Created. Now try it out!", the Classic icon's line); no others |
| 2026-11-13 | Level 2 heading size | 16, and 20 for the icon picker's title | **16 for every level 2**, the icon picker's title included |
| 2026-11-13 | Selected cards | Bubbles: border only; icon cards: border, fill and check | **2px accent border and the check badge**, no fill |
| 2026-11-13 | Icon tiles | 38/r10 (Confirm), 42/r11 (Creator), 58/r15 (empty state) | **40 with `RadiusLg`** inline; the empty-state picture **56 with `RadiusXl`** |
| 2026-11-13 | Smallest text | 11 in badges, row markers and the Browse menu | **12 minimum**; `FontSizeEyebrow` goes away |
| 2026-11-13 | Name and description placeholders | Creator: examples; Edit: hints | **Examples in both** ("e.g. art", "e.g. Recent paintings") |
| 2026-11-13 | Button text (aligning with #218) | Sentence case ("Show welcome", "Check now", "Save") | **Lowercase** for every text and link button ("show welcome", "restore defaults", "check now", "save"); proper names keep their case ("add to PATH"). Menu items and labels stay sentence case |
| 2026-11-13 | Divider lines (aligning with #218) | None in the app; #218's mockups v1 to v4 drew one under each setting | **None**: too cluttered. Gaps separate rows and sections. #218 now says the same (item 16, mockup v5) |
| 2026-11-13 | Output folder buttons (aligning with #218) | Icon-only choose and open; #218's mockup shows a text "Browse" and no open | **Both, icon-only**, in 36 square buttons |
| 2026-11-13 | Settings tab (aligning with #218) | #218: the gear alone, named `Settings` | **The gear alone, no text**; `settings` is its tooltip and automation name |
| 2026-11-13 | Icon picker over the Settings screen (aligning with #218) | #218: disables everything, the tab bar included | **Disables the page and the tabs**; the caption buttons and dragging stay available |
| 2026-11-13 | Main window width and headings (aligning with #218) | Creator 440; headings `keycuts creator` and `keycuts manager` | The Creator and Settings share `MainWidth`, set by the tab bar; Confirm keeps 440 and Edit 480; the main screens lose their headings (#218) |
| 2026-11-13 | Placeholder contrast (PR #220 review) | Light `TextTertiaryBrush` `#818b9d`, 3.44:1, allowed for placeholders | **4.5:1 for placeholders too**: light `TextTertiaryBrush` becomes `#646e80`; dark is unchanged |
| 2026-11-14 | Gap between the name and the first tab (#218, mockup v7) | 4, from #218's item 6; v6 drew it and it looked cramped (v3 had asked for more room, v5 had about 18) | **16** (`Space4`). The tabs stay 4 apart. At `MainWidth` 600 the empty, draggable bar is still about 66 |
| 2026-11-14 | Maximizing the main window (#218) | Maximize on the Manager only (button, double click on the bar, Win+Up, drag to the top, window menu), with Windows 11's snap layouts over the button; disabled on the Creator and Settings | **Off on every screen, every way**: the button is always disabled, and the window refuses the maximize command. The Manager keeps its resize edges and snapping to the sides. Snap layouts over the button go with it |
| 2026-11-15 | Window sizes (mockup `window-sizes-v1`) | The Creator and Settings `MainWidth` wide and as tall as their content; the Manager resizable on its own | **Two sizes**: small for the Creator (unchanged), large for the Manager and Settings, which share it. Settings never grows the window: **two columns** (option C; one column that scrolls was option D), stacking into one when the window is narrow. The Manager gets the Creator's fields as one row on top (Target, Name, Description); a create there clears the fields for the next one, and Enter creates from any field |
| 2026-11-16 | Large size at first | 960 × 700 | **960 × 540**: the owner's pick from the running app. Settings still fits in two columns without scrolling |
| 2026-11-16 | Large size at first, again | 960 × 540 | **960 × 544**: 4 taller, so the Manager's table is the header and 7 whole rows (297) with 24 under it, and the torn corner bites into a full last row. The table is always a whole number of rows tall; at other heights the rest goes under it |
| 2026-11-16 | Settings' "Changes are saved as you make them." | A `Caption` note with a check above the last row | **Gone**: with no Save button, saving as you go is what people expect |
| 2026-11-16 | The Creator's description label | "Add a description (optional)" while the box is hidden, "Description (optional)" while it shows | **"Description (optional)" both ways**; it is still the button that shows and hides the box |
| 2026-11-16 | Order in Settings' Appearance | Icon, then Theme | **Theme, then Icon** |
| 2026-11-16 | Settings' version row | "Version 1.6.0" in SemiBold; "You're up to date (1.6.0)." | **"Version · 1.6.0"** like "Icon · Black & White" (the value in secondary text), and **"You're up to date."**: the version is on the line above |
| 2026-11-23 | The commit in Settings' version row (#321) | "Version · 2.1.0" | **"Version · 2.1.0 (abc1234)"**: the first seven characters of the commit, in the same secondary-text run as the number. No commit (a source archive) shows the number alone, with no empty brackets |
| 2026-11-16 | The Manager's column labels | 5 to the right of their values (the sort header's focus frame), most visible over Runs | **Lined up with the values**, 12 in |
| 2026-11-16 | The Manager's row menu | A 44 wide column of `...` buttons | **No column**: right-click a row, or Shift+F10 and the Menu key for the selected one. The room goes to the other columns |
| 2026-11-16 | The Manager's column widths | Name 1.7 shares (190 at least), Type 110, Description 1.5, Target 2.8 (120), Modified 110, Runs 72, Last run 110 | **Name 1.2 (120), Type 104, Description 1, Target 3.4 (160), Modified 112, Runs 64, Last run 112**, with 4 at the right of a badge and a header: names and descriptions are short, targets long |
| 2026-11-16 | Tab icons | A 16 icon before `creator` and `manager` | **Text only**; the tabs are 24 narrower. The Creator stays 600 wide, so the empty bar grows to about 110 |
| 2026-11-16 | The keycut count | A number in a pill at the left of every screen's footer; "4 of 355 shown" beside the search box while searching | **One pill, beside the Manager's search box**: "355 keycuts", or "4 of 355 keycuts" while searching. The other screens show no count |
| 2026-11-16 | The bottom bar | On every screen: the count, the Manager's messages and undo, the update line | **Only while a newer version is out**, until dismissed or up to date. The Manager's messages move to its search row (chosen over a bar that comes and goes, and a toast over the table), with undo after the message |
| 2026-11-16 | The window's shape | A rectangle | **A torn bottom-right corner** (`docs/mockups/torn-corner-v2`, B2: 108 along the bottom, 50 up the side, bowing in, with a weave band and loose threads in `ControlBorderBrush`), transparent behind. The page draws its own 1px `BorderStrongBrush` edge, since a transparent window gets none from Windows. The rip is the bottom-right resize handle at the large size. The bottom bar's right padding clears it (`FooterBarPadding`), so its links stay whole; **the pages keep no room for it**: with their usual 24 below, the rip cuts into the last element (the Manager's table, the Creator's description box), and clicks there go through to the desktop. High contrast keeps the plain rectangle |
| 2026-11-16 | The app icon, bigger | The key about 82% of its tile; in the tab bar a 20 icon in a 28 socket | **The key fills its tile** (`docs/mockups/icon-size-v1`, B): 96% from 48 px up, its shadow running off the tile, and all of it below. **Transparent versions** (the keycap alone; Classic's monogram alone) ship with the app, and **Transparent icon background** in Settings shows them on the taskbar, Alt+Tab and the shortcuts. **The tab bar always shows the keycap alone, at 36, with no socket** (`docs/mockups/icon-size-v2`, R3) |
| 2026-11-17 | The Creator's form and the tab bar | Labels above their boxes; "Description (optional)" a button that hid the box; tab names 13, 16 after `keycuts`, a 16 gear, the names 4 apart vertically | **Labels at the left of their boxes** in the Creator, the boxes starting at one edge 16 after "Description"; **Description always shown**, a plain label in every form, its placeholder "e.g. my paintings (optional)"; "Saving…" and "Created" under the name box. The welcome bubbles lose their "→ name" line. **Tab names 16**, **24 after `keycuts`**, **the gear 20**, and **all the bar's names on one line** (`docs/mockups/creator-polish-v1`) |
| 2026-11-17 | The Modified column's place | Before Runs and Last run | **The last column**, at the far right while the table is wide enough to show it |
| 2026-11-17 | Settings at a narrow window | The two columns stacked into one below 840 | **Always two columns**: at the minimum 720 × 470 they fit side by side without scrolling (rendered from the real view; stacked, the page scrolled 231) |
| 2026-11-17 | Large size at first, smaller | 960 × 544 | **768 × 470**: the smallest height, and just wide enough for the Manager's Runs column. Settings fits in two columns there; the Manager shows the header and 5 rows, and widening brings back Last run, then Modified |
| 2026-11-20 | The window on macOS (the macOS mockups) | Windows' caption buttons at the right | **macOS's traffic lights at the left**, zoom always off, then the keycap and name and the tabs; the same `MainWidth` and sizes |
| 2026-11-20 | The Manager's main key on macOS | Finder renames on Return | **Return runs the keycut**, as Enter on Windows and Return in the Run box; F2 renames |
| 2026-11-20 | Deleting by key on macOS | Delete on Windows | **⌘⌫ (as Finder) or forward delete**; a plain ⌫ does nothing in the table |
| 2026-11-20 | The Run box shortcut (plan correction 2) | ⌃⌥Space proposed, a macOS default | **⇧⌘Space** by default, changeable in Settings; no ⌃⌥ shortcuts (VoiceOver's modifier) |
| 2026-11-20 | Menus on macOS | — | The app menu (About, Settings… ⌘,, Hide, Quit ⌘Q), the standard Edit menu, View (⌘1 to ⌘3), Window, Help; a menu bar icon (the app icon’s keycap in full color, the one chosen in Settings) with Run…, New keycut…, Manager, Settings… and Quit keycuts. macOS's own items keep macOS's Title Case |
| 2026-11-20 | Settings on macOS | The Windows rows | Run keycuts in Terminal, Finder Quick Action, Use $HOME in paths, a Run box section (Shortcut, Open at login); no Transparent icon background |
| 2026-11-19 | The font on macOS ([the macOS plan](macos-port.md), Phase 3.1) | Segoe UI, the only platform | **A platform token**: Segoe UI Variable and Segoe UI on Windows, the system font on macOS, with the same sizes and weights |
| 2026-11-13 | Dialog button order | Cancel, then primary at the far right | **Keep it**: primary last, at the far right. A deliberate difference from Fluent's ContentDialog, which puts the primary button first |
| 2026-11-22 | The torn corner on macOS | None (the native window) | **Optional, off by default** (Settings → Behavior → Torn corner, issue #292, asked for directly by the owner without a new mockup). On: the window is transparent behind the page, the page is clipped to the same torn shape as Windows (`TornCorner`, the rip is the resize grip), and macOS keeps its own frame, rounded top corners and traffic lights; the page draws no border of its own. Off: the opaque native window, as in `macos-window-v1`. Switches live, no restart. Restore defaults turns it off |
| 2026-11-22 | Settings on macOS, rearranged | `macos-settings-v1` | **`macos-settings-v2`** (the owner's nine changes). Behavior (left): Open at login first, then Ask before replacing, Finder Quick Action, Use $HOME in paths, Torn corner. The right column starts with the **Run Box** row (was Shortcut under a RUN BOX section label, now without the label), then **Run keycuts in Terminal** (was under Output), then Appearance and Updates. No sentence under the output folder (errors still show there) or under Torn corner. Notes: Open at login "keycuts waits in the menu bar."; Finder Quick Action "Add “keycut this!” to Finder’s Quick Actions. Turn it on in System Settings → Extensions."; Use $HOME "Save paths in your home folder as $HOME/…. Hold shift to see or create keycuts even when off.". macOS only: the Windows page keeps its layout and wording |
| 2026-11-22 | Settings: one Behavior section, in step on both systems | `macos-settings-v2` | **`macos-settings-v3` and `windows-settings-v1`** (the owner's, to keep the two pages in sync). No OUTPUT heading: Behavior (left) opens with **Output folder** (no sentence under it; an error still shows there). macOS Behavior: Output folder, Run Box, Open at login, Ask before replacing a keycut, Finder Quick Action, Use $HOME in paths, Run keycuts in Terminal. Windows Behavior: Output folder, Ask before replacing a keycut, Right-click context menus, Use environment variables in paths. Right column on both: Appearance (macOS: Theme, Icon; Windows: Theme, Icon, Transparent icon background) and Updates. The notes under the paths setting read "…Hold shift to see or create keycuts even when off." on both. Right-click context menus (Windows) and Finder Quick Action (macOS) are the same setting for each system; Run Box, Open at login and Run keycuts in Terminal are macOS only for now (issues for the Windows ones are open), and Transparent icon background is Windows only for now. The README's Windows Settings pictures need regenerating on Windows |
| 2026-11-22 | The torn corner on macOS, always | Optional, off by default (the row above) | **Always on, no setting** (the owner's decision, in the PR for #308). The Settings row is gone from macOS; the corner is torn on both systems and only high contrast keeps the plain rectangle. The window is transparent behind the page and set again once it is open, because a transparency hint set before the native window exists is not honored (the corner came up black). macOS keeps its own frame, rounded top corners and traffic lights. Replaces the optional-corner row above and the Torn corner row in `macos-settings-v3` (which now has none) |
| 2026-11-22 | Transparent icon background on macOS (#305) | Not offered (no tile behind the Dock icon, which was wrong) | **Offered, last under Appearance** (`macos-settings-v4`), "Affects the Dock icon." The Dock icon becomes the keycap alone; the bundle's own icon (Finder, Desktop, Login Items) never changes |
| 2026-11-22 | The Dock icon's size (#310) | The full-bleed art (a fifth bigger than other apps') | **Apple's icon grid**: the art at 824 of 1024 px, centered on a transparent canvas, for the Dock image only (`DockIconImage`). `assets/icons`, `keycuts.icns` (macOS shrinks it to 81% itself) and Windows are untouched |
| 2026-11-23 | The macOS Dock icon and Settings' icon rows (#314; supersedes #305 and #310 on macOS) | The Dock icon followed Settings while running (`MacDockIcon`, on Apple's grid) and went back to Porcelain at a quit; Settings offered Icon and Transparent icon background | **The Dock always shows the bundle's Porcelain icon**, running or pinned. No runtime Dock icon, and **Icon and Transparent icon background aren't offered on macOS** (Windows keeps both). A bundle icon that follows light and dark (an Icon Composer `.icon`) is parked: it takes the system's Liquid Glass mask and needs a hand-made dark variant |
| 2026-11-24 | The Run box with nothing typed (#332) | Only the text box | **The keycuts run most recently**, up to 6, latest first, as the same rows as when typing: no heading, the first one selected, so Return repeats the last keycut. Only the text box when no run is recorded. No mockup was rendered: it reuses the rows as they are |
| 2026-11-23 | Settings' Spotlight section on macOS (#328) | Spotlight can't run a keycut | **A Spotlight section** (macOS 26+, only when the bundle has the signed shortcut) in the right column: **Quick key**, "Adds “Run keycut” to Shortcuts. In Spotlight, press Add quick keys on it and type k; then “k gmail” runs a keycut.", and an **add to Shortcuts** secondary button that opens Shortcuts' import sheet. No mockup was rendered (`Render-Mockup.ps1` is Windows-only): it reuses the Run Box row's layout (name and note left, secondary button right) and the Updates section label |
| 2026-11-22 | Windows Settings: Run Box and Open at login (#306, #307) | Neither (Win+R was the Run box) | **`windows-settings-v2`**: Behavior is Output folder, **Run Box** (Ctrl+Alt+Space; Ctrl, Alt or Win needed, Shift alone refused), **Open at login** ("keycuts waits in the system tray."; "Turn keycuts on in Settings → Apps → Startup." when Startup apps has it off), Ask before replacing, Right-click context menus, Use environment variables in paths. The tray icon's menu is the macOS menu bar's: Run… (with the shortcut), New keycut…, Manager, Settings…, Quit keycuts. **Closing the window hides it to the tray** (macOS parity, no option; the owner left the choice to the PR) |

Defaults taken from best practice, listed in #211 and not overturned: the 4px grid, one outer padding (24, with 16 at the top since the owner's change), one dialog footer (`24,12`), radii from the tokens only, readable text at 4.5:1, body text at 13 and inputs at 14, no plain Bold, one set of shared text styles, headings marked for assistive tech, colors only from theme brushes, and the writing rules above.

Settled by this guide without a question, because they don't change how a screen looks: section labels as a control with sentence-case source text (Narrator and the tests get "Output", not "OUTPUT"); `AccentTextBrush` for accent text (the dark accent text gets slightly lighter, which WCAG requires); automation names starting with the visible label; typographic apostrophes in the GUI.

## Cleanups that need no decision

Small, mostly invisible changes #219 makes along with the rest (by file):

- **`Controls.xaml`**: `BaseButton` padding 13 → 12; `AppTextBox` padding 10 → 12; context menu padding 5 → 4; `AppMenuItem` min height 30 → 32; button icon-to-label gap 7 → 8 (Save, Confirm); `TypeBadge` 11 Bold → 12 SemiBold.
- **`CreatorView.xaml`**: outer margin `20,20,20,16` → `PagePadding` (`24,16,24,24`); welcome panel padding `14,12` → 16, 18 below → 16, intro margins `0,6,0,10` → `0,4,0,12`, helper line 9 above → 8; Got it 32 → 36 and Add to PATH 34 → 36 high; drop area inner margin `10,18` → `12,16`, text column 10 → 12; Name and Description label gap 6 → 8; Name and Description boxes 38 → 36; Create 38 → 36 and Browse 40 → 36 square; name helper 8 above → 4; Browse menu items 46 → 48, their second lines 11 tertiary → 12 secondary; the name status check 14 → 12 with a 4 gap; welcome helper icons 18 → 20.
- **`ManagerView.xaml`**: heading row's top margin 24 → 16; header row 34 → 36; header and cell padding 10 → 12 (#219 checks the narrowest table still fits); the sort header's frame radius 3 → `RadiusSm`, padding `3,2` → `4,2`, chevron 11 → 12 with a 4 gap; Missing and Warning markers 11 → 12, gap 3 → 4; row menu button 28 → 32 with a 16 glyph (its menu's offset follows: `-(250 - 32)`); search box padding `34,0,10,0` → `36,0,12,0`, its icon 11 in → 12; loading text gap 10 → 8; empty-state spark 14 → 16; empty-state Create button 38 → 36.
- **`ManagerStatusView.xaml`**: Undo padding `6,2` → `8,4`, gap 6 → 8.
- **`FooterView.xaml`**: min height 46 → 48; count pill padding `9,0` → `8,0`; dismiss glyph 14 → 16; padding 16 → 24 once #218 removes the gear.
- **`SettingsViewModel.cs`**: the output folder's helper "New keycuts files are saved here." → "New keycuts are saved here."
- **`SettingsWindow.xaml`** (done in #218: the dialog became `Views/SettingsView`, built to these values): heading gap 22 → 16; section gap 28 → 24; setting-name-to-note gap 2 → 4; toggle and button left margin 20 → 16; save error 14 above → 16; Choose and Open folder become 36 square buttons with 16 → 20 glyphs; the icon button padding 5 → 4; icon picker per the sheet pattern (radius 10 → 12, header margin `18,16,12,8` → `16,16,16,8`, 14 below → 16, card padding 10 → 12 and margins `5,0,5,10` → `4,0,4,8`, the check badge 7 in → 8).
- **`ConfirmWindow.xaml`**: header 18 below → 16, text 14 from the tile → 12; targets box padding `14,12` → 16 and 14 below → 16; second section label 12 above → 12 (kept) and 4 below → 8; warning icons 15 → 16.
- **`EditWindow.xaml`**: heading gap 20 → 16; label gap 6 → 8; error gap 6 → 4; save error 14 above → 16.
- **Edit and Confirm**: body margin 24 → `PagePadding` (16 at the top); footer `Padding="24,13"` → the shared `DialogFooter` (`24,12`).

## Tracking

- #211 wrote this guide.
- #218 (the tabbed window) follows it: its tab bar, the Settings screen and the footer are built to these rules from the start.
- #219, "Update the app to follow the style guide", holds the checklist of changes by screen and an enforcement test (`StyleGuideTests`) that reads the GUI's XAML and fails on a literal font size, a hex color outside the palettes, a margin, padding or radius off the grid or outside the tokens, `FontWeight="Bold"`, or a heading or section label without its `HeadingLevel`. A file not converted yet is on its `NotYetConverted` list, and the guide's exceptions are in its `Exceptions`, each with its reason.

**Order of work**: (1) #219 Part 1, the foundation: tokens, shared styles, `SectionLabel` and the contrast tests, which change little on screen; (2) #218, in two PRs, first the window frame and tabs, then Settings as a screen, both built with those styles; (3) #219 Part 2, the screen-by-screen cleanup of what #218 doesn't rebuild (the Creator's body, the Manager, Edit, Confirm, the footer), then the enforcement test switched on and the screenshots regenerated once.

When a rule changes, change it here first, with a row under [Decisions](#decisions) if the owner made the call. Then change the tokens and the enforcement test, then the views.
