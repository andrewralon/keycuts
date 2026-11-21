using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using keycuts.GUI.Themes;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The real <see cref="App"/>, headless: what its tokens and palettes resolve to, and that windows follow a palette change.</summary>
    public class ThemeResourceTests
    {
        static T Find<T>(string key)
        {
            var app = Application.Current!;
            Assert.True(app.TryGetResource(key, app.ActualThemeVariant, out var value), $"No resource {key}");
            return Assert.IsAssignableFrom<T>(value);
        }

        static Color BrushColor(string key)
        {
            return Find<ISolidColorBrush>(key).Color;
        }

        [AvaloniaFact]
        public void Tokens_ResolveToTheirTypes()
        {
            Assert.Equal(36d, Find<double>("ControlHeight"));
            Assert.Equal(new Thickness(24, 16, 24, 24), Find<Thickness>("PagePadding"));
            Assert.Equal(new CornerRadius(12), Find<CornerRadius>("RadiusLg"));
        }

        [AvaloniaFact]
        public void TheFont_IsAPlatformToken()
        {
            var font = Find<FontFamily>("AppFontFamily");
            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(new[] { "Segoe UI Variable", "Segoe UI" }, font.FamilyNames.Select(n => n.Trim()));
            }
            else
            {
                Assert.Equal(FontFamily.Default, font);
            }
        }

        [AvaloniaFact]
        public void Icons_AreGeometriesOnTheir24Grid()
        {
            var bounds = Find<Geometry>("Icon.Settings").Bounds;
            Assert.True(bounds.Width > 0 && bounds.Left >= 0 && bounds.Right <= 24 && bounds.Bottom <= 24, $"Icon.Settings spans {bounds}");
        }

        [AvaloniaFact]
        public void Palettes_SwitchWithTheTheme()
        {
            try
            {
                ThemeManager.Apply(Palette.Light);
                Assert.Equal(Color.Parse("#172033"), BrushColor("TextBrush"));

                ThemeManager.Apply(Palette.Dark);
                Assert.Equal(Palette.Dark, ThemeManager.Current);
                Assert.NotEqual(Color.Parse("#172033"), BrushColor("TextBrush"));
            }
            finally
            {
                ThemeManager.Apply(Palette.Light);
            }
        }

        [AvaloniaFact]
        public void Gradients_AreRelativeToWhatTheyFill()
        {
            var rail = Find<LinearGradientBrush>("RailBrush");
            Assert.Equal(RelativeUnit.Relative, rail.StartPoint.Unit);
            Assert.Equal(new Point(0, 1), rail.EndPoint.Point);
        }

        [AvaloniaFact]
        public void HighContrast_TakesTheWindowsSystemColors()
        {
            var read = ThemeManager.ReadSystemColor;
            try
            {
                ThemeManager.ReadSystemColor = name => name switch
                {
                    SystemColorName.Window => Colors.Black,
                    SystemColorName.WindowText => Colors.Yellow,
                    SystemColorName.HotTrack => Colors.Cyan,
                    SystemColorName.GrayText => Colors.Lime,
                    _ => Colors.Magenta
                };

                ThemeManager.Apply(Palette.HighContrast);

                Assert.Equal(ThemeManager.HighContrastVariant, Application.Current!.ActualThemeVariant);
                Assert.Equal(Colors.Black, BrushColor("CanvasBrush"));
                Assert.Equal(Colors.Yellow, BrushColor("TextBrush"));
                Assert.Equal(Colors.Cyan, BrushColor("AccentTextBrush"));
                Assert.Equal(Colors.Lime, BrushColor("TextTertiaryBrush"));
                // Windows' close button keeps its red in every theme
                Assert.Equal(Color.Parse("#c42b1c"), BrushColor("CaptionCloseBrush"));

                // A change in Windows' colors while high contrast stays on reaches the same brushes
                ThemeManager.ReadSystemColor = name => name == SystemColorName.WindowText ? Colors.White : Colors.Black;
                ThemeManager.Apply(Palette.HighContrast);
                Assert.Equal(Colors.White, BrushColor("TextBrush"));
            }
            finally
            {
                ThemeManager.ReadSystemColor = read;
                ThemeManager.Apply(Palette.Light);
            }
        }

        [AvaloniaFact]
        public void Windows_FollowThePalette()
        {
            var window = new Window();
            try
            {
                ThemeManager.Apply(Palette.Light);
                window.Show();
                Assert.Equal(Color.Parse("#eef1f5"), Assert.IsAssignableFrom<ISolidColorBrush>(window.Background).Color);
                Assert.Equal(Color.Parse("#172033"), Assert.IsAssignableFrom<ISolidColorBrush>(window.Foreground).Color);

                ThemeManager.Apply(Palette.Dark);
                Assert.Equal(BrushColor("CanvasBrush"), Assert.IsAssignableFrom<ISolidColorBrush>(window.Background).Color);
                Assert.NotEqual(Color.Parse("#eef1f5"), Assert.IsAssignableFrom<ISolidColorBrush>(window.Background).Color);
            }
            finally
            {
                window.Close();
                ThemeManager.Apply(Palette.Light);
            }
        }
    }
}
