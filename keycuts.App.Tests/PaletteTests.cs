using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using keycuts.GUI.Themes;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// The app's tokens, palettes and icons, read from their files (WPF's ThemeTests, ported). They matched the WPF app's
    /// value for value until the cutover (Phase 3.12).
    /// </summary>
    public class PaletteTests
    {
        static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        static readonly string[] Palettes = { "Light", "Dark", "HighContrast" };

        [Fact]
        public void Palettes_DefineTheSameKeys()
        {
            var light = LoadKeys(AppTheme("Light"));
            Assert.Equal(light.OrderBy(k => k), LoadKeys(AppTheme("Dark")).OrderBy(k => k));
            Assert.Equal(light.OrderBy(k => k), LoadKeys(AppTheme("HighContrast")).OrderBy(k => k));
        }

        [Fact]
        public void Palettes_IncludeTheRequiredTokens()
        {
            var required = new[]
            {
                "AccentBrush", "AccentHoverBrush", "AccentPressedBrush", "AccentSoftBrush", "CanvasBrush", "SurfaceBrush",
                "SurfaceSubtleBrush", "SurfaceHoverBrush", "SurfaceRaisedBrush", "TextBrush", "TextSecondaryBrush",
                "TextTertiaryBrush", "BorderBrush", "BorderStrongBrush", "DangerBrush", "DangerSoftBrush", "SuccessBrush", "WarningBrush",
                "AccentTextBrush", "OnAccentBrush", "ControlBorderBrush", "ScrimBrush", "ShadowColor",
                // The tab bar (#218)
                "RailBrush", "RailTextureBrush", "RailGripBrush", "TabBrush", "TabTextureBrush", "TabBorderBrush", "TabSelectedBorderBrush",
                "TabBaseBrush", "TabTextBrush", "SocketBrush", "SocketBorderBrush", "CaptionHoverBrush", "CaptionPressedBrush",
                "CaptionHoverGlyphBrush", "CaptionCloseBrush", "CaptionCloseGlyphBrush"
            };

            foreach (var palette in Palettes)
            {
                Assert.Empty(required.Except(LoadKeys(AppTheme(palette))));
            }
        }

        [Fact]
        public void HighContrastPalette_HasNoScrimOrShadow_AndAccentTextIsTheLinkColor()
        {
            var colors = LoadColors(AppTheme("HighContrast"));
            Assert.Equal("transparent", colors["ScrimBrush"]);
            Assert.Equal("transparent", colors["ShadowColor"]);
            Assert.Equal("0", colors["ShadowOpacity"]);
            Assert.Equal("0.3", LoadColors(AppTheme("Light"))["ShadowOpacity"]);
            Assert.Equal("0.3", LoadColors(AppTheme("Dark"))["ShadowOpacity"]);
            Assert.Equal("system:hottrack", colors["AccentTextBrush"]);
        }

        [Fact]
        public void HighContrastPalette_NamesOnlySystemColorsThereAre()
        {
            var names = XDocument.Load(AppTheme("HighContrast")).Root!.Elements()
                .Select(e => e.Attributes().FirstOrDefault(a => a.Name.LocalName == "SystemColor.Name"))
                .Where(a => a != null)
                .Select(a => a!.Value)
                .ToList();

            Assert.NotEmpty(names);
            Assert.All(names, name => Assert.True(Enum.TryParse<SystemColorName>(name, out _), $"{name} isn't a SystemColorName"));
        }

        [Fact]
        public void Gradients_RunTopToBottomOfWhatTheyFill()
        {
            // Avalonia reads "0,1" as pixels, where WPF read it relative to the shape: a gradient's points must be percentages
            foreach (var palette in Palettes)
            {
                foreach (var gradient in XDocument.Load(AppTheme(palette)).Descendants().Where(e => e.Name.LocalName == "LinearGradientBrush"))
                {
                    Assert.Equal("0%,0%", (string?)gradient.Attribute("StartPoint"));
                    Assert.Equal("0%,100%", (string?)gradient.Attribute("EndPoint"));
                }
            }
        }

        public static IEnumerable<object[]> ContrastPairs()
        {
            var surfaces = new[] { "CanvasBrush", "SurfaceBrush", "SurfaceSubtleBrush", "SurfaceHoverBrush", "SurfaceRaisedBrush", "AccentSoftBrush" };
            foreach (var palette in new[] { "Light", "Dark" })
            {
                // Body text (4.5:1)
                foreach (var surface in surfaces)
                {
                    yield return new object[] { palette, "TextBrush", surface, 4.5 };
                }

                // Accent-colored text (section labels, links, the active sort header) can sit on any surface
                foreach (var surface in surfaces)
                {
                    yield return new object[] { palette, "AccentTextBrush", surface, 4.5 };
                }

                // The welcome panel's text
                yield return new object[] { palette, "TextSecondaryBrush", "AccentSoftBrush", 4.5 };

                foreach (var surface in surfaces.Where(s => s != "AccentSoftBrush"))
                {
                    yield return new object[] { palette, "TextSecondaryBrush", surface, 4.5 };
                    yield return new object[] { palette, "DangerBrush", surface, 4.5 };
                    yield return new object[] { palette, "SuccessBrush", surface, 4.5 };
                    yield return new object[] { palette, "WarningBrush", surface, 4.5 };
                }

                // The tab bar (#218). A tab and the bar are gradients, so the text has to pass at both ends: inactive tab text on the whole tab,
                // the name `keycuts` on the whole bar, and the caption glyphs as non-text UI (3:1). Close is white on red in both themes.
                foreach (var end in new[] { "#0", "#1" })
                {
                    yield return new object[] { palette, "TabTextBrush", "TabBrush" + end, 4.5 };
                    yield return new object[] { palette, "TextBrush", "RailBrush" + end, 4.5 };
                    yield return new object[] { palette, "TextBrush", "SocketBrush" + end, 3.0 };
                }

                yield return new object[] { palette, "CaptionCloseGlyphBrush", "CaptionCloseBrush", 4.5 };

                yield return new object[] { palette, "DangerBrush", "DangerSoftBrush", 4.5 };
                yield return new object[] { palette, "OnAccentBrush", "AccentBrush", 4.5 };
                yield return new object[] { palette, "OnAccentBrush", "AccentHoverBrush", 4.5 };
                yield return new object[] { palette, "OnAccentBrush", "AccentPressedBrush", 4.5 };

                foreach (var surface in new[] { "CanvasBrush", "SurfaceBrush", "SurfaceSubtleBrush", "SurfaceRaisedBrush" })
                {
                    // Non-text UI: control boundaries and the accent focus ring (3:1)
                    yield return new object[] { palette, "ControlBorderBrush", surface, 3.0 };
                    yield return new object[] { palette, "AccentBrush", surface, 3.0 };

                    // Tertiary text is only placeholders, disabled text and decorative glyphs; placeholders are text (WCAG 1.4.3)
                    yield return new object[] { palette, "TextTertiaryBrush", surface, 4.5 };
                }
            }
        }

        [Theory]
        [MemberData(nameof(ContrastPairs))]
        public void Palettes_MeetWcagContrast(string palette, string foreground, string background, double minimum)
        {
            var colors = LoadColors(AppTheme(palette));
            var ratio = ColorContrast.Ratio(colors[foreground], colors[background]);
            Assert.True(ratio >= minimum, $"{palette}: {foreground} on {background} is {ratio:0.00}:1, needs {minimum}:1");
        }

        [Fact]
        public void ContrastRatio_MatchesKnownValues()
        {
            Assert.Equal(21.0, ColorContrast.Ratio("#000000", "#ffffff"), 2);
            Assert.Equal(1.0, ColorContrast.Ratio("#336699", "#336699"), 5);
            Assert.Throws<FormatException>(() => ColorContrast.Ratio("#fff", "#000000"));
        }

        // The font is a platform token (docs/styleguide.md, In Avalonia): Segoe UI on Windows, the system font elsewhere
        [Fact]
        public void Tokens_TheFontIsAPlatformToken()
        {
            var font = XDocument.Load(AppTheme("Tokens")).Root!.Elements().Single(e => (string?)e.Attribute(Xaml + "Key") == "AppFontFamily");
            Assert.Equal("OnPlatform", font.Name.LocalName);
            Assert.Equal("Segoe UI Variable, Segoe UI", (string?)font.Attribute("Windows"));
            Assert.Equal("$Default", (string?)font.Attribute("Default"));
        }

        static HashSet<string> LoadKeys(string path)
        {
            return XDocument.Load(path).Root!.Elements().Select(e => (string)e.Attribute(Xaml + "Key")!).ToHashSet();
        }

        /// <summary>
        /// Each key's color, lowercase: a brush's Color attribute, or a Color's (or Double's) text. A system color reads
        /// "system:hottrack", from WPF's <c>SystemColors.HotTrackColorKey</c> or the app's <c>SystemColor.Name="HotTrack"</c>. A
        /// gradient is listed by its stops, as "TabBrush#0" (the top) and "TabBrush#1" (the bottom); a texture has no color of its own.
        /// </summary>
        static Dictionary<string, string> LoadColors(string path)
        {
            var colors = new Dictionary<string, string>();
            foreach (var e in XDocument.Load(path).Root!.Elements())
            {
                var key = (string)e.Attribute(Xaml + "Key")!;
                var color = (string?)e.Attribute("Color");
                var systemName = (string?)e.Attributes().FirstOrDefault(a => a.Name.LocalName == "SystemColor.Name");
                var wpfSystem = color == null ? null : Regex.Match(color, @"SystemColors\.(\w+)ColorKey");

                colors[key] = (systemName != null ? "system:" + systemName
                    : wpfSystem is { Success: true } ? "system:" + wpfSystem.Groups[1].Value
                    : color ?? (e.HasElements ? "" : e.Value.Trim())).ToLowerInvariant();

                if (e.Name.LocalName == "LinearGradientBrush")
                {
                    var stops = e.Elements().Where(c => c.Name.LocalName == "GradientStop").OrderBy(c => (double)c.Attribute("Offset")!).ToList();
                    for (var i = 0; i < stops.Count; i++)
                    {
                        colors[key + "#" + i] = ((string)stops[i].Attribute("Color")!).ToLowerInvariant();
                    }
                }
            }

            return colors;
        }

        /// <summary>Each token's value: an element's text (<c>&lt;x:Double&gt;</c>, <c>&lt;Thickness&gt;</c>, <c>&lt;CornerRadius&gt;</c>).</summary>
        static Dictionary<string, string> LoadValues(string path)
        {
            return XDocument.Load(path).Root!.Elements().ToDictionary(e => (string)e.Attribute(Xaml + "Key")!, e => e.Value.Trim());
        }

        static string AppTheme(string name)
        {
            return RepoFiles.Find(Path.Combine("keycuts.App", "Themes", name + ".axaml"));
        }
    }
}
