#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The toggle switch and the Settings screen (#218), hosted offscreen like the other accessibility tests.</summary>
    [Collection(WpfCollection.Name)]
    public class SettingsScreenTests
    {
        static void RunSta(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null)
            {
                throw new Exception("STA test failed: " + failure, failure);
            }
        }

        static void EnsureApplication()
        {
            if (Application.Current != null)
            {
                return;
            }

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var name in new[] { "Light", "Icons", "Controls" })
            {
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/keycuts.GUI;component/Themes/" + name + ".xaml")
                });
            }
        }

        static void Pump()
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }

        static IEnumerable<T> Descendants<T>(DependencyObject root)
            where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var nested in Descendants<T>(child))
                {
                    yield return nested;
                }
            }
        }

        /// <summary>The Settings screen in a window of its own, at the narrowest the main window shows it (the large size's minimum width), as tall as its content.</summary>
        private sealed class SettingsHost : Window
        {
            public SettingsHost(SettingsViewModel viewModel)
            {
                ViewModel = viewModel;
                View = new keycuts.GUI.Views.SettingsView { DataContext = viewModel };
                Content = View;
                Width = WindowLayout.ManagerMinWidth;
                SizeToContent = SizeToContent.Height;
            }

            public SettingsViewModel ViewModel { get; }

            public keycuts.GUI.Views.SettingsView View { get; }

            /// <summary>The names belong to the view's name scope, not the window's.</summary>
            public new object FindName(string name)
            {
                return View.FindName(name)!;
            }
        }

        static SettingsHost ShowOffscreen(SettingsViewModel viewModel)
        {
            var window = new SettingsHost(viewModel)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000,
                Top = -20000,
                ShowInTaskbar = false,
                ShowActivated = false
            };
            window.Show();
            Pump();
            window.UpdateLayout();
            return window;
        }

        static SettingsViewModel NewViewModel(string folder)
        {
            var store = new FakeSettingsStore { Stored = new Settings { OutputFolder = folder } };
            return new SettingsViewModel(store);
        }

        private class FakeSettingsStore : ISettingsStore
        {
            public Settings Stored { get; set; } = new Settings();

            public string? SaveMessage { get; set; }

            public int Saves { get; private set; }

            public Settings? Last { get; private set; }

            public Settings Load()
            {
                return Stored;
            }

            public string? SaveOptions(Settings settings)
            {
                Saves++;
                Last = settings;
                return SaveMessage;
            }

            // Counted with the other saves; Last then holds just the folder
            public FolderSaveResult SaveOutputFolder(string folder)
            {
                Saves++;
                Last = new Settings { OutputFolder = folder };
                return new FolderSaveResult(true, null);
            }
        }

        #region Toggle switch

        [Fact]
        public void ToggleSwitch_IsSizedAndColoredFromThePalette()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var toggle = new ToggleButton { Style = (Style)Application.Current.FindResource("ToggleSwitch") };
                var host = new Window { Content = toggle, Width = 200, Height = 100, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowInTaskbar = false, ShowActivated = false };
                host.Show();
                Pump();
                try
                {
                    var track = (Border)toggle.Template.FindName("Track", toggle);
                    var thumb = (System.Windows.Shapes.Ellipse)toggle.Template.FindName("Thumb", toggle);
                    var accent = ((SolidColorBrush)Application.Current.FindResource("AccentBrush")).Color;
                    var surface = ((SolidColorBrush)Application.Current.FindResource("SurfaceBrush")).Color;
                    var onAccent = ((SolidColorBrush)Application.Current.FindResource("OnAccentBrush")).Color;

                    // 24 to click (WCAG 2.5.8), around a 20 track
                    Assert.Equal(36, toggle.ActualWidth);
                    Assert.Equal(24, toggle.ActualHeight);
                    Assert.Equal(20, track.ActualHeight);
                    Assert.Equal(14, thumb.Width);
                    Assert.Equal(14, thumb.Height);
                    Assert.Equal(surface, ((SolidColorBrush)track.Background).Color);
                    Assert.Equal(HorizontalAlignment.Left, thumb.HorizontalAlignment);

                    toggle.IsChecked = true;
                    Pump();
                    Assert.Equal(accent, ((SolidColorBrush)track.Background).Color);
                    Assert.Equal(onAccent, ((SolidColorBrush)thumb.Fill).Color);
                    Assert.Equal(HorizontalAlignment.Right, thumb.HorizontalAlignment);

                    Assert.Same(Application.Current.FindResource("ToggleFocusVisual"), toggle.FocusVisualStyle);
                }
                finally
                {
                    host.Close();
                }
            });
        }

        [Fact]
        public void ToggleSwitch_LetsEnterThroughToTheDefaultButton()
        {
            // A bare ToggleButton handles Enter and flips itself, so Enter on a focused switch would change the setting instead of saving
            RunSta(() =>
            {
                EnsureApplication();
                var toggle = new ToggleButton { Style = (Style)Application.Current.FindResource("ToggleSwitch") };
                var host = new Window { Content = toggle, Width = 200, Height = 100, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowInTaskbar = false, ShowActivated = false };
                host.Show();
                Pump();
                try
                {
                    toggle.Focus();
                    var enter = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(toggle), 0, Key.Enter)
                    {
                        RoutedEvent = Keyboard.KeyDownEvent
                    };
                    toggle.RaiseEvent(enter);
                    Pump();

                    Assert.False(KeyboardNavigation.GetAcceptsReturn(toggle));
                    Assert.False(enter.Handled);
                    Assert.False(toggle.IsChecked);
                }
                finally
                {
                    host.Close();
                }
            });
        }

        [Fact]
        public void ToggleSwitch_ExposesTheTogglePattern()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var toggle = new ToggleButton { Style = (Style)Application.Current.FindResource("ToggleSwitch") };
                AutomationProperties.SetName(toggle, "Ask before replacing a keycut");

                var peer = UIElementAutomationPeer.CreatePeerForElement(toggle);
                var pattern = (IToggleProvider)peer.GetPattern(PatternInterface.Toggle);

                Assert.Equal("Ask before replacing a keycut", peer.GetName());
                Assert.Equal(ToggleState.Off, pattern.ToggleState);
                pattern.Toggle();
                Assert.Equal(ToggleState.On, pattern.ToggleState);
                Assert.True(toggle.IsChecked);
            });
        }

        [Theory]
        [InlineData("Light.xaml")]
        [InlineData("Dark.xaml")]
        [InlineData("HighContrast.xaml")]
        public void ToggleSwitch_OnlyUsesTokensEveryPaletteDefines(string palette)
        {
            XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
            var controls = File.ReadAllText(ThemePath("Controls.xaml"));
            var start = controls.IndexOf("x:Key=\"ToggleSwitch\"", StringComparison.Ordinal);
            var end = controls.IndexOf("</Style>", start, StringComparison.Ordinal);
            var style = controls.Substring(start, end - start);

            var used = Regex.Matches(style, @"DynamicResource (\w+)").Select(m => m.Groups[1].Value).Distinct().ToList();
            var defined = XDocument.Load(ThemePath(palette)).Root!.Elements().Select(e => (string)e.Attribute(xaml + "Key")!).ToHashSet();

            Assert.NotEmpty(used);
            Assert.All(used, key => Assert.Contains(key, defined));
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

        #endregion

        #region Screen (#218)

        [Fact]
        public void Screen_HasNoHeadingNoSaveButton_AndNoNoteAboutSaving()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var window = ShowOffscreen(NewViewModel(Path.GetTempPath()));
                try
                {
                    var texts = Descendants<TextBlock>(window).Where(t => t.IsVisible).Select(t => t.Text).ToList();

                    // The selected tab names the screen, and nothing waits for a Save button
                    Assert.DoesNotContain("Settings", texts);
                    Assert.DoesNotContain(texts, t => t is "Save" or "Cancel");
                    Assert.Null(window.FindName("BtnSave"));
                    Assert.DoesNotContain(Descendants<Button>(window), b => b.IsDefault || b.IsCancel);

                    // Saving as you go goes without saying: no note about it
                    Assert.Null(window.FindName("SavedNote"));
                    Assert.DoesNotContain(texts, t => t.StartsWith("Changes are saved", StringComparison.Ordinal));

                    // The last row: show welcome at the left, restore defaults at the right, both lowercase
                    var welcome = (Button)window.FindName("BtnShowWelcome");
                    var restore = (Button)window.FindName("BtnRestoreDefaults");
                    Assert.Equal("show welcome", welcome.Content);
                    Assert.Equal("restore defaults", restore.Content);
                    var root = (FrameworkElement)window.Content;
                    Assert.True(BoundsIn(welcome, root).Right < BoundsIn(restore, root).Left);
                    // Restore defaults ends at its column's edge, with the toggles above it
                    Assert.Equal(BoundsIn((FrameworkElement)window.FindName("ToggleCheckForUpdates"), root).Right, BoundsIn(restore, root).Right, 1);
                    Assert.Equal(Math.Round(BoundsIn(welcome, root).Top), Math.Round(BoundsIn(restore, root).Top));
                    Assert.Equal("check now", ((Button)window.FindName("BtnCheckNow")).Content);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Screen_FocusesTheFolderField_AndFollowsTheDocumentedTabOrder()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var window = ShowOffscreen(NewViewModel(Path.GetTempPath()));
                try
                {
                    var folder = (TextBox)window.FindName("TextboxOutputFolder");
                    Assert.True(folder.IsKeyboardFocused || folder.IsFocused || folder.IsKeyboardFocusWithin);

                    // Tab visits controls in document order (no TabIndex is used), so the visual tree order is the tab order
                    var stops = Descendants<Control>(window.View)
                        .Where(c => c.IsTabStop && c.Focusable && c.IsEnabled && c.IsVisible && c is not ScrollViewer)
                        .Select(c => AutomationProperties.GetName(c) is { Length: > 0 } name ? name : c is ContentControl { Content: string s } ? s : c.GetType().Name)
                        .ToList();

                    Assert.Equal(
                        new[]
                        {
                            "Output folder", "Choose a folder", "Open the output folder", "Ask before replacing a keycut", "Right-click context menus",
                            "Use environment variables in paths", "System theme", "Light theme", "Dark theme", "Icon: Black & White. Change",
                            "Transparent icon background",
                            "Check for updates daily", "show the welcome panel", "restore defaults"
                        },
                        stops);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        static Rect BoundsIn(FrameworkElement element, FrameworkElement root)
        {
            return new Rect(element.TranslatePoint(new Point(0, 0), root), new Size(element.ActualWidth, element.ActualHeight));
        }

        static KeyEventArgs KeyDown(UIElement target, Key key)
        {
            return new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), 0, key)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };
        }

        // The folder is stored when it is committed: Enter, or leaving the box. Typing alone stores nothing.
        [Fact]
        public void Screen_StoresTheFolderOnEnter_AndWhenTheBoxLosesTheFocus()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var store = new FakeSettingsStore { Stored = new Settings { OutputFolder = Path.GetTempPath() } };
                var first = Path.Combine(Path.GetTempPath(), "keycuts-screen-" + Guid.NewGuid().ToString("N"));
                var second = first + "-2";
                var window = ShowOffscreen(new SettingsViewModel(store));
                try
                {
                    var box = (TextBox)window.FindName("TextboxOutputFolder");
                    box.Focus();
                    Keyboard.Focus(box);
                    box.Text = first;
                    Pump();
                    Assert.Equal(0, store.Saves);

                    var enter = KeyDown(box, Key.Enter);
                    box.RaiseEvent(enter);
                    Assert.True(enter.Handled);
                    Assert.Equal(1, store.Saves);
                    Assert.Equal(first, store.Last!.OutputFolder);

                    box.Text = second;
                    Keyboard.Focus((ToggleButton)window.FindName("ToggleAskBeforeReplacing"));
                    Pump();
                    Assert.Equal(2, store.Saves);
                    Assert.Equal(second, store.Last!.OutputFolder);
                }
                finally
                {
                    window.Close();
                    Directory.Delete(first, true);
                    Directory.Delete(second, true);
                }
            });
        }

        // Escape in the folder box drops an edit and stays; with nothing to drop it goes on to the window (which leaves the screen)
        [Fact]
        public void Screen_EscapeInTheFolderBox_DropsTheEdit_AndStoresNothing()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var stored = Path.GetTempPath();
                var store = new FakeSettingsStore { Stored = new Settings { OutputFolder = stored } };
                var window = ShowOffscreen(new SettingsViewModel(store));
                try
                {
                    var box = (TextBox)window.FindName("TextboxOutputFolder");
                    box.Focus();
                    Keyboard.Focus(box);
                    box.Text = Path.Combine(stored, "typed-but-not-wanted");
                    Pump();

                    var escape = KeyDown(box, Key.Escape);
                    box.RaiseEvent(escape);
                    Pump();

                    Assert.True(escape.Handled);
                    Assert.Equal(stored, box.Text);
                    Assert.Equal(0, store.Saves);
                    Assert.True(box.IsKeyboardFocused);

                    var again = KeyDown(box, Key.Escape);
                    box.RaiseEvent(again);
                    Assert.False(again.Handled);
                    Assert.Equal(0, store.Saves);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // The whole path in the main window: the first Escape drops the edit and stays on Settings, the second returns to the
        // creator, and leaving the box on the way out has nothing to store
        [Fact]
        public void MainWindow_EscapeTwiceFromAnEditedFolderBox_LeavesSettings_WithoutStoringTheFolder()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var stored = Path.GetTempPath();
                var store = new FakeSettingsStore { Stored = new Settings { OutputFolder = stored } };
                var shell = new ShellViewModel(() => System.Threading.Tasks.Task.FromResult(3), settingsFactory: () => new SettingsViewModel(store));
                var window = new MainWindow(shell)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -20000,
                    Top = -20000,
                    ShowInTaskbar = false,
                    ShowActivated = false
                };
                window.Show();
                Pump();
                try
                {
                    shell.ShowSettings();
                    Pump();
                    window.UpdateLayout();
                    var box = Descendants<TextBox>(window).Single(t => t.Name == "TextboxOutputFolder");
                    box.Focus();
                    Keyboard.Focus(box);
                    box.Text = Path.Combine(stored, "typed-but-not-wanted");
                    Pump();

                    box.RaiseEvent(KeyDown(box, Key.Escape));
                    Pump();
                    Assert.Equal(ShellMode.Settings, shell.Mode);
                    Assert.Equal(stored, box.Text);

                    box.RaiseEvent(KeyDown(box, Key.Escape));
                    Pump();
                    Assert.Equal(ShellMode.Creator, shell.Mode);
                    Assert.Equal(0, store.Saves);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Screen_AnInvalidFolder_ShowsItsErrorUnderTheBox_AndStoresNothing()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var store = new FakeSettingsStore { Stored = new Settings { OutputFolder = Path.GetTempPath() } };
                var window = ShowOffscreen(new SettingsViewModel(store));
                try
                {
                    var message = (keycuts.GUI.Controls.HelperText)window.FindName("OutputFolderMessage");
                    var box = (TextBox)window.FindName("TextboxOutputFolder");
                    var root = (FrameworkElement)window.Content;

                    Assert.Equal(SettingsViewModel.DefaultHelperText, message.Text);
                    Assert.Equal(keycuts.GUI.Controls.HelperTextState.Normal, message.State);
                    // The helper line sits under the box, 4 below it
                    Assert.Equal(BoundsIn(box, root).Bottom + 4, BoundsIn(message, root).Top, 1);

                    box.Text = @"relative\folder";
                    box.RaiseEvent(KeyDown(box, Key.Enter));
                    Pump();

                    Assert.Equal(0, store.Saves);
                    Assert.Equal(Settings.InvalidOutputFolderMessage, message.Text);
                    Assert.Equal(keycuts.GUI.Controls.HelperTextState.Error, message.State);
                    Assert.True(keycuts.GUI.Themes.TextBoxChrome.GetHasError(box));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Screen_TheFolderButtons_AreIconOnlySquares_WithNamesAndTooltips()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var window = ShowOffscreen(NewViewModel(Path.GetTempPath()));
                try
                {
                    var choose = (Button)window.FindName("BtnChoose");
                    var launch = (Button)window.FindName("BtnLaunch");
                    var box = (TextBox)window.FindName("TextboxOutputFolder");

                    Assert.Equal("Choose a folder", choose.ToolTip);
                    Assert.Equal("Open the output folder", launch.ToolTip);
                    Assert.Equal("Choose a folder", AutomationProperties.GetName(choose));
                    Assert.Equal("Open the output folder", AutomationProperties.GetName(launch));
                    // Launch is disabled for a missing folder, and a bare icon should still say what it is
                    Assert.True(ToolTipService.GetShowOnDisabled(launch));

                    foreach (var button in new[] { choose, launch })
                    {
                        Assert.Same(Application.Current.FindResource("SquareButton"), button.Style);
                        Assert.Equal(36, button.ActualWidth);
                        Assert.Equal(36, button.ActualHeight);
                        Assert.Empty(Descendants<TextBlock>(button));
                        Assert.Equal(20, Descendants<keycuts.GUI.Controls.Icon>(button).Single().Size);
                    }

                    Assert.Equal(36, box.ActualHeight);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Screen_ThemeChoices_ShareTheLabelsLine_AtTheRightEdge()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var window = ShowOffscreen(NewViewModel(Path.GetTempPath()));
                try
                {
                    var root = (FrameworkElement)window.Content;
                    var label = BoundsIn(Descendants<TextBlock>(window).Single(t => t.Text == "Theme"), root);
                    var choices = (StackPanel)window.FindName("ThemeChoices");
                    var bounds = BoundsIn(choices, root);
                    // The toggle in the same column as the theme choices
                    var toggle = BoundsIn((ToggleButton)window.FindName("ToggleTransparentIcon"), root);

                    Assert.Equal(new[] { ThemePreference.System, ThemePreference.Light, ThemePreference.Dark },
                        choices.Children.OfType<RadioButton>().Select(r => (ThemePreference)r.Tag));
                    Assert.True(Math.Abs((label.Top + label.Height / 2) - (bounds.Top + bounds.Height / 2)) < 2, "Same line");
                    Assert.True(bounds.Left >= label.Right);
                    Assert.Equal(toggle.Right, bounds.Right, 1);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Screen_SectionsAreLabels_WithNoDividers_24Apart()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var window = ShowOffscreen(NewViewModel(Path.GetTempPath()));
                try
                {
                    // The page is two columns at every width, each starting with its section label at the top
                    var body = (Grid)((ScrollViewer)window.FindName("BodyScroller")).Content;
                    var left = (StackPanel)window.FindName("LeftColumn");
                    var right = (StackPanel)window.FindName("RightColumn");
                    Assert.Equal(0, Grid.GetColumn(left));
                    Assert.Equal(2, Grid.GetColumn(right));
                    Assert.Equal(new Thickness(0), right.Margin);

                    foreach (var column in new[] { left, right })
                    {
                        var labels = column.Children.OfType<keycuts.GUI.Controls.SectionLabel>().ToList();
                        Assert.DoesNotContain(column.Children.Cast<UIElement>(), child => child is Border or Separator);
                        Assert.Equal(0, labels[0].Margin.Top);
                        Assert.All(labels.Skip(1), l => Assert.Equal(new Thickness(0, 24, 0, 8), l.Margin));
                    }

                    var all = left.Children.OfType<keycuts.GUI.Controls.SectionLabel>().Concat(right.Children.OfType<keycuts.GUI.Controls.SectionLabel>());
                    Assert.Equal(new[] { "Output", "Behavior", "Appearance", "Updates" }, all.Select(l => l.Text));
                    Assert.Equal((Thickness)Application.Current.FindResource("PagePadding"), body.Margin);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Screen_ShowsTheShortenedText_ButKeepsTheHelpTextForScreenReaders()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var window = ShowOffscreen(NewViewModel(Path.GetTempPath()));
                try
                {
                    var texts = Descendants<TextBlock>(window).Where(t => t.IsVisible).Select(t => t.Text).ToList();

                    Assert.DoesNotContain(texts, t => t.StartsWith("Confirm before a new keycut", StringComparison.Ordinal));
                    Assert.Contains("Check for updates daily", texts);
                    Assert.DoesNotContain(texts, t => t.StartsWith("Once a day", StringComparison.Ordinal));
                    Assert.Contains(@"Save paths as %USERPROFILE%\… or similar. Hold Shift to preview or create a keycut like this, even when disabled.", texts);

                    Assert.Equal("", AutomationProperties.GetHelpText((DependencyObject)window.FindName("ToggleCheckForUpdates")));
                    Assert.Equal("Confirm before a new keycut replaces one with the same name.",
                        AutomationProperties.GetHelpText((DependencyObject)window.FindName("ToggleAskBeforeReplacing")));

                    // "Flip" is right whichever way the setting is: Shift turns it on for one keycut, or off
                    var variables = AutomationProperties.GetHelpText((DependencyObject)window.FindName("ToggleEnvironmentVariables"));
                    Assert.Contains("Hold Shift while creating to flip it for one keycut", variables);
                    Assert.Equal(ConfirmViewModel.RestoreDefaultsIntro, AutomationProperties.GetHelpText((DependencyObject)window.FindName("BtnRestoreDefaults")));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Screen_DisablesLaunch_ForAFolderThatDoesNotExist()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var missing = Path.Combine(Path.GetTempPath(), "keycuts-missing-" + Guid.NewGuid().ToString("N"));
                var window = ShowOffscreen(NewViewModel(missing));
                try
                {
                    var launch = (Button)window.FindName("BtnLaunch");
                    Assert.False(launch.IsEnabled);

                    window.ViewModel.OutputFolder = Path.GetTempPath();
                    Pump();
                    Assert.True(launch.IsEnabled);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Screen_Opening_NeitherAppliesNorStoresATheme_AndAClickDoes()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var store = new FakeSettingsStore { Stored = new Settings { OutputFolder = Path.GetTempPath(), Theme = ThemePreference.Dark } };
                var themes = new List<ThemePreference>();
                var window = ShowOffscreen(new SettingsViewModel(store, applyTheme: themes.Add));
                try
                {
                    var choices = ((StackPanel)window.FindName("ThemeChoices")).Children.OfType<RadioButton>().ToList();
                    Assert.True(choices.Single(c => (ThemePreference)c.Tag == ThemePreference.Dark).IsChecked);
                    Assert.Empty(themes);
                    Assert.Equal(0, store.Saves);

                    choices.Single(c => (ThemePreference)c.Tag == ThemePreference.Light).IsChecked = true;

                    Assert.Equal(new[] { ThemePreference.Light }, themes);
                    Assert.Equal(1, store.Saves);
                    Assert.Equal(ThemePreference.Light, store.Last!.Theme);

                    // Restore defaults moves the radio buttons too, without storing the theme a second time
                    window.ViewModel.RestoreDefaultsCommand.Execute(null);
                    Pump();
                    Assert.True(choices.Single(c => (ThemePreference)c.Tag == ThemePreference.System).IsChecked);
                    Assert.Equal(2, store.Saves);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Screen_ScrollsAStoreErrorIntoView_WhenThePageIsCapped()
        {
            // The error sits low on the page, so a capped window scrolled to the top would hide it
            RunSta(() =>
            {
                EnsureApplication();
                var store = new FakeSettingsStore
                {
                    Stored = new Settings { OutputFolder = Path.GetTempPath() },
                    SaveMessage = Settings.ContextMenuFailedMessage
                };
                var window = ShowOffscreen(new SettingsViewModel(store));
                try
                {
                    window.MaxHeight = 300;
                    Pump();
                    window.UpdateLayout();

                    var scroller = (ScrollViewer)window.FindName("BodyScroller");
                    var message = (TextBlock)window.FindName("SaveErrorMessage");
                    scroller.ScrollToTop();
                    Pump();

                    window.ViewModel.RightClickContextMenus = !window.ViewModel.RightClickContextMenus;
                    Pump();
                    window.UpdateLayout();
                    Pump();

                    var top = message.TranslatePoint(new Point(0, 0), scroller).Y;
                    var bottom = message.TranslatePoint(new Point(0, message.ActualHeight), scroller).Y;
                    Assert.Equal(Settings.ContextMenuFailedMessage, message.Text);
                    Assert.True(scroller.VerticalOffset > 0);
                    Assert.True(top >= 0 && bottom <= scroller.ViewportHeight, $"Message spans {top}-{bottom}, viewport is {scroller.ViewportHeight}");
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Screen_CappedBelowItsContent_ScrollsThePage_AndKeepsTheFooterVisible()
        {
            // What a 1080p screen at 200% scaling (540 DIPs high) does to the screen
            RunSta(() =>
            {
                EnsureApplication();
                var window = ShowOffscreen(NewViewModel(Path.GetTempPath()));
                try
                {
                    window.MaxHeight = 400;
                    Pump();
                    window.UpdateLayout();

                    var scroller = (ScrollViewer)window.FindName("BodyScroller");
                    var footer = (FrameworkElement)window.FindName("Footer");
                    var content = (FrameworkElement)window.Content;
                    var footerBottom = footer.TranslatePoint(new Point(0, footer.ActualHeight), content).Y;

                    Assert.True(window.ActualHeight <= 400);
                    Assert.True(scroller.ScrollableHeight > 0);
                    Assert.True(footerBottom <= content.ActualHeight + 0.5, $"The footer ends at {footerBottom}, content is {content.ActualHeight}");
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // The README's screenshots leave the version row out (the version and check now), so they don't go stale at each
        // release (tools/New-Screenshots.ps1). The button never stays behind without its label.
        [Fact]
        public void Screen_ShowsTheVersionRowUnlessToldNotTo_AsAWhole()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var checker = UpdateFakes.Checker(new FakeReleaseSource(), new FakeUpdateState(), "1.1.1.0");
                var store = new FakeSettingsStore { Stored = new Settings { OutputFolder = Path.GetTempPath() } };
                var shown = new SettingsViewModel(store, updates: checker);
                var hidden = new SettingsViewModel(store, updates: checker) { ShowVersionRow = false };
                Assert.True(shown.ShowVersionRow);

                var window = ShowOffscreen(shown);
                var other = ShowOffscreen(hidden);
                try
                {
                    Assert.True(((TextBlock)window.FindName("CurrentVersionText")).IsVisible);
                    Assert.True(((Button)window.FindName("BtnCheckNow")).IsVisible);

                    Assert.False(((FrameworkElement)other.FindName("VersionRow")).IsVisible);
                    Assert.False(((TextBlock)other.FindName("CurrentVersionText")).IsVisible);
                    Assert.False(((Button)other.FindName("BtnCheckNow")).IsVisible);

                    // Turning it on later shows all of it
                    hidden.ShowVersionRow = true;
                    other.UpdateLayout();
                    Assert.True(((TextBlock)other.FindName("CurrentVersionText")).IsVisible);
                    Assert.True(((Button)other.FindName("BtnCheckNow")).IsVisible);
                }
                finally
                {
                    window.Close();
                    other.Close();
                }
            });
        }

        // #171: check now and the version show with a checker; download only while a newer release is known
        [Fact]
        public void Screen_WithAnUpdateChecker_ShowsCheckNow_AndDownloadOnlyWhenAnUpdateIsKnown()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var state = new FakeUpdateState();
                var checker = UpdateFakes.Checker(new FakeReleaseSource(), state, "1.1.1.0");
                var store = new FakeSettingsStore { Stored = new Settings { OutputFolder = Path.GetTempPath() } };
                var window = ShowOffscreen(new SettingsViewModel(store, updates: checker));
                try
                {
                    var checkNow = (Button)window.FindName("BtnCheckNow");
                    Assert.True(checkNow.IsVisible);
                    Assert.Equal("check for updates now", AutomationProperties.GetName(checkNow));
                    // "Version · 1.1.1", like the icon's row; a screen reader hears "Version 1.1.1"
                    var version = (TextBlock)window.FindName("CurrentVersionText");
                    Assert.Equal("Version · 1.1.1", version.Text);
                    Assert.Equal("Version 1.1.1", AutomationProperties.GetName(version));
                    var runs = version.Inlines.OfType<Run>().ToList();
                    Assert.Equal(FontWeights.SemiBold, runs[0].FontWeight);
                    Assert.Equal(FontWeights.Normal, runs[2].FontWeight);
                    Assert.False(((TextBlock)window.FindName("UpdateStatusMessage")).IsVisible);
                    Assert.False(((Hyperlink)window.FindName("SettingsDownloadLink")).IsEnabled);
                }
                finally
                {
                    window.Close();
                }

                state.LatestReleaseSeen = ("1.2.0", "https://github.com/andrewralon/keycuts/releases/tag/v1.2.0");
                window = ShowOffscreen(new SettingsViewModel(store, updates: checker));
                try
                {
                    Assert.Equal("keycuts 1.2.0 is available", ((TextBlock)window.FindName("UpdateStatusMessage")).Text);
                    Assert.True(((TextBlock)window.FindName("UpdateStatusMessage")).IsVisible);
                    Assert.True(((Hyperlink)window.FindName("SettingsDownloadLink")).IsEnabled);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Screen_WithoutAnUpdateChecker_HidesCheckNow()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var window = ShowOffscreen(NewViewModel(Path.GetTempPath()));
                try
                {
                    Assert.False(((Button)window.FindName("BtnCheckNow")).IsVisible);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Screen_TogglesAreNamedForScreenReaders()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var window = ShowOffscreen(NewViewModel(Path.GetTempPath()));
                try
                {
                    // RadioButton derives from ToggleButton; only the switches are wanted here
                    var toggles = Descendants<ToggleButton>(window).Where(t => t is not RadioButton).ToList();

                    Assert.Equal(
                        new[] { "Ask before replacing a keycut", "Right-click context menus", "Use environment variables in paths", "Transparent icon background", "Check for updates daily" },
                        toggles.Select(AutomationProperties.GetName));
                    Assert.All(toggles, toggle => Assert.NotNull(UIElementAutomationPeer.CreatePeerForElement(toggle).GetPattern(PatternInterface.Toggle)));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Screen_EndsInTheSameFooter_AsTheOtherScreens()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var viewModel = NewViewModel(Path.GetTempPath());
                var footerSource = new CreatorViewModel(new keycuts.GUI.Mvvm.RelayCommand(() => { }), new keycuts.GUI.Mvvm.RelayCommand(() => { }), checkName: TestNameChecks.NoWarnings)
                {
                    KeycutCount = 355
                };
                viewModel.Footer = footerSource;
                var window = ShowOffscreen(viewModel);
                try
                {
                    var footer = (keycuts.GUI.Views.FooterView)window.FindName("Footer");
                    Assert.Same(footerSource, footer.DataContext);
                    Assert.Null(footer.FindName("CountText"));   // the count is the Manager's, beside its search box
                }
                finally
                {
                    window.Close();
                }
            });
        }

        #endregion
    }
}
