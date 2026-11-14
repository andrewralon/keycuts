using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using keycuts.Common;
using keycuts.GUI.Themes;
using Xunit;

namespace keycuts.Tests
{
    public class ThemeTests
    {
        static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        [Theory]
        [InlineData(0, false, ThemeVariant.Dark)]
        [InlineData(1, false, ThemeVariant.Light)]
        [InlineData(null, false, ThemeVariant.Light)]
        [InlineData(0, true, ThemeVariant.HighContrast)]
        [InlineData(1, true, ThemeVariant.HighContrast)]
        [InlineData(null, true, ThemeVariant.HighContrast)]
        public void Resolve_PicksVariantFromWindowsSettings(int? appsUseLightTheme, bool highContrast, ThemeVariant expected)
        {
            Assert.Equal(expected, ThemeManager.Resolve(appsUseLightTheme, highContrast));
        }

        [Theory]
        [InlineData(0, ThemePreference.Light, ThemeVariant.Light)]
        [InlineData(1, ThemePreference.Light, ThemeVariant.Light)]
        [InlineData(null, ThemePreference.Light, ThemeVariant.Light)]
        [InlineData(0, ThemePreference.Dark, ThemeVariant.Dark)]
        [InlineData(1, ThemePreference.Dark, ThemeVariant.Dark)]
        [InlineData(null, ThemePreference.Dark, ThemeVariant.Dark)]
        [InlineData(0, ThemePreference.System, ThemeVariant.Dark)]
        [InlineData(1, ThemePreference.System, ThemeVariant.Light)]
        public void Resolve_TheUsersChoiceBeatsWindows_UnlessItIsSystem(int? appsUseLightTheme, ThemePreference preference, ThemeVariant expected)
        {
            Assert.Equal(expected, ThemeManager.Resolve(appsUseLightTheme, false, preference));
        }

        [Theory]
        [InlineData(ThemePreference.Light)]
        [InlineData(ThemePreference.Dark)]
        [InlineData(ThemePreference.System)]
        public void Resolve_HighContrastBeatsTheUsersChoice(ThemePreference preference)
        {
            Assert.Equal(ThemeVariant.HighContrast, ThemeManager.Resolve(0, true, preference));
        }

        [Theory]
        [InlineData("Light", ThemePreference.Light)]
        [InlineData("dark", ThemePreference.Dark)]
        [InlineData("System", ThemePreference.System)]
        [InlineData(null, ThemePreference.System)]
        [InlineData("", ThemePreference.System)]
        [InlineData("1", ThemePreference.System)]
        [InlineData("Sepia", ThemePreference.System)]
        public void ThemePreference_ParsesSavedValues_AndFallsBackToSystem(string value, ThemePreference expected)
        {
            Assert.Equal(expected, ThemePreferences.Parse(value));
        }

        [Fact]
        public void ThemePreference_RoundTripsThroughItsSavedName()
        {
            foreach (var preference in Enum.GetValues<ThemePreference>())
            {
                Assert.Equal(preference, ThemePreferences.Parse(preference.ToString()));
            }
        }

        [Theory]
        [InlineData(ThemeVariant.Light, "Light.xaml")]
        [InlineData(ThemeVariant.Dark, "Dark.xaml")]
        [InlineData(ThemeVariant.HighContrast, "HighContrast.xaml")]
        public void GetPaletteFileName_MapsEveryVariant(ThemeVariant variant, string expected)
        {
            Assert.Equal(expected, ThemeManager.GetPaletteFileName(variant));
        }

        [Fact]
        public void ContrastRatio_MatchesKnownValues()
        {
            Assert.Equal(21.0, ColorContrast.Ratio("#000000", "#ffffff"), 2);
            Assert.Equal(1.0, ColorContrast.Ratio("#336699", "#336699"), 5);
            Assert.Equal(ColorContrast.Ratio("#2563eb", "#ffffff"), ColorContrast.Ratio("#ffffff", "#2563eb"), 10);
        }

        [Fact]
        public void ContrastRatio_RejectsMalformedColors()
        {
            Assert.Throws<FormatException>(() => ColorContrast.Ratio("#fff", "#000000"));
        }

        [Fact]
        public void Palettes_DefineTheSameKeys()
        {
            var light = LoadKeys("Light.xaml");
            Assert.Equal(light.OrderBy(k => k), LoadKeys("Dark.xaml").OrderBy(k => k));
            Assert.Equal(light.OrderBy(k => k), LoadKeys("HighContrast.xaml").OrderBy(k => k));
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

            foreach (var file in new[] { "Light.xaml", "Dark.xaml", "HighContrast.xaml" })
            {
                Assert.Empty(required.Except(LoadKeys(file)));
            }
        }

        [Fact]
        public void LightPalette_UsesPrototypeHexValues()
        {
            var colors = LoadColors("Light.xaml");
            Assert.Equal("#2563eb", colors["AccentBrush"]);
            Assert.Equal("#eef1f5", colors["CanvasBrush"]);
            Assert.Equal("#172033", colors["TextBrush"]);
            Assert.Equal("#c42b1c", colors["DangerBrush"]);
            Assert.Equal("#2563eb", colors["AccentTextBrush"]);
            Assert.Equal("#646e80", colors["TextTertiaryBrush"]);
            Assert.Equal("#818b9d", colors["ControlBorderBrush"]);
            Assert.Equal("#6b0f172a", colors["ScrimBrush"]);
            Assert.Equal("#0f172a", colors["ShadowColor"]);
        }

        [Fact]
        public void DarkPalette_UsesPrototypeHexValues()
        {
            var colors = LoadColors("Dark.xaml");
            Assert.Equal("#4f8cff", colors["AccentBrush"]);
            Assert.Equal("#14171d", colors["CanvasBrush"]);
            Assert.Equal("#f3f5f8", colors["TextBrush"]);
            Assert.Equal("#ff8175", colors["DangerBrush"]);
            Assert.Equal("#6a9eff", colors["AccentTextBrush"]);
            Assert.Equal("#99000000", colors["ScrimBrush"]);
            Assert.Equal("#000000", colors["ShadowColor"]);
        }

        [Fact]
        public void HighContrastPalette_HasNoScrimOrShadow_AndAccentTextIsTheLinkColor()
        {
            var colors = LoadColors("HighContrast.xaml");
            Assert.Equal("transparent", colors["ScrimBrush"]);
            Assert.Equal("transparent", colors["ShadowColor"]);
            // DropShadowEffect ignores a color's alpha, so only the opacity turns a shadow off (#224 review)
            Assert.Equal("0", colors["ShadowOpacity"]);
            Assert.Equal("0.3", LoadColors("Light.xaml")["ShadowOpacity"]);
            Assert.Equal("0.3", LoadColors("Dark.xaml")["ShadowOpacity"]);
            Assert.Contains("HotTrackColorKey".ToLowerInvariant(), colors["AccentTextBrush"]);
        }

        public static IEnumerable<object[]> ContrastPairs()
        {
            var surfaces = new[] { "CanvasBrush", "SurfaceBrush", "SurfaceSubtleBrush", "SurfaceHoverBrush", "SurfaceRaisedBrush", "AccentSoftBrush" };
            foreach (var palette in new[] { "Light.xaml", "Dark.xaml" })
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

                yield return new object[] { palette, "TextBrush", "SurfaceBrush", 4.5 };
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

                    // Tertiary text is only placeholders, disabled text and decorative glyphs (the search icon). Placeholders are
                    // text, and WCAG 1.4.3 covers them even beside a label, so it meets 4.5:1 like any other (docs/styleguide.md)
                    yield return new object[] { palette, "TextTertiaryBrush", surface, 4.5 };
                }
            }
        }

        [Theory]
        [MemberData(nameof(ContrastPairs))]
        public void Palettes_MeetWcagContrast(string palette, string foreground, string background, double minimum)
        {
            var colors = LoadColors(palette);
            var ratio = ColorContrast.Ratio(colors[foreground], colors[background]);
            Assert.True(ratio >= minimum, $"{palette}: {foreground} on {background} is {ratio:0.00}:1, needs {minimum}:1");
        }

        [Fact]
        public void EveryShadow_TakesItsOpacityFromThePalette()
        {
            // A shadow with a fixed opacity would still show in high contrast, where ShadowColor is Transparent: the effect ignores alpha
            var gui = Path.GetDirectoryName(Path.GetDirectoryName(ThemePath("Controls.xaml")))!;
            var shadows = Directory.EnumerateFiles(gui, "*.xaml", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .SelectMany(f => XDocument.Load(f).Descendants().Where(e => e.Name.LocalName == "DropShadowEffect").Select(e => (File: Path.GetFileName(f), Opacity: (string)e.Attribute("Opacity"))))
                .ToList();

            Assert.NotEmpty(shadows);
            Assert.All(shadows, s => Assert.True(s.Opacity == "{DynamicResource ShadowOpacity}", $"{s.File}: a DropShadowEffect has Opacity=\"{s.Opacity}\""));
        }

        [Fact]
        public void ControlStyles_DoNotHardCodeColors()
        {
            var controls = File.ReadAllText(ThemePath("Controls.xaml"));
            Assert.DoesNotContain("#", controls.Replace("&#", string.Empty));
        }

        static HashSet<string> LoadKeys(string file)
        {
            return XDocument.Load(ThemePath(file)).Root!.Elements().Select(e => (string)e.Attribute(Xaml + "Key")!).ToHashSet();
        }

        static Dictionary<string, string> LoadColors(string file)
        {
            // A brush has a Color attribute; a Color (ShadowColor) is the element's own text. A gradient is listed by its stops, as
            // "TabBrush#0" (the top) and "TabBrush#1" (the bottom), so a contrast pair can name either end; a texture has no color of its own.
            var colors = new Dictionary<string, string>();
            foreach (var e in XDocument.Load(ThemePath(file)).Root!.Elements())
            {
                var key = (string)e.Attribute(Xaml + "Key")!;
                colors[key] = ((string)e.Attribute("Color") ?? e.Value.Trim()).ToLowerInvariant();

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

        static string ThemePath(string file)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "keycuts.sln")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "keycuts.GUI", "Themes", file);
        }
    }
}
