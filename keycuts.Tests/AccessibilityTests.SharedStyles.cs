#nullable enable
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using keycuts.GUI.Controls;
using Xunit;

namespace keycuts.Tests
{
    // The shared styles and controls docs/styleguide.md asks for (#219 Part 1), rendered with the app's resources
    public partial class AccessibilityTests
    {
        static Window HostElements(params UIElement[] elements)
        {
            var panel = new StackPanel();
            foreach (var element in elements)
            {
                panel.Children.Add(element);
            }

            return Host(panel, new object());
        }

        static T Styled<T>(string key)
            where T : FrameworkElement, new()
        {
            return new T { Style = (Style)Application.Current.FindResource(key) };
        }

        static object AppResource(string key)
        {
            return Application.Current.FindResource(key);
        }

        [Fact]
        public void SectionLabel_DrawsCapitals_ButIsReadInSentenceCase_AsALevel2Heading()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var label = new SectionLabel { Text = "Opens now" };
                var window = HostElements(label);
                try
                {
                    var drawn = Descendants<TextBlock>(label).Single();
                    Assert.Equal("OPENS NOW", drawn.Text);
                    Assert.Same(AppResource("AccentTextBrush"), drawn.Foreground);
                    Assert.Equal(12, drawn.FontSize);
                    Assert.Equal(FontWeights.ExtraBold, drawn.FontWeight);
                    Assert.Equal(new Thickness(0, 0, 0, 8), label.Margin);

                    var peer = UIElementAutomationPeer.CreatePeerForElement(label);
                    Assert.Equal("Opens now", peer.GetName());
                    Assert.Equal(AutomationControlType.Text, peer.GetAutomationControlType());
                    Assert.Equal(AutomationHeadingLevel.Level2, peer.GetHeadingLevel());
                    Assert.Null(peer.GetChildren());
                    Assert.False(label.Focusable);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void SectionLabel_CanBeALevel3Heading_AndIsCappedTheSameEverywhere()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var label = new SectionLabel { Text = "Follows the theme" };
                AutomationProperties.SetHeadingLevel(label, AutomationHeadingLevel.Level3);
                var window = HostElements(label);
                try
                {
                    Assert.Equal(AutomationHeadingLevel.Level3, UIElementAutomationPeer.CreatePeerForElement(label).GetHeadingLevel());
                    Assert.Equal("FOLLOWS THE THEME", label.DisplayText);
                }
                finally
                {
                    window.Close();
                }
            });

            // Not the Turkish dotted capital I, whatever the machine's culture
            Assert.Equal("WILL OPEN", SectionLabel.ToDisplayText("Will open"));
            Assert.Equal(string.Empty, SectionLabel.ToDisplayText(null));
        }

        [Theory]
        [InlineData(HelperTextState.Normal, "TextSecondaryBrush", null, AutomationLiveSetting.Polite)]
        [InlineData(HelperTextState.Warning, "WarningBrush", IconKind.Warning, AutomationLiveSetting.Assertive)]
        [InlineData(HelperTextState.Error, "DangerBrush", null, AutomationLiveSetting.Assertive)]
        [InlineData(HelperTextState.Success, "SuccessBrush", IconKind.Check, AutomationLiveSetting.Polite)]
        public void HelperText_EachStateHasItsColor_ItsIcon_AndItsLiveSetting(HelperTextState state, string brush, IconKind? icon, AutomationLiveSetting live)
        {
            RunSta(() =>
            {
                EnsureApplication();
                var helper = new HelperText { Text = "Opens Notepad.", State = state };
                var window = HostElements(helper);
                try
                {
                    var text = Descendants<TextBlock>(helper).Single();
                    Assert.Equal("Opens Notepad.", text.Text);
                    Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
                    Assert.Equal(12, text.FontSize);
                    Assert.Same(AppResource(brush), helper.Foreground);
                    Assert.Equal(new Thickness(0, 4, 0, 0), helper.Margin);

                    var stateIcon = Descendants<Icon>(helper).Single();
                    Assert.Equal(icon != null, stateIcon.IsVisible);
                    if (icon != null)
                    {
                        Assert.Equal(icon, stateIcon.Kind);
                        Assert.Equal(12, stateIcon.Size);
                    }

                    var peer = UIElementAutomationPeer.CreatePeerForElement(helper);
                    Assert.Equal("Opens Notepad.", peer.GetName());
                    Assert.Equal(live, peer.GetLiveSetting());
                    Assert.Null(peer.GetChildren());
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void SharedTextStyles_HaveTheirSizes_Spacing_AndHeadingLevels()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var heading = Styled<TextBlock>("Heading");
                var subheading = Styled<TextBlock>("Subheading");
                var fieldLabel = Styled<TextBlock>("FieldLabel");
                var error = Styled<TextBlock>("ErrorText");
                var caption = Styled<TextBlock>("Caption");
                heading.Text = subheading.Text = fieldLabel.Text = error.Text = caption.Text = "Sample";
                var window = HostElements(heading, subheading, fieldLabel, error, caption);
                try
                {
                    Assert.Equal((20.0, AutomationHeadingLevel.Level1, 16.0), (heading.FontSize, AutomationProperties.GetHeadingLevel(heading), heading.Margin.Bottom));
                    Assert.Equal((16.0, AutomationHeadingLevel.Level2, 4.0), (subheading.FontSize, AutomationProperties.GetHeadingLevel(subheading), subheading.Margin.Bottom));
                    Assert.Equal(FontWeights.SemiBold, heading.FontWeight);
                    Assert.Equal(FontWeights.SemiBold, subheading.FontWeight);
                    Assert.Equal((13.0, 8.0), (fieldLabel.FontSize, fieldLabel.Margin.Bottom));
                    Assert.Equal(FontWeights.SemiBold, fieldLabel.FontWeight);
                    Assert.Equal((12.0, 4.0), (error.FontSize, error.Margin.Top));
                    Assert.Same(AppResource("DangerBrush"), error.Foreground);
                    Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(error));
                    Assert.Equal(12, caption.FontSize);
                    Assert.Same(AppResource("TextSecondaryBrush"), caption.Foreground);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Buttons_TextButtonsAre36High_AndSquareButtons36Square()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var text = Styled<Button>("SecondaryButton");
                text.Content = "cancel";
                var square = Styled<Button>("SquareButton");
                square.Content = new Icon { Kind = IconKind.Folder, Size = 20 };
                var primary = Styled<Button>("PrimarySquareButton");
                primary.Content = new Icon { Kind = IconKind.Rocket, Size = 20 };
                foreach (var button in new[] { text, square, primary })
                {
                    button.HorizontalAlignment = HorizontalAlignment.Left;
                }

                var window = HostElements(text, square, primary);
                try
                {
                    Assert.Equal(36, text.ActualHeight);
                    Assert.Equal(new Thickness(12, 0, 12, 0), text.Padding);
                    Assert.Equal((36.0, 36.0), (square.ActualWidth, square.ActualHeight));
                    Assert.Equal((36.0, 36.0), (primary.ActualWidth, primary.ActualHeight));
                    Assert.True(ToolTipService.GetShowOnDisabled(square));
                    Assert.Same(AppResource("AccentBrush"), primary.Background);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void ToggleSwitch_IsA24pxPointerTarget_AroundA20pxTrack()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var toggle = new ToggleButton { Style = (Style)AppResource("ToggleSwitch"), HorizontalAlignment = HorizontalAlignment.Left };
                var window = HostElements(toggle);
                try
                {
                    Assert.Equal((36.0, 24.0), (toggle.ActualWidth, toggle.ActualHeight));
                    var track = (Border)toggle.Template.FindName("Track", toggle);
                    Assert.Equal(20, track.ActualHeight);

                    // The transparent area above the track takes the click too
                    var hit = toggle.InputHitTest(new Point(18, 1)) as DependencyObject;
                    Assert.NotNull(hit);
                    Assert.Same(toggle, FindAncestorOrSelf<ToggleButton>(hit!));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void CardItem_SelectionAddsTheAccentBorderAndCheck_WithoutMovingTheContent()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var content = new TextBlock { Text = "Porcelain" };
                var list = new ListBox();
                var item = new ListBoxItem { Style = (Style)AppResource("CardItem"), Content = content, Width = 200 };
                list.Items.Add(item);
                list.Items.Add(new ListBoxItem { Style = (Style)AppResource("CardItem"), Content = "Onyx", Width = 200 });
                var window = HostElements(list);
                try
                {
                    var card = (Border)item.Template.FindName("Card", item);
                    var check = (FrameworkElement)item.Template.FindName("Check", item);
                    Assert.Equal(new Thickness(1), card.BorderThickness);
                    Assert.Equal(new Thickness(12), card.Padding);
                    Assert.Equal(new Thickness(4, 0, 4, 8), item.Margin);
                    Assert.False(check.IsVisible);
                    var before = content.TranslatePoint(new Point(), item);

                    item.IsSelected = true;
                    window.UpdateLayout();

                    Assert.Equal(new Thickness(2), card.BorderThickness);
                    Assert.Equal(new Thickness(11), card.Padding);
                    Assert.Same(AppResource("AccentBrush"), card.BorderBrush);
                    Assert.Same(AppResource("SurfaceBrush"), card.Background);
                    Assert.True(check.IsVisible);
                    Assert.Equal((18.0, 18.0), (check.ActualWidth, check.ActualHeight));
                    Assert.Equal(before, content.TranslatePoint(new Point(), item));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void CardRadio_CheckedLooksLikeASelectedCard()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var radio = new RadioButton { Style = (Style)AppResource("CardRadio"), Content = "g", IsChecked = true };
                var window = HostElements(radio);
                try
                {
                    var card = (Border)radio.Template.FindName("Card", radio);
                    Assert.Equal(new Thickness(2), card.BorderThickness);
                    Assert.Equal(new Thickness(11), card.Padding);
                    Assert.True(((FrameworkElement)radio.Template.FindName("Check", radio)).IsVisible);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void OverlaySheet_TakesItsShadowAndScrimFromThePalette()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var scrim = Styled<Border>("OverlayScrim");
                var sheet = new ContentControl { Style = (Style)AppResource("OverlaySheet"), Content = new TextBlock { Text = "Icon" } };
                var grid = new Grid();
                grid.Children.Add(scrim);
                grid.Children.Add(sheet);
                var window = Host(grid, new object());
                try
                {
                    Assert.Same(AppResource("ScrimBrush"), scrim.Background);
                    Assert.Equal(new Thickness(16, 40, 16, 16), sheet.Margin);
                    var shadow = (Border)sheet.Template.FindName("Shadow", sheet);
                    var effect = Assert.IsType<DropShadowEffect>(shadow.Effect);
                    Assert.Equal((Color)AppResource("ShadowColor"), effect.Color);
                    Assert.Equal(new CornerRadius(12), ((Border)sheet.Template.FindName("Sheet", sheet)).CornerRadius);
                    Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetTabNavigation(sheet));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void LayoutStyles_UseTheTokens()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var tile = Styled<Border>("IconTile");
                var footer = Styled<Border>("DialogFooter");
                var panel = Styled<Border>("InsetPanel");
                var window = HostElements(tile, footer, panel);
                try
                {
                    Assert.Equal((40.0, 40.0), (tile.ActualWidth, tile.ActualHeight));
                    Assert.Equal(new CornerRadius(12), tile.CornerRadius);
                    Assert.Equal(new Thickness(24, 12, 24, 12), footer.Padding);
                    Assert.Equal(new Thickness(0, 1, 0, 0), footer.BorderThickness);
                    Assert.Equal(new Thickness(16), panel.Padding);
                    Assert.Equal(new Thickness(0, 0, 0, 16), panel.Margin);
                    Assert.Equal(new Thickness(24, 16, 24, 24), (Thickness)AppResource("PagePadding"));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        static T? FindAncestorOrSelf<T>(DependencyObject start)
            where T : DependencyObject
        {
            for (var current = start; current != null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is T match)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
