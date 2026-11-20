using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Path = System.IO.Path;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using keycuts.App.Themes;
using keycuts.Tests;
using Xunit;

namespace keycuts.App.Tests
{
    /// <summary>
    /// The shared control themes (Themes/Controls.axaml), headless on the real <see cref="App"/>: each looks and reacts as its WPF
    /// style does (keycuts.GUI/Themes/Controls.xaml), and keycuts' themes win over the Fluent base.
    /// </summary>
    public class ControlThemeTests
    {
        static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        #region Helpers

        static Window Host(Control content)
        {
            ThemeManager.Apply(Palette.Light);
            var window = new Window { Width = 480, Height = 320, Content = content };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            return window;
        }

        static T Part<T>(Visual root, string name) where T : Control
        {
            return root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
        }

        static object Resource(string key)
        {
            var app = Application.Current!;
            Assert.True(app.TryGetResource(key, app.ActualThemeVariant, out var value), $"No resource {key}");
            return value!;
        }

        static ControlTheme Theme(string key)
        {
            return Assert.IsType<ControlTheme>(Resource(key));
        }

        static Color PaletteColor(string key)
        {
            return Assert.IsAssignableFrom<ISolidColorBrush>(Resource(key)).Color;
        }

        static Color ColorOf(IBrush? brush)
        {
            return Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
        }

        static Point Center(Window window, Control control)
        {
            return control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        }

        static void Click(Window window, Control control)
        {
            var point = Center(window, control);
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }

        #endregion

        #region Buttons

        [AvaloniaFact]
        public void APlainButton_HasTheSecondaryLook_AndItsStates()
        {
            var button = new Button { Content = "save", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var window = Host(button);
            try
            {
                var face = Part<ContentPresenter>(button, "PART_ContentPresenter");
                Assert.Equal(new CornerRadius(5), face.CornerRadius);
                Assert.Equal(36, button.Bounds.Height);
                Assert.Equal(FontWeight.SemiBold, button.FontWeight);
                Assert.Equal(PaletteColor("SurfaceBrush"), ColorOf(face.Background));
                Assert.Equal(PaletteColor("ControlBorderBrush"), ColorOf(face.BorderBrush));

                window.MouseMove(Center(window, button), RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(PaletteColor("SurfaceHoverBrush"), ColorOf(face.Background));
                Assert.Equal(PaletteColor("TextSecondaryBrush"), ColorOf(face.BorderBrush));

                window.MouseDown(Center(window, button), MouseButton.Left, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(PaletteColor("AccentSoftBrush"), ColorOf(face.Background));
                window.MouseUp(Center(window, button), MouseButton.Left, RawInputModifiers.None);

                button.IsEnabled = false;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(PaletteColor("SurfaceSubtleBrush"), ColorOf(face.Background));
                Assert.Equal(PaletteColor("TextTertiaryBrush"), ColorOf(button.Foreground));
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaTheory]
        [InlineData("PrimaryButton", "AccentBrush", 36d)]
        [InlineData("PrimarySquareButton", "AccentBrush", 36d)]
        [InlineData("SquareButton", "SurfaceBrush", 36d)]
        public void KeyedButtons_TakeTheirTheme(string theme, string background, double size)
        {
            var button = new Button { Theme = Theme(theme), Content = "x", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var window = Host(button);
            try
            {
                Assert.Equal(PaletteColor(background), ColorOf(Part<ContentPresenter>(button, "PART_ContentPresenter").Background));
                Assert.Equal(size, button.Bounds.Height);
                if (theme.EndsWith("SquareButton", StringComparison.Ordinal))
                {
                    Assert.Equal(size, button.Bounds.Width);
                    Assert.True(ToolTip.GetShowOnDisabled(button));
                }
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void AnIconButton_Is32_AndClearUntilHovered()
        {
            var button = new Button { Theme = Theme("IconButton"), Content = "x", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var window = Host(button);
            try
            {
                Assert.Equal(new Size(32, 32), button.Bounds.Size);
                Assert.Equal(Colors.Transparent, ColorOf(Part<ContentPresenter>(button, "PART_ContentPresenter").Background));

                window.MouseMove(Center(window, button), RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(PaletteColor("SurfaceHoverBrush"), ColorOf(Part<ContentPresenter>(button, "PART_ContentPresenter").Background));
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void TheCloseCaptionButton_TurnsRed()
        {
            var button = new Button { Theme = Theme("CaptionCloseButton"), Content = "x", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var window = Host(button);
            try
            {
                Assert.Equal(new Size(46, 48), button.Bounds.Size);
                Assert.False(button.Focusable);

                window.MouseMove(Center(window, button), RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(Color.Parse("#c42b1c"), ColorOf(button.Background));
                Assert.Equal(Colors.White, ColorOf(button.Foreground));
            }
            finally
            {
                window.Close();
            }
        }

        #endregion

        #region Text boxes

        [AvaloniaFact]
        public void ATextBox_TakesTyping_AndShowsItsPlaceholderOnlyWhileEmpty()
        {
            var textBox = new TextBox { PlaceholderText = "e.g. art", Width = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var window = Host(textBox);
            try
            {
                var placeholder = Part<TextBlock>(textBox, "Placeholder");
                Assert.True(placeholder.IsVisible);
                Assert.Equal("e.g. art", placeholder.Text);
                Assert.Equal(PaletteColor("TextTertiaryBrush"), ColorOf(placeholder.Foreground));
                Assert.Equal(36, textBox.Bounds.Height);
                Assert.Equal(14, textBox.FontSize);

                textBox.Focus();
                window.KeyTextInput("gmail");
                Dispatcher.UIThread.RunJobs();

                Assert.Equal("gmail", textBox.Text);
                Assert.False(placeholder.IsVisible);
                Assert.Equal(PaletteColor("AccentBrush"), ColorOf(Part<Border>(textBox, "Box").BorderBrush));
                Assert.Equal(0.2, Part<Border>(textBox, "Glow").Opacity, 3);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ATextBox_ShowsAWarning_AndAnErrorWins()
        {
            var textBox = new TextBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var other = new Button { Content = "other" };
            var window = Host(new StackPanel { Children = { textBox, other } });
            try
            {
                var box = Part<Border>(textBox, "Box");
                var glow = Part<Border>(textBox, "Glow");

                TextBoxChrome.SetHasWarning(textBox, true);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(PaletteColor("WarningBrush"), ColorOf(box.BorderBrush));

                textBox.Focus();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(PaletteColor("WarningBrush"), ColorOf(box.BorderBrush));
                Assert.Equal(PaletteColor("WarningBrush"), ColorOf(glow.BorderBrush));

                TextBoxChrome.SetHasError(textBox, true);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(PaletteColor("DangerBrush"), ColorOf(box.BorderBrush));
                Assert.Equal(PaletteColor("DangerBrush"), ColorOf(glow.BorderBrush));

                other.Focus();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(PaletteColor("DangerBrush"), ColorOf(box.BorderBrush));
                Assert.Equal(0, glow.Opacity);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ErrorTextBox_StartsInTheErrorState()
        {
            var textBox = new TextBox { Theme = Theme("ErrorTextBox"), Width = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var window = Host(textBox);
            try
            {
                Assert.True(TextBoxChrome.GetHasError(textBox));
                Assert.Equal(PaletteColor("DangerBrush"), ColorOf(Part<Border>(textBox, "Box").BorderBrush));
            }
            finally
            {
                window.Close();
            }
        }

        #endregion

        #region Text

        [AvaloniaTheory]
        [InlineData("Heading", 20d, 1)]
        [InlineData("Subheading", 16d, 2)]
        [InlineData("FieldLabel", 13d, 0)]
        [InlineData("Caption", 12d, 0)]
        [InlineData("ErrorText", 12d, 0)]
        public void TextStyles_HaveTheirSizeAndHeadingLevel(string theme, double size, int headingLevel)
        {
            var text = new TextBlock { Theme = Theme(theme), Text = "Output" };
            var window = Host(text);
            try
            {
                Assert.Equal(size, text.FontSize);
                Assert.Equal(headingLevel, AutomationProperties.GetHeadingLevel(text));
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ErrorText_IsAnnouncedAtOnce()
        {
            var text = new TextBlock { Theme = Theme("ErrorText") };
            var window = Host(text);
            try
            {
                Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(text));
                Assert.Equal(PaletteColor("DangerBrush"), ColorOf(text.Foreground));
            }
            finally
            {
                window.Close();
            }
        }

        #endregion

        #region Toggles, radios, menus, scroll bars

        [AvaloniaFact]
        public void TheToggleSwitch_FillsAndMovesItsThumbWhenOn()
        {
            var toggle = new ToggleButton { Theme = Theme("ToggleSwitch"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var window = Host(toggle);
            try
            {
                Assert.Equal(new Size(36, 24), toggle.Bounds.Size);
                Assert.Equal(HorizontalAlignment.Left, Part<Ellipse>(toggle, "Thumb").HorizontalAlignment);

                Click(window, toggle);

                Assert.True(toggle.IsChecked);
                Assert.Equal(HorizontalAlignment.Right, Part<Ellipse>(toggle, "Thumb").HorizontalAlignment);
                Assert.Equal(PaletteColor("OnAccentBrush"), ColorOf(Part<Ellipse>(toggle, "Thumb").Fill));
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ARadioButton_ShowsItsDotWhenChecked()
        {
            var radio = new RadioButton { Content = "Light", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var window = Host(radio);
            try
            {
                Assert.False(Part<Ellipse>(radio, "Mark").IsVisible);

                Click(window, radio);

                Assert.True(radio.IsChecked);
                Assert.True(Part<Ellipse>(radio, "Mark").IsVisible);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void AMenuItem_ShowsItsKey()
        {
            var item = new MenuItem { Header = "Copy target", InputGesture = new KeyGesture(Key.C, KeyModifiers.Control) };
            var menu = new ContextMenu { Items = { item } };
            var owner = new Button { Content = "row", ContextMenu = menu };
            var window = Host(owner);
            try
            {
                menu.Open(owner);
                Dispatcher.UIThread.RunJobs();

                var key = Part<TextBlock>(item, "PART_InputGestureText");
                Assert.Contains("C", key.Text);
                Assert.Equal(PaletteColor("TextTertiaryBrush"), ColorOf(key.Foreground));
                Assert.True(item.Bounds.Height >= 32);
            }
            finally
            {
                menu.Close();
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ScrollBars_AreSlim()
        {
            var viewer = new ScrollViewer { Height = 100, VerticalScrollBarVisibility = ScrollBarVisibility.Visible, Content = new Border { Height = 1000 } };
            var window = Host(viewer);
            try
            {
                var bar = viewer.GetVisualDescendants().OfType<ScrollBar>().First(b => b.Orientation == Orientation.Vertical);
                Assert.Equal(12, bar.Bounds.Width);
                Assert.True(Part<Track>(bar, "PART_Track").IsDirectionReversed);
                Assert.Single(bar.GetVisualDescendants().OfType<Thumb>());
            }
            finally
            {
                window.Close();
            }
        }

        #endregion

        #region Layout pieces

        [AvaloniaFact]
        public void TheOverlaySheetsShadow_FollowsThePalette()
        {
            var sheet = new ContentControl { Theme = Theme("OverlaySheet"), Content = new TextBlock { Text = "icon" } };
            var window = Host(sheet);
            try
            {
                var shadow = Assert.IsType<DropShadowDirectionEffect>(Part<Border>(sheet, "Shadow").Effect);
                Assert.Equal(0.3, shadow.Opacity, 3);
                Assert.Equal(8, shadow.ShadowDepth);
                Assert.Equal(28, shadow.BlurRadius);

                ThemeManager.Apply(Palette.HighContrast);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(0, Assert.IsType<DropShadowDirectionEffect>(Part<Border>(sheet, "Shadow").Effect).Opacity);
            }
            finally
            {
                window.Close();
                ThemeManager.Apply(Palette.Light);
            }
        }

        [AvaloniaTheory]
        [InlineData("InsetPanel", "SurfaceSubtleBrush")]
        [InlineData("DialogFooter", "SurfaceSubtleBrush")]
        [InlineData("IconTile", "AccentSoftBrush")]
        [InlineData("OverlayScrim", "ScrimBrush")]
        public void Panels_TakeTheirBackground(string theme, string background)
        {
            var border = new Border { Theme = Theme(theme) };
            var window = Host(border);
            try
            {
                Assert.Equal(PaletteColor(background), ColorOf(border.Background));
            }
            finally
            {
                window.Close();
            }
        }

        #endregion

        #region Parity with the WPF app, until cutover

        /// <summary>
        /// WPF styles whose Avalonia theme waits for a control that isn't ported yet: none since Phase 3.3. A style that has to
        /// wait goes here, and comes off as soon as it's ported; the test fails while a listed one is already here.
        /// </summary>
        static readonly HashSet<string> WithTheirControls = new HashSet<string>();

        /// <summary>WPF resources with no Avalonia twin, and why.</summary>
        static readonly Dictionary<string, string> NotNeeded = new Dictionary<string, string>
        {
            ["AppButtonTemplate"] = "BaseButton holds its template",
            ["Label"] = "WPF's Label didn't inherit the window's Foreground; Avalonia's does",
        };

        [Fact]
        public void EveryWpfStyle_IsPorted_OrWaitsForItsControl()
        {
            var wpf = XDocument.Load(RepoFiles.Find(Path.Combine("keycuts.GUI", "Themes", "Controls.xaml"))).Root!.Elements()
                .Where(e => e.Name.LocalName is "Style" or "ControlTemplate")
                .Select(e => Name((string?)e.Attribute(Xaml + "Key") ?? (string)e.Attribute("TargetType")!))
                .ToList();
            var app = XDocument.Load(RepoFiles.Find(Path.Combine("keycuts.App", "Themes", "Controls.axaml"))).Root!.Elements()
                .Where(e => e.Name.LocalName is "ControlTheme" or "FocusAdornerTemplate")
                .Select(e => Name((string)e.Attribute(Xaml + "Key")!))
                .ToList();

            Assert.Empty(wpf.Except(app).Except(WithTheirControls).Except(NotNeeded.Keys));
            Assert.Empty(app.Except(wpf));
            Assert.Empty(WithTheirControls.Intersect(app));
        }

        /// <summary>"{x:Type Button}" and "Button" are Button; "controls:TypeBadge" is TypeBadge; WPF's menu separator key is Separator.</summary>
        static string Name(string key)
        {
            if (key.Contains("SeparatorStyleKey", StringComparison.Ordinal))
            {
                return "Separator";
            }

            var name = key.Replace("{x:Type ", "").TrimEnd('}');
            return name.Contains(':') ? name.Split(':')[1] : name;
        }

        [Fact]
        public void EveryShadow_TakesItsOpacityFromThePalette()
        {
            // High contrast turns shadows off through ShadowOpacity, so a fixed opacity would still draw one there
            var app = Path.GetDirectoryName(RepoFiles.Find(Path.Combine("keycuts.App", "keycuts.App.csproj")))!;
            var shadows = Directory.EnumerateFiles(app, "*.axaml", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .SelectMany(f => XDocument.Load(f).Descendants().Where(e => e.Name.LocalName.StartsWith("DropShadow", StringComparison.Ordinal))
                    .Select(e => (File: Path.GetFileName(f), Opacity: (string?)e.Attribute("Opacity"))))
                .ToList();

            Assert.NotEmpty(shadows);
            Assert.All(shadows, s => Assert.True(s.Opacity == "{DynamicResource ShadowOpacity}", $"{s.File}: a shadow has Opacity=\"{s.Opacity}\""));
        }

        #endregion
    }
}
