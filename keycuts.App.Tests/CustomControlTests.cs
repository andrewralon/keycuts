using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using keycuts.App.Controls;
using keycuts.App.Themes;
using keycuts.Common;
using keycuts.Tests;
using Xunit;
using Path = System.IO.Path;

namespace keycuts.App.Tests
{
    /// <summary>
    /// The custom controls of Phase 3.3a (Icon, SectionLabel, HelperText, TypeBadge), and the themes that draw them (the check
    /// box, the check badge, the cards): WPF's IconTests, TypeBadgeTests and the shared-style accessibility tests, ported.
    /// </summary>
    public class CustomControlTests
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

        static object Resource(string key)
        {
            var app = Application.Current!;
            Assert.True(app.TryGetResource(key, app.ActualThemeVariant, out var value), $"No resource {key}");
            return value!;
        }

        static Color PaletteColor(string key)
        {
            return Assert.IsAssignableFrom<ISolidColorBrush>(Resource(key)).Color;
        }

        static Color ColorOf(IBrush? brush)
        {
            return Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
        }

        static IEnumerable<T> Descendants<T>(Visual root)
        {
            return root.GetVisualDescendants().OfType<T>();
        }

        static Dictionary<string, string> IconFigures()
        {
            return XDocument.Load(RepoFiles.Find(Path.Combine("keycuts.App", "Themes", "Icons.axaml"))).Root!.Elements()
                .ToDictionary(e => (string)e.Attribute(Xaml + "Key")!, e => e.Value.Trim());
        }

        #endregion

        #region Icon

        [Fact]
        public void IconsAxaml_HasAGeometryForEveryIconKind_AndNoOthers()
        {
            var expected = Enum.GetValues<IconKind>().Select(Icon.GetResourceKey).OrderBy(k => k);
            Assert.Equal(expected, IconFigures().Keys.OrderBy(k => k));
        }

        [AvaloniaFact]
        public void EveryIcon_ParsesAndFitsTheViewBox()
        {
            foreach (var (key, figures) in IconFigures())
            {
                var bounds = Geometry.Parse(figures).Bounds;
                Assert.True(bounds.Width > 1 && bounds.Height > 1, key);
                Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= Icon.ViewBoxSize && bounds.Bottom <= Icon.ViewBoxSize, $"{key} {bounds}");
            }
        }

        [AvaloniaFact]
        public void TheSettingsGear_IsCenteredInTheViewBox()
        {
            var bounds = Geometry.Parse(IconFigures()[Icon.GetResourceKey(IconKind.Settings)]).Bounds;
            Assert.Equal(Icon.ViewBoxSize / 2, bounds.Left + bounds.Width / 2, 0.25);
            Assert.Equal(Icon.ViewBoxSize / 2, bounds.Top + bounds.Height / 2, 0.25);
        }

        [Fact]
        public void OnlyMore_IsFilled_AndUnknownKindsAreRejected()
        {
            Assert.Equal(new[] { IconKind.More }, Enum.GetValues<IconKind>().Where(Icon.IsFilled));
            Assert.Throws<ArgumentOutOfRangeException>(() => Icon.GetResourceKey((IconKind)999));
        }

        [AvaloniaFact]
        public void AnIcon_IsItsSize_FindsItsGeometry_AndIsInvisibleToScreenReaders()
        {
            var icon = new Icon { Kind = IconKind.Rocket, Size = 20, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var window = Host(icon);
            try
            {
                Assert.Equal(new Size(20, 20), icon.Bounds.Size);
                Assert.NotNull(icon.FindGeometry());
                Assert.False(icon.IsHitTestVisible);
                Assert.IsType<NoneAutomationPeer>(ControlAutomationPeer.CreatePeerForElement(icon));
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaTheory]
        [InlineData(IconKind.Rocket)]
        [InlineData(IconKind.More)]
        public void AnIcon_DrawsInItsForeground(IconKind kind)
        {
            var icon = new Icon { Kind = kind, Size = 24, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var window = Host(new Border { Background = Brushes.White, Child = icon });
            try
            {
                using var frame = window.CaptureRenderedFrame()!;
                using var pixels = frame.Lock();
                var dark = 0;
                unsafe
                {
                    for (var y = 0; y < 24; y++)
                    {
                        var row = (byte*)pixels.Address + y * pixels.RowBytes;
                        for (var x = 0; x < 24; x++)
                        {
                            // BGRA or RGBA: a dark pixel is dark in every channel
                            if (row[x * 4] < 128 && row[x * 4 + 1] < 128 && row[x * 4 + 2] < 128)
                            {
                                dark++;
                            }
                        }
                    }
                }

                // The three "more" dots cover about 21 px; past antialiasing, about 12 are more than half dark
                Assert.True(dark > 8, $"{kind} drew {dark} dark pixels");
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void AnIcon_TakesTheForegroundAroundIt()
        {
            var icon = new Icon { Kind = IconKind.Check };
            var panel = new Border { Child = icon };
            TextElementForeground(panel, Brushes.Red);
            var window = Host(panel);
            try
            {
                Assert.Equal(Colors.Red, ColorOf(icon.Foreground));
            }
            finally
            {
                window.Close();
            }
        }

        static void TextElementForeground(Control control, IBrush brush)
        {
            control.SetValue(Avalonia.Controls.Documents.TextElement.ForegroundProperty, brush);
        }

        #endregion

        #region SectionLabel

        [AvaloniaFact]
        public void ASectionLabel_DrawsCapitals_ButIsReadInSentenceCase_AsALevel2Heading()
        {
            var label = new SectionLabel { Text = "Opens now" };
            var window = Host(label);
            try
            {
                var drawn = Descendants<TextBlock>(label).Single();
                Assert.Equal("OPENS NOW", drawn.Text);
                Assert.Equal(PaletteColor("AccentTextBrush"), ColorOf(drawn.Foreground));
                Assert.Equal(12, drawn.FontSize);
                Assert.Equal(FontWeight.ExtraBold, drawn.FontWeight);
                Assert.Equal(new Thickness(0, 0, 0, 8), label.Margin);

                var peer = ControlAutomationPeer.CreatePeerForElement(label);
                Assert.Equal("Opens now", peer.GetName());
                Assert.Equal(AutomationControlType.Text, peer.GetAutomationControlType());
                Assert.Equal(2, peer.GetHeadingLevel());
                Assert.Empty(peer.GetChildren());
                Assert.False(label.Focusable);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ASectionLabel_CanBeALevel3Heading_AndIsCappedTheSameEverywhere()
        {
            var label = new SectionLabel { Text = "Follows the theme" };
            AutomationProperties.SetHeadingLevel(label, 3);
            var window = Host(label);
            try
            {
                Assert.Equal(3, ControlAutomationPeer.CreatePeerForElement(label).GetHeadingLevel());
                Assert.Equal("FOLLOWS THE THEME", label.DisplayText);
            }
            finally
            {
                window.Close();
            }

            // Not the Turkish dotted capital I, whatever the machine's culture
            Assert.Equal("WILL OPEN", SectionLabel.ToDisplayText("Will open"));
            Assert.Equal(string.Empty, SectionLabel.ToDisplayText(null));
        }

        #endregion

        #region HelperText

        [AvaloniaTheory]
        [InlineData(HelperTextState.Normal, "TextSecondaryBrush", null, AutomationLiveSetting.Polite)]
        [InlineData(HelperTextState.Warning, "WarningBrush", IconKind.Warning, AutomationLiveSetting.Assertive)]
        [InlineData(HelperTextState.Error, "DangerBrush", null, AutomationLiveSetting.Assertive)]
        [InlineData(HelperTextState.Success, "SuccessBrush", IconKind.Check, AutomationLiveSetting.Polite)]
        public void HelperText_EachStateHasItsColor_ItsIcon_AndItsLiveSetting(HelperTextState state, string brush, IconKind? icon, AutomationLiveSetting live)
        {
            var helper = new HelperText { Text = "Opens Notepad.", State = state };
            var window = Host(helper);
            try
            {
                var text = Descendants<TextBlock>(helper).Single();
                Assert.Equal("Opens Notepad.", text.Text);
                Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
                Assert.Equal(12, text.FontSize);
                Assert.Equal(PaletteColor(brush), ColorOf(helper.Foreground));
                Assert.Equal(new Thickness(0, 4, 0, 0), helper.Margin);

                var stateIcon = Descendants<Icon>(helper).Single();
                Assert.Equal(icon != null, stateIcon.IsVisible);
                if (icon != null)
                {
                    Assert.Equal(icon, stateIcon.Kind);
                    Assert.Equal(12, stateIcon.Size);
                }

                var peer = ControlAutomationPeer.CreatePeerForElement(helper);
                Assert.Equal("Opens Notepad.", peer.GetName());
                Assert.Equal(live, peer.GetLiveSetting());
                Assert.Empty(peer.GetChildren());
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void HelperText_RaisesANameChangeForEachNewText()
        {
            // What Avalonia turns into a live region announcement on Windows (UIA LiveRegionChanged); WPF needed LiveRegion.Announce
            var helper = new HelperText { Text = "Opens Notepad." };
            var window = Host(helper);
            try
            {
                var peer = ControlAutomationPeer.CreatePeerForElement(helper);
                var names = new List<object?>();
                peer.PropertyChanged += (_, e) =>
                {
                    if (e.Property == AutomationElementIdentifiers.NameProperty)
                    {
                        names.Add(e.NewValue);
                    }
                };

                helper.State = HelperTextState.Warning;
                helper.Text = "Windows opens “npp” from System32 first.";
                helper.Text = string.Empty;

                Assert.Equal(new object?[] { "Windows opens “npp” from System32 first.", "" }, names);
            }
            finally
            {
                window.Close();
            }
        }

        #endregion

        #region TypeBadge

        [Theory]
        [InlineData(KeycutKind.Url, "URL", IconKind.Link, "Type: URL")]
        [InlineData(KeycutKind.Folder, "Folder", IconKind.Folder, "Type: Folder")]
        [InlineData(KeycutKind.File, "File", IconKind.File, "Type: File")]
        [InlineData(KeycutKind.Command, "Command", IconKind.Play, "Type: Command")]
        public void EachKind_HasLabelIconAndAutomationName(KeycutKind kind, string label, IconKind icon, string automationName)
        {
            Assert.Equal(label, TypeBadge.GetLabel(kind));
            Assert.Equal(icon, TypeBadge.GetIconKind(kind));
            Assert.Equal(automationName, TypeBadge.GetAutomationName(kind));
        }

        [Fact]
        public void EveryKind_HasADistinctIconAndLabel_AndUndefinedKindsAreRejected()
        {
            var kinds = Enum.GetValues<KeycutKind>();
            Assert.Equal(kinds.Length, kinds.Select(TypeBadge.GetLabel).Distinct().Count());
            Assert.Equal(kinds.Length, kinds.Select(TypeBadge.GetIconKind).Distinct().Count());
            Assert.Throws<ArgumentOutOfRangeException>(() => TypeBadge.GetLabel((KeycutKind)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => TypeBadge.GetIconKind((KeycutKind)99));
        }

        [AvaloniaTheory]
        [InlineData(KeycutKind.Url, "Url")]
        [InlineData(KeycutKind.File, "File")]
        [InlineData(KeycutKind.Folder, "Folder")]
        [InlineData(KeycutKind.Command, "Command")]
        public void ABadge_TakesItsKindsColors_AndDrawsItsIconAndLabel(KeycutKind kind, string brushes)
        {
            var badge = new TypeBadge { Kind = kind };
            var window = Host(badge);
            try
            {
                Assert.Equal(22, badge.Bounds.Height);
                Assert.Equal(PaletteColor($"Badge{brushes}ForegroundBrush"), ColorOf(badge.Foreground));
                Assert.Equal(PaletteColor($"Badge{brushes}BackgroundBrush"), ColorOf(badge.Background));
                Assert.Equal(TypeBadge.GetIconKind(kind), Descendants<Icon>(badge).Single().Kind);
                Assert.Equal(TypeBadge.GetLabel(kind), Descendants<TextBlock>(badge).Single().Text);
                Assert.Equal(TypeBadge.GetAutomationName(kind), ControlAutomationPeer.CreatePeerForElement(badge).GetName());
            }
            finally
            {
                window.Close();
            }
        }

        [Theory]
        [InlineData("Light")]
        [InlineData("Dark")]
        public void BadgeLabels_MeetAaContrastOnTheirBackground(string palette)
        {
            var colors = XDocument.Load(RepoFiles.Find(Path.Combine("keycuts.App", "Themes", palette + ".axaml"))).Root!.Elements()
                .Where(e => e.Attribute("Color") != null)
                .ToDictionary(e => (string)e.Attribute(Xaml + "Key")!, e => ((string)e.Attribute("Color")!).ToLowerInvariant());
            foreach (var kind in new[] { "Url", "File", "Folder", "Command" })
            {
                var ratio = ColorContrast.Ratio(colors[$"Badge{kind}ForegroundBrush"], colors[$"Badge{kind}BackgroundBrush"]);
                Assert.True(ratio >= 4.5, $"{palette}: {kind} badge is {ratio:0.00}:1");
            }
        }

        #endregion

        #region The themes that draw them

        [AvaloniaFact]
        public void ACheckBox_ShowsItsCheckWhenChecked()
        {
            var box = new CheckBox { Content = "Use environment variables in paths", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var window = Host(box);
            try
            {
                var mark = Descendants<Icon>(box).Single();
                Assert.False(mark.IsVisible);
                Assert.Equal(IconKind.Check, mark.Kind);

                var point = box.TranslatePoint(new Point(9, box.Bounds.Height / 2), window)!.Value;
                window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
                window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();

                Assert.True(box.IsChecked);
                Assert.True(mark.IsVisible);
                Assert.Equal(PaletteColor("OnAccentBrush"), ColorOf(mark.Foreground));
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ASelectedCard_GetsTheAccentBorderAndTheCheckBadge_WithoutMoving()
        {
            var cardTheme = Assert.IsType<Avalonia.Styling.ControlTheme>(Resource("CardItem"));
            var list = new ListBox
            {
                ItemContainerTheme = cardTheme,
                ItemsSource = new[] { "Porcelain", "Onyx" }
            };
            var window = Host(list);
            try
            {
                var items = Descendants<ListBoxItem>(list).ToList();
                Assert.Equal(2, items.Count);
                var card = Descendants<Border>(items[0]).First(b => b.Name == "Card");
                var check = Descendants<ContentControl>(items[0]).First(c => c.Name == "Check");
                Assert.False(check.IsVisible);
                Assert.Equal(new Thickness(12), card.Padding);

                list.SelectedIndex = 0;
                Dispatcher.UIThread.RunJobs();

                Assert.True(check.IsVisible);
                Assert.Equal(PaletteColor("AccentBrush"), ColorOf(card.BorderBrush));
                Assert.Equal(new Thickness(2), card.BorderThickness);
                Assert.Equal(new Thickness(11), card.Padding);
                Assert.Equal(IconKind.Check, Descendants<Icon>(check).Single().Kind);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ACheckedCardRadio_GetsTheCheckBadge()
        {
            var radio = new RadioButton { Theme = Assert.IsType<Avalonia.Styling.ControlTheme>(Resource("CardRadio")), Content = "npp" };
            var window = Host(radio);
            try
            {
                var check = Descendants<ContentControl>(radio).First(c => c.Name == "Check");
                Assert.False(check.IsVisible);

                radio.IsChecked = true;
                Dispatcher.UIThread.RunJobs();

                Assert.True(check.IsVisible);
                Assert.Equal(9, Descendants<Border>(check).First().CornerRadius.TopLeft);
            }
            finally
            {
                window.Close();
            }
        }

        #endregion
    }
}
