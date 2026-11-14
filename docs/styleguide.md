# keycuts style guide

How the keycuts app looks and reads: spacing, layout, type, color, control sizes, corner radii, icons, headings and accessibility, and writing. It covers every screen and dialog: the main window's tab bar and its three screens (the Creator, the Manager and Settings, with its icon picker), the Edit and Confirm dialogs, the shared footer and the welcome panel.

**It describes the app as it will be after #218**, which makes Settings a third screen in the main window, behind folder tabs that replace the title bar. Rules for the Settings dialog, the screen headings and the footer's gear apply until #218 removes them; the [Tabbed window](#tabbed-window-218) section says what changes. #218's mockup (`window-tabs-v5`) is a picture of the layout; where its sizes, colors or type differ from this guide, the guide wins.

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

Tokens live in `keycuts.GUI/Themes/Tokens.xaml` (sizes, type) and the three palettes `Light.xaml`, `Dark.xaml` and `HighContrast.xaml` (brushes, with the same keys in each; `ThemeTests` compares them). Shared styles live in `Themes/Controls.xaml`. Sizes are `StaticResource`; brushes are always `DynamicResource`, so the palette can change while the app runs.

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
| `RailHeight`, `TabHeight`, `CaptionButtonWidth` | none | **new** (#218): 48, 40, 46 | [Tabbed window](#tabbed-window-218) |
| `Rail*`, `Tab*`, `Socket*`, `Caption*` brushes | none | **new** (#218), in all three palettes | The tab bar's colors come from the palettes, not literals |
| `TileSize` | none | **new**: 40 | The inline icon tile ([decision](#decisions)) |
| `TextTertiaryBrush` (Light) | `#818b9d`, 3.0-3.4:1 | **`#646e80`**, 4.54:1 or better on every light surface | Placeholders are text, and WCAG 1.4.3 covers them ([decision](#decisions)); `ControlBorderBrush` keeps `#818b9d` |

Kept as they are: `RadiusSm/Md/Lg/Xl` (5/8/12/16), `Space1`-`Space8` and `Inset1`-`Inset4`, `IconButtonSize` (32), `AppFontFamily` (Segoe UI Variable, Segoe UI), `FontSizeCaption` (12), `FontSizeBody` (13), `FontSizeInput` (14), `FontSizeSubheading` (16), `FontSizeHeading` (20). Add `Inset6` (24) for the outer padding, and `Thickness` tokens for the composite values used below (`PagePadding` 24,16,24,24, `DialogFooterPadding` 24,12, `PanelPadding` 16).

## Spacing

**Everything sits on a 4px grid.** Every margin, padding and gap is a multiple of 4 and comes from `Space*` (a `Double`, for one side or a gap) or `Inset*` (a `Thickness`). The scale:

| Token | px | Typical use |
|---|---|---|
| `Space1` / `Inset1` | 4 | Control to its helper or note; icon to 12px text; between a label and its own note |
| `Space2` / `Inset2` | 8 | Label to its control; between buttons; icon to 13-14px text; section label to its first row |
| `Space3` / `Inset3` | 12 | Between groups inside a panel; text column beside an icon tile; bottom of the Manager's table |
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

Edit and Confirm end in the same footer, one shared style, `DialogFooter`: `SurfaceSubtleBrush` background, a 1px top border in `BorderBrush`, `Padding="24,12"` (`DialogFooterPadding`), buttons right-aligned with 8 between them. The primary button is **last, at the far right**, and cancel comes before it ([decision](#decisions)). A secondary action that isn't cancel sits at the left edge. Enter presses the primary button (`IsDefault`) and Escape cancels (`IsCancel`). Until #218, the Settings dialog uses it too, with show welcome at the left and save at the right. The body above the footer scrolls when the window is capped to the screen, and the footer never does.

### Window widths

- **The Creator and Settings**: `MainWidth`, the width the whole tab bar needs plus about 60 of empty, draggable bar (#218; about 600, measured when it is built). It must also fit two icon cards side by side in Settings' picker, as 500 does today. Both are fixed-width and fit their content's height; Settings stays inside the work area and scrolls when the screen is short.
- **The Manager**: resizable, and remembers its size.
- **Confirm**: 440 (`ConfirmWidth`); **Edit**: 480 (`EditWidth`). They keep their widths while the main window grows ([decision](#decisions)). Dialogs size to their content's height (`SizeToContent="Height"`) and can't be resized.

Until #218 the main window is 440 and the Settings dialog 500.

### The app footer

All three screens end in `FooterView`. It is 48 high at least (`MinHeight`, today 46), with `Padding="24,0"` and a top border in `BorderBrush`, on `SurfaceSubtleBrush`. Left to right: the count pill, the screen's message (the Manager's undo and status line), and the update line, right-aligned. Gaps are 8 between controls and 12 before the message.

Until #218, the footer starts with the Settings gear (a 32 ghost button) and its padding is 16, so the gear's glyph, 6 inside its button, lines up with the content at about 24.

### Inset panels

A panel set into a page (the welcome panel, Confirm's "Opens now / Will open" box) has `RadiusLg` corners, `PanelPadding` (16) and 16 below it. Groups inside it are 12 apart. Its background is `SurfaceSubtleBrush` with a 1px `BorderBrush` border. The welcome panel is the one exception: it is `AccentSoftBrush` with no border, because it is a greeting, not content.

### Overlay sheets and cards

A sheet over a window (today only the icon picker; any future picker follows the same pattern) is one shared pattern, `OverlaySheet` in `Controls.xaml`:

- **Scrim**: `ScrimBrush` over the page and the tabs, which are disabled meanwhile (`IsBodyEnabled`) so Tab stays in the sheet. The caption buttons (minimize, maximize, close) and dragging the window stay available, as on any window ([decision](#decisions)). A click on the scrim closes the sheet.
- **Sheet**: `SurfaceRaisedBrush`, 1px `BorderBrush` border, `RadiusLg` corners, 40 from the top of the window and 16 from the bottom, centered, at most the window's width less 2 × 16.
- **Shadow**: a `DropShadowEffect` in `ShadowColor` (blur 28, depth 8, opacity 0.3) on a separate border behind the sheet, so the sheet's text keeps ClearType.
- **Header**: `Padding="16,16,16,8"`, the title as a level 2 heading at 16 (`Subheading`), an optional note under it (`Caption`, 4 below), and the close button at the top right: an `IconButton` (32) with a 16 `Close` glyph, tooltip "Close", and an automation name that starts with "Close" ("Close the icon picker").
- Escape, the close button and a scrim click all close the sheet. Whether closing undoes anything is up to the sheet; the icon picker applies each pick at once, so closing undoes nothing.

A **card** in a sheet or panel: `SurfaceBrush`, 1px `BorderBrush`, `RadiusMd` corners, `Padding` 12, 4 at the sides and 8 below (cards in a row are then 8 apart), and `SurfaceHoverBrush` on hover. Selected cards follow [Selection](#states-hover-focus-selection-disabled). Card widths are worked out from the list's width so that two fit in a row with 2px to spare (the arithmetic sits in a comment beside `IconCard`).

### Tabbed window (#218)

The main window has no Windows title bar. Its top row is the **tab bar** (#218 calls it the rail), and it does everything a title bar does. The values below are where #218 starts; if they need adjusting once drawn in real pixels, #218 records the final numbers here.

- **Height**: `RailHeight` (48). The tabs stand on its bottom edge; there is no line between the bar and the page.
- **Left to right**: the app icon in a recessed square (28, `RadiusMd`, the icon at 20) and the name `keycuts` (16, SemiBold, 8 from the icon), then 16 (`Space4`) to the first tab, so the name stands apart from the tabs ([decision](#decisions)); the tabs `creator` and `manager` (a 16 icon, 8, then the name at 13 SemiBold, lowercase); the settings tab, **the gear alone** (16, no text; [decision](#decisions)); the empty, draggable bar; minimize, maximize/restore and close.
- **Tabs**: `TabHeight` (40), all the same height, 4 apart at the base. Folder shape: a square left side with an 8 cut corner and a slanted right side. Padding 12 at the left and the slant plus 8 at the right. The selected tab is filled with `SurfaceBrush`, runs into the page, and casts a soft shadow to its right only; its text is `TextBrush`. Inactive tabs are shaded and textured, and their text is `TabTextBrush`, which must meet 4.5:1 on the whole tab. The mockup's colors don't: light `#5d687b` is 3.48:1 at the tab's base and dark `#8d97a8` 4.43:1 at its top; `#465064` and `#9aa4b5` pass. No accent line on the selected tab.
- **Bar**: brushed metal (a vertical gradient with fine horizontal lines) and a faint dot grid in the empty space that fades out at both ends. Every color comes from the `Rail*`, `Tab*` and `Socket*` palette keys. **High contrast drops the textures**: the bar is `Window`, the tabs are outlined in `WindowText`, and the selected tab is marked by its outline running into the page.
- **Caption buttons**: `CaptionButtonWidth` (46) by `RailHeight`, with 10px glyphs: the Windows standard sizes, an exception to the grid and the icon sizes. Hover is a light wash (`CaptionHoverBrush`); close turns `#c42b1c` with a white glyph in both themes, as Windows does (`CaptionCloseBrush`, the one fixed color). Maximize is disabled on the fixed-size Creator and Settings.
- **Accessibility**: the tabs are a tab list. Their names are `creator`, `manager` and `settings` (the gear tab's tooltip too), with their selected state; Tab and the arrow keys reach them. The caption buttons are named `Minimize`, `Maximize` or `Restore`, and `Close`, with matching tooltips. Each screen's content is announced with its tab's name.
- **No screen headings**: the selected tab names the screen, so `keycuts creator`, `keycuts manager` and the creator/manager buttons beside them go, and the Settings screen has no heading. Content starts `PagePadding` (16) below the bar.
- **The Settings screen** follows the rest of this guide: `SectionLabel`s, setting rows 16 apart with no lines, 36 high buttons and text boxes, the output folder's two icon-only square buttons, the note "Changes are saved as you make them." as `Caption` text with a 12 check, and a last row with `show welcome` at the left and `restore defaults` at the right.

### Empty, loading and error states

These are centered in the space the content would take. Top to bottom: an illustration (a 56 tile, `RadiusXl`, `AccentSoftBrush`, with a 32 glyph), 12 gap, a level 2 heading (`Subheading`, 16), 4 gap, one line of `TextSecondaryBrush` body text, 16 gap, then one button (36). An error state's heading is `DangerBrush`. Loading is a spinner and a line of secondary text, 8 apart. Text wraps at 560 at most.

## Type

Font: `AppFontFamily`, Segoe UI Variable with Segoe UI as the fallback. Set once on each window; views inherit it.

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

`HelperText` has state triggers for warning (`WarningBrush` with a 12 `Warning` icon before it), error (`DangerBrush`) and success (`SuccessBrush` with a 12 `Check` icon). It never uses `TextTertiaryBrush`.

**The section label is a control** (`Controls/SectionLabel`, like `TypeBadge`). WPF has no text-transform, so typing `OUTPUT` into the XAML is the only way to get capitals from a `TextBlock`, and screen readers then get capitals too. The control takes sentence-case text (`Text="Output"`), draws it in capitals, and gives sentence case to the automation name, so the source, the tests and Narrator all see "Output". The icon picker's group names (`AppIcons.FollowsThemeGroup`, `AlwaysTheSameGroup`) become sentence case for the same reason.

## Color and contrast

**Colors come only from the palette brushes**, as `DynamicResource`, in every view and style. That keeps light, dark and high contrast working. No hex literal appears outside `Light.xaml`, `Dark.xaml` and `HighContrast.xaml`. `ThemeTests.ControlStyles_DoNotHardCodeColors` already checks `Controls.xaml`, and #219's enforcement test extends that to every view. High contrast maps each key to a system color; a new key needs a high-contrast entry too, and `ThemeTests` fails until it has one.

### Roles

| Brush | Role |
|---|---|
| `TextBrush` | Headings, labels, body text, values the user typed |
| `TextSecondaryBrush` | **All readable supporting text**: helper text, notes, captions, status lines, table cells, column headers, menu item second lines, intros, Settings' "Changes are saved as you make them." |
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
| Ghost icon button (no border: dismiss, sheet close, row menu; the footer's Settings until #218) | 32 × 32 | `IconButton`, `IconButtonSize` |
| Text box editing inside a Manager row (rename, description) | 30 high | The **only** 30; it has to fit a 36 row |
| Manager table: header / row | 36 / 36 (40 if a name plus a 12px marker doesn't fit; #219 checks) | `ManagerTable` |
| Menu item: one line / two lines (Browse menu) | 32 / 48 min | `AppMenuItem`, `BrowseMenuItem` |
| Toggle switch | 36 × 20 drawn, **36 × 24 to click** | `ToggleSwitch` (its template adds a transparent hit area around the track) |
| Check box and radio box | 18 drawn; the label is part of the click area | The implicit `CheckBox` and `RadioButton` styles |
| Type badge, count pill | 22 high | `TypeBadge`, the footer pill |
| Icon tile | 40 × 40, `RadiusLg` | `IconTile` (new), `TileSize` |
| App footer | 48 min | `FooterView` |
| Tab bar, tab, caption button (#218) | 48 / 40 / 46 × 48 | `RailHeight`, `TabHeight`, `CaptionButtonWidth` |

The owner's decision says icon buttons are 32. This guide reads that as the ghost `IconButton`. An icon-only button with a border or fill that sits beside a 36 text box is square at the box's height, so the row lines up. That is today's pattern too (Create 38 beside a 38 box).

**Pointer targets are at least 24 × 24** (WCAG 2.5.8, Target Size (Minimum)). That is the area that takes the click, which isn't always the area that is drawn:

- **By size**: text boxes, text and square buttons (36), ghost icon buttons (32, the row menu button up from 28), menu items, tabs and caption buttons. Check boxes and radio buttons pass with their label, which WPF makes part of the click area; one without a visible label needs a 24 hit area of its own.
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
| 16 | **The default**: in buttons with text, ghost icon buttons (dismiss, close, the row menu), menu items, list warnings, the tabs (the gear included) |
| 20 | Square buttons (Create's rocket, Browse, choose a folder, open the output folder), the app icon in the tab bar, the footer's Settings gear until #218, the welcome bubbles' arrow and the welcome helper line |
| 24 | In a 40 icon tile |
| 32 | In the 56 empty-state illustration |

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

- **Titles and tabs are lowercase**, in the brand style ([decision](#decisions)): the tabs `creator`, `manager` and `settings` (the gear tab's name and tooltip), and the dialog titles, window title and heading alike: `edit keycut`, and Confirm's `replace keycut`, `name already in use` and `restore defaults`. Until #218, `keycuts creator`, `keycuts manager`, `settings` and the nav buttons `creator` and `manager` are lowercase too. A heading that is a sentence or a question rather than a title, such as Confirm's "Replace “npp”?", is sentence case.
- **Button text is lowercase** ([decision](#decisions)), for text buttons and link buttons and their automation names: `cancel`, `save`, `replace`, `create anyway`, `rename anyway`, `check now`, `show welcome`, `restore defaults`, `got it`, `clear search`, `open settings`, `create a keycut`, `undo`, the footer's `download`. Proper names keep their case: `add to PATH`.
- **Sentence case** for everything else: menu items, labels, setting names, level 2 headings, section labels' source text, tooltips, column headers, messages. Capitalize the first word and proper nouns only: "Edit in Notepad", "Open file location", "Use environment variables in paths".
- **The words keycuts and keycut are always lowercase**, the app's name and the noun alike, even at the start of a sentence or label ([decision](#decisions)): "keycuts lets you open your apps…", "keycuts 1.4.1 is available", "keycut name", and "keycuts" as the table's automation name. Today "Keycut name" (two automation names) and "Keycuts" (the table) break this.
- **Section labels and group names are written in sentence case in the source** ("Output", "Follows the theme"). The `SectionLabel` control draws them in capitals.
- Product and platform names keep their own case: Windows, File Explorer, Win+R, PATH, GitHub, Notepad, URL.
- Names the owner chose are kept as written: the icon names and descriptions in `Themes/AppIcons.cs` ("Black & White", "Team Ralon FTW!"), which a test pins.

### Keys

Write key names as Windows does, joined with `+` and no spaces: `Ctrl+Shift+E`, `Shift+F2`, `Win+R`, `Enter`, `Escape`, `Delete`, `Page Up`. In a tooltip, the key goes in parentheses after the action: "Undo the delete (Ctrl+Z)". Menus put it in `InputGestureText`.

### Ellipsis

Use the single character `…` (U+2026), never three dots, in two cases only:

- A **command that asks for more input before it acts**: the Browse menu's "File…" and "Folder…" (they open a picker), and "Edit…" in the row menu (it opens a form; today it says "Edit"). Rename, Delete and Open act at once and take none.
- **Work in progress**: "Saving…", "Loading keycuts…", "Checking for updates…".

Not elsewhere: the Creator's target placeholder "Drop or paste a file, folder, URL ..." becomes "Drop or paste a file, folder, or URL".

### Punctuation

- **No period** on labels, buttons, menu items, tooltips that are fragments, column headers, headings and placeholders.
- **Periods** on full sentences: notes, helper text, messages, errors. A setting's note is a sentence ("Once a day, ask GitHub whether a newer version is out.").
- **Exclamation marks** only in the four places that have them ([decision](#decisions)): "Welcome to keycuts!", the Explorer verb "keycut this!", "Created. Now try it out!" and the Classic icon's description. Nowhere else.
- **Apostrophes and quotes** in the GUI are typographic: `’` and `“ ”`. Today the GUI mixes them with `'` ("Can't load your keycuts", "Couldn't delete"). The CLI's console output stays plain ASCII, because the console's code page may not show them.
- **Em dash** without spaces around it ("dashes, or dots—no \ / :"). Better still, rewrite the sentence without one.
- The serial (Oxford) comma: "a file, folder, or URL".

### Placeholders

A placeholder is a hint inside an empty box, in `TextTertiaryBrush` (4.5:1, like all text). Anything people need goes in the label or helper text instead. The Name and Description fields use **examples, the same in both forms** ([decision](#decisions)): "e.g. art" and "e.g. Recent paintings". The Edit form changes from "What you type in Win+R" and "What is it for? Shown in the manager". "e.g." with no comma after it is allowed in placeholders only, where space is tight; elsewhere write "for example". A placeholder that isn't an example says what goes there ("Search names, types, descriptions, or targets", "A file, folder, or URL").

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
| 2026-11-13 | Dialog button order | Cancel, then primary at the far right | **Keep it**: primary last, at the far right. A deliberate difference from Fluent's ContentDialog, which puts the primary button first |

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
- **`SettingsWindow.xaml`** (#218 moves it into a screen; build that screen to these values rather than restyling the dialog): heading gap 22 → 16; section gap 28 → 24; setting-name-to-note gap 2 → 4; toggle and button left margin 20 → 16; save error 14 above → 16; Choose and Open folder become 36 square buttons with 16 → 20 glyphs; the icon button padding 5 → 4; icon picker per the sheet pattern (radius 10 → 12, header margin `18,16,12,8` → `16,16,16,8`, 14 below → 16, card padding 10 → 12 and margins `5,0,5,10` → `4,0,4,8`, the check badge 7 in → 8).
- **`ConfirmWindow.xaml`**: header 18 below → 16, text 14 from the tile → 12; targets box padding `14,12` → 16 and 14 below → 16; second section label 12 above → 12 (kept) and 4 below → 8; warning icons 15 → 16.
- **`EditWindow.xaml`**: heading gap 20 → 16; label gap 6 → 8; error gap 6 → 4; save error 14 above → 16.
- **Edit and Confirm** (and Settings until #218): body margin 24 → `PagePadding` (16 at the top); footer `Padding="24,13"` → the shared `DialogFooter` (`24,12`).

## Tracking

- #211 wrote this guide.
- #218 (the tabbed window) follows it: its tab bar, the Settings screen and the footer are built to these rules from the start.
- #219, "Update the app to follow the style guide", holds the checklist of changes by screen and an enforcement test that reads the GUI's XAML and fails on a literal font size, a hex color outside the palettes, a margin, padding or radius off the grid or outside the tokens, `FontWeight="Bold"`, or a heading or section label without its `HeadingLevel`.

**Order of work**: (1) #219 Part 1, the foundation: tokens, shared styles, `SectionLabel` and the contrast tests, which change little on screen; (2) #218, in two PRs, first the window frame and tabs, then Settings as a screen, both built with those styles; (3) #219 Part 2, the screen-by-screen cleanup of what #218 doesn't rebuild (the Creator's body, the Manager, Edit, Confirm, the footer), then the enforcement test switched on and the screenshots regenerated once.

When a rule changes, change it here first, with a row under [Decisions](#decisions) if the owner made the call. Then change the tokens and the enforcement test, then the views.
