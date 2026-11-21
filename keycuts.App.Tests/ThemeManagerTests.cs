using Avalonia.Platform;
using Avalonia.Styling;
using keycuts.Common;
using keycuts.GUI.Themes;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>Which palette the app shows (WPF's ThemeTests.Resolve_*, ported): high contrast, then the user's choice, then the platform.</summary>
    public class ThemeManagerTests
    {
        [Theory]
        [InlineData(PlatformThemeVariant.Dark, false, Palette.Dark)]
        [InlineData(PlatformThemeVariant.Light, false, Palette.Light)]
        [InlineData(null, false, Palette.Light)]
        [InlineData(PlatformThemeVariant.Dark, true, Palette.HighContrast)]
        [InlineData(PlatformThemeVariant.Light, true, Palette.HighContrast)]
        [InlineData(null, true, Palette.HighContrast)]
        public void Resolve_PicksThePaletteFromThePlatform(PlatformThemeVariant? platformTheme, bool highContrast, Palette expected)
        {
            Assert.Equal(expected, ThemeManager.Resolve(platformTheme, highContrast));
        }

        [Theory]
        [InlineData(PlatformThemeVariant.Dark, ThemePreference.Light, Palette.Light)]
        [InlineData(PlatformThemeVariant.Light, ThemePreference.Light, Palette.Light)]
        [InlineData(null, ThemePreference.Light, Palette.Light)]
        [InlineData(PlatformThemeVariant.Dark, ThemePreference.Dark, Palette.Dark)]
        [InlineData(PlatformThemeVariant.Light, ThemePreference.Dark, Palette.Dark)]
        [InlineData(null, ThemePreference.Dark, Palette.Dark)]
        [InlineData(PlatformThemeVariant.Dark, ThemePreference.System, Palette.Dark)]
        [InlineData(PlatformThemeVariant.Light, ThemePreference.System, Palette.Light)]
        public void Resolve_TheUsersChoiceBeatsThePlatform_UnlessItIsSystem(PlatformThemeVariant? platformTheme, ThemePreference preference, Palette expected)
        {
            Assert.Equal(expected, ThemeManager.Resolve(platformTheme, false, preference));
        }

        [Theory]
        [InlineData(ThemePreference.Light)]
        [InlineData(ThemePreference.Dark)]
        [InlineData(ThemePreference.System)]
        public void Resolve_HighContrastBeatsTheUsersChoice(ThemePreference preference)
        {
            Assert.Equal(Palette.HighContrast, ThemeManager.Resolve(PlatformThemeVariant.Light, true, preference));
        }

        [Theory]
        [InlineData(ColorContrastPreference.High, true, true)]
        [InlineData(ColorContrastPreference.NoPreference, true, false)]
        [InlineData(ColorContrastPreference.High, false, false)]
        public void IsHighContrast_IsThePlatformsHighContrast_WhereKeycutsFollowsIt(ColorContrastPreference contrast, bool follows, bool expected)
        {
            var values = new PlatformColorValues { ThemeVariant = PlatformThemeVariant.Light, ContrastPreference = contrast };
            Assert.Equal(expected, ThemeManager.IsHighContrast(values, follows));
            Assert.False(ThemeManager.IsHighContrast(null, follows));
        }

        [Fact]
        public void DetectPalette_IsHighContrast_WhenMacIncreaseContrastIsOn_AndNotBefore()
        {
            var read = ThemeManager.ReadMacIncreaseContrast;
            try
            {
                // Avalonia reports no contrast preference on macOS, so the app reads Increase contrast itself; it is off by default
                Assert.False(ThemeManager.ReadMacIncreaseContrast());
                Assert.NotEqual(Palette.HighContrast, ThemeManager.DetectPalette());

                ThemeManager.ReadMacIncreaseContrast = () => true;
                Assert.Equal(Palette.HighContrast, ThemeManager.DetectPalette());
            }
            finally
            {
                ThemeManager.ReadMacIncreaseContrast = read;
            }
        }

        // macOS's Increase contrast (Phase 5.4): fixed colors, each text color 4.5:1 or better on the background it sits on
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void OnTheMac_HighContrastColors_AreFixed_AndReadable(bool dark)
        {
            string Hex(SystemColorName name)
            {
                var c = SystemColor.ReadMac(name, dark);
                return $"#{c.R:x2}{c.G:x2}{c.B:x2}";
            }

            Assert.Equal(dark ? "#000000" : "#ffffff", Hex(SystemColorName.Window));
            foreach (var text in new[] { SystemColorName.WindowText, SystemColorName.GrayText, SystemColorName.HotTrack, SystemColorName.Highlight })
            {
                Assert.True(ColorContrast.Ratio(Hex(text), Hex(SystemColorName.Window)) >= 4.5, text.ToString());
            }

            Assert.True(ColorContrast.Ratio(Hex(SystemColorName.HighlightText), Hex(SystemColorName.Highlight)) >= 4.5);
        }

        [Fact]
        public void EveryPalette_HasItsOwnThemeVariant()
        {
            Assert.Equal(ThemeVariant.Light, ThemeManager.GetThemeVariant(Palette.Light));
            Assert.Equal(ThemeVariant.Dark, ThemeManager.GetThemeVariant(Palette.Dark));
            Assert.Equal(ThemeManager.HighContrastVariant, ThemeManager.GetThemeVariant(Palette.HighContrast));
            Assert.Equal(ThemeVariant.Light, ThemeManager.HighContrastVariant.InheritVariant);
        }

        [Theory]
        // GetSysColor's indexes (winuser.h COLOR_*), the colors WPF's SystemColors read
        [InlineData(SystemColorName.Window, 5)]
        [InlineData(SystemColorName.WindowText, 8)]
        [InlineData(SystemColorName.HotTrack, 26)]
        [InlineData(SystemColorName.GrayText, 17)]
        [InlineData(SystemColorName.Control, 15)]
        [InlineData(SystemColorName.Highlight, 13)]
        [InlineData(SystemColorName.HighlightText, 14)]
        public void SystemColor_ReadsTheSameColorsAsWpf(SystemColorName name, int index)
        {
            Assert.Equal(index, SystemColor.Index(name));
        }
    }
}
