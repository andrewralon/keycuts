using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
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
                "TextTertiaryBrush", "BorderBrush", "BorderStrongBrush", "DangerBrush", "DangerSoftBrush", "SuccessBrush"
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
        }

        [Fact]
        public void DarkPalette_UsesPrototypeHexValues()
        {
            var colors = LoadColors("Dark.xaml");
            Assert.Equal("#4f8cff", colors["AccentBrush"]);
            Assert.Equal("#14171d", colors["CanvasBrush"]);
            Assert.Equal("#f3f5f8", colors["TextBrush"]);
            Assert.Equal("#ff8175", colors["DangerBrush"]);
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

                foreach (var surface in surfaces.Where(s => s != "AccentSoftBrush"))
                {
                    yield return new object[] { palette, "TextSecondaryBrush", surface, 4.5 };
                    yield return new object[] { palette, "DangerBrush", surface, 4.5 };
                    yield return new object[] { palette, "SuccessBrush", surface, 4.5 };
                }

                yield return new object[] { palette, "DangerBrush", "DangerSoftBrush", 4.5 };
                yield return new object[] { palette, "OnAccentBrush", "AccentBrush", 4.5 };
                yield return new object[] { palette, "OnAccentBrush", "AccentHoverBrush", 4.5 };
                yield return new object[] { palette, "OnAccentBrush", "AccentPressedBrush", 4.5 };

                // Non-text UI: control boundaries, accent focus ring, secondary and placeholder text (3:1)
                foreach (var surface in new[] { "CanvasBrush", "SurfaceBrush", "SurfaceSubtleBrush", "SurfaceRaisedBrush" })
                {
                    yield return new object[] { palette, "ControlBorderBrush", surface, 3.0 };
                    yield return new object[] { palette, "AccentBrush", surface, 3.0 };
                    yield return new object[] { palette, "TextTertiaryBrush", surface, 3.0 };
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
            return XDocument.Load(ThemePath(file)).Root!.Elements()
                .ToDictionary(e => (string)e.Attribute(Xaml + "Key")!, e => ((string)e.Attribute("Color")!).ToLowerInvariant());
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
