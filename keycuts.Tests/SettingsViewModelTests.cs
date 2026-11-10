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
    /// <summary>The Settings window's rules. The store is a fake and folders live under a temp root: nothing touches HKCU or the real output folder.</summary>
    public class SettingsViewModelTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "keycuts-settings-tests-" + Guid.NewGuid().ToString("N"));

        public SettingsViewModelTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private class FakeStore : ISettingsStore
        {
            public Settings Stored { get; set; } = new Settings
            {
                OutputFolder = @"C:\Shortcuts",
                ForceOverwrite = true,
                RightClickContextMenus = false,
                UseEnvironmentVariables = true,
                Theme = ThemePreference.Dark
            };

            public List<Settings> Saves { get; } = new List<Settings>();

            public string? SaveMessage { get; set; }

            public Settings Load()
            {
                return Stored;
            }

            public string? Save(Settings settings)
            {
                Saves.Add(settings);
                return SaveMessage;
            }
        }

        private SettingsViewModel NewViewModel(FakeStore store, Func<string, string?>? pick = null, Action<string>? open = null)
        {
            return new SettingsViewModel(store, pick, open);
        }

        #region Load and save mapping

        [Fact]
        public void Load_MapsEveryStoredValue()
        {
            var vm = NewViewModel(new FakeStore());

            Assert.Equal(@"C:\Shortcuts", vm.OutputFolder);
            Assert.True(vm.ForceOverwrite);
            Assert.False(vm.RightClickContextMenus);
            Assert.True(vm.UseEnvironmentVariables);
            Assert.Equal(ThemePreference.Dark, vm.Theme);
            Assert.Equal(SettingsViewModel.DefaultHelperText, vm.OutputFolderMessage);
            Assert.False(vm.HasOutputFolderError);
        }

        [Fact]
        public void Save_WritesEveryValueToTheStore_AndRaisesSaved()
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            var folder = Path.Combine(root, "keys");
            var saved = 0;
            vm.Saved += (sender, e) => saved++;

            vm.OutputFolder = "  " + folder + "  ";
            vm.ForceOverwrite = false;
            vm.RightClickContextMenus = true;
            vm.UseEnvironmentVariables = false;
            vm.Theme = ThemePreference.Light;

            Assert.True(vm.Save());

            var result = Assert.Single(store.Saves);
            Assert.Equal(folder, result.OutputFolder);
            Assert.False(result.ForceOverwrite);
            Assert.True(result.RightClickContextMenus);
            Assert.False(result.UseEnvironmentVariables);
            Assert.Equal(ThemePreference.Light, result.Theme);
            Assert.Equal(1, saved);
            Assert.True(Directory.Exists(folder));
        }

        [Fact]
        public void EditingWithoutSaving_NeverReachesTheStore()
        {
            // Cancel and Escape just close the window, so an unsaved view model must leave the store alone
            var store = new FakeStore();
            var vm = NewViewModel(store);

            vm.OutputFolder = Path.Combine(root, "elsewhere");
            vm.ForceOverwrite = false;
            vm.RightClickContextMenus = true;
            vm.Theme = ThemePreference.Light;

            Assert.Empty(store.Saves);
            Assert.False(Directory.Exists(Path.Combine(root, "elsewhere")));
        }

        [Fact]
        public void Save_ReportsAStoreMessageInline_AndStaysOpen()
        {
            var store = new FakeStore { SaveMessage = Settings.PathUpdateFailedMessage };
            var vm = NewViewModel(store);
            vm.OutputFolder = Path.Combine(root, "keys");
            var saved = 0;
            vm.Saved += (sender, e) => saved++;

            Assert.False(vm.Save());

            Assert.Equal(Settings.PathUpdateFailedMessage, vm.SaveError);
            Assert.True(vm.HasSaveError);
            Assert.Equal(0, saved);
        }

        [Fact]
        public void StoreChanged_IsSetOnceASaveReachesTheStore_EvenWhenItReportsAProblem()
        {
            // The folder can be stored before PATH fails, so the main window must refresh even if the dialog is then cancelled
            var store = new FakeStore { SaveMessage = Settings.PathUpdateFailedMessage };
            var vm = NewViewModel(store);
            Assert.False(vm.StoreChanged);

            vm.OutputFolder = string.Empty;
            vm.Save();
            Assert.False(vm.StoreChanged);

            vm.OutputFolder = Path.Combine(root, "keys");
            Assert.False(vm.Save());
            Assert.True(vm.StoreChanged);
        }

        #endregion

        #region Validation

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Save_RejectsAnEmptyFolder(string folder)
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            vm.OutputFolder = folder;

            Assert.False(vm.Save());

            Assert.Equal(SettingsViewModel.EmptyFolderMessage, vm.OutputFolderError);
            Assert.True(vm.HasOutputFolderError);
            Assert.Equal(SettingsViewModel.EmptyFolderMessage, vm.OutputFolderMessage);
            Assert.Empty(store.Saves);
        }

        [Theory]
        [InlineData(@"relative\folder")]
        [InlineData(@"C:folder")]
        public void Save_RejectsAFolderThatIsNotAFullPath(string folder)
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            vm.OutputFolder = folder;

            Assert.False(vm.Save());

            Assert.Equal(Settings.InvalidOutputFolderMessage, vm.OutputFolderError);
            Assert.Empty(store.Saves);
        }

        [Fact]
        public void Save_RejectsAFolderThatCannotBeCreated()
        {
            // A file where the parent folder should be
            var blocker = Path.Combine(root, "blocker");
            File.WriteAllText(blocker, "x");
            var store = new FakeStore();
            var vm = NewViewModel(store);
            vm.OutputFolder = Path.Combine(blocker, "keys");

            Assert.False(vm.Save());

            Assert.Equal(SettingsViewModel.UnwritableFolderMessage, vm.OutputFolderError);
            Assert.Empty(store.Saves);
        }

        [Fact]
        public void Save_RejectsAPathThatIsAFile()
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            var file = Path.Combine(root, "afile");
            File.WriteAllText(file, "x");
            vm.OutputFolder = file;

            Assert.False(vm.Save());

            Assert.Equal(SettingsViewModel.UnwritableFolderMessage, vm.OutputFolderError);
            Assert.Empty(store.Saves);
        }

        [Fact]
        public void Save_RejectsAnExistingFolderThatIsNotWritable()
        {
            // A temp folder that denies the current user file creation; the rule is removed before cleanup
            var folder = Directory.CreateDirectory(Path.Combine(root, "readonly"));
            var user = WindowsIdentity.GetCurrent().User!;
            var deny = new FileSystemAccessRule(user, FileSystemRights.CreateFiles | FileSystemRights.WriteData, AccessControlType.Deny);
            var security = folder.GetAccessControl();
            security.AddAccessRule(deny);
            folder.SetAccessControl(security);

            try
            {
                var store = new FakeStore();
                var vm = NewViewModel(store);
                vm.OutputFolder = folder.FullName;

                Assert.False(vm.Save());

                Assert.Equal(SettingsViewModel.UnwritableFolderMessage, vm.OutputFolderError);
                Assert.Empty(store.Saves);
            }
            finally
            {
                security = folder.GetAccessControl();
                security.RemoveAccessRule(deny);
                folder.SetAccessControl(security);
            }
        }

        [Fact]
        public void CheckFolder_LeavesNoProbeFileBehind()
        {
            var folder = Path.Combine(root, "probe");

            Assert.Null(SettingsViewModel.CheckFolder(folder));

            Assert.Empty(Directory.GetFileSystemEntries(folder));
        }

        [Fact]
        public void EditingTheFolder_ClearsTheError()
        {
            var vm = NewViewModel(new FakeStore());
            vm.OutputFolder = string.Empty;
            vm.Save();
            Assert.True(vm.HasOutputFolderError);

            var changed = new List<string?>();
            vm.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);
            vm.OutputFolder = Path.Combine(root, "fixed");

            Assert.False(vm.HasOutputFolderError);
            Assert.Equal(SettingsViewModel.DefaultHelperText, vm.OutputFolderMessage);
            Assert.Contains(nameof(SettingsViewModel.OutputFolderMessage), changed);
        }

        [Fact]
        public void TheOtherToggles_NotifyUnderTheirOwnNames()
        {
            var vm = NewViewModel(new FakeStore());
            var changed = new List<string?>();
            vm.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);

            vm.ForceOverwrite = !vm.ForceOverwrite;
            vm.RightClickContextMenus = !vm.RightClickContextMenus;

            Assert.Equal(new[] { nameof(SettingsViewModel.ForceOverwrite), nameof(SettingsViewModel.RightClickContextMenus) }, changed);
        }

        #endregion

        #region Choose and Launch

        [Fact]
        public void Launch_IsEnabledOnlyForAFolderThatExists()
        {
            var vm = NewViewModel(new FakeStore());

            vm.OutputFolder = string.Empty;
            Assert.False(vm.CanLaunch);
            Assert.False(vm.LaunchCommand.CanExecute(null));

            vm.OutputFolder = Path.Combine(root, "missing");
            Assert.False(vm.CanLaunch);

            vm.OutputFolder = root;
            Assert.True(vm.CanLaunch);
            Assert.True(vm.LaunchCommand.CanExecute(null));

            vm.OutputFolder = "   ";
            Assert.False(vm.CanLaunch);
        }

        [Fact]
        public void Launch_OpensTheFolder_AndDoesNothingWhenItIsMissing()
        {
            var opened = new List<string>();
            var vm = NewViewModel(new FakeStore(), open: opened.Add);

            vm.OutputFolder = Path.Combine(root, "missing");
            vm.LaunchCommand.Execute(null);
            Assert.Empty(opened);

            vm.OutputFolder = root;
            vm.LaunchCommand.Execute(null);
            Assert.Equal(new[] { root }, opened);
        }

        [Fact]
        public void LaunchCommand_RaisesCanExecuteChanged_WhenTheFolderTextChanges()
        {
            var vm = NewViewModel(new FakeStore());
            var raised = 0;
            vm.LaunchCommand.CanExecuteChanged += (sender, e) => raised++;

            vm.OutputFolder = root;

            Assert.Equal(1, raised);
        }

        [Fact]
        public void Choose_SeedsThePickerWithTheCurrentFolder_AndTakesItsAnswer()
        {
            string? seed = null;
            var picked = Path.Combine(root, "picked");
            var vm = NewViewModel(new FakeStore(), pick: current =>
            {
                seed = current;
                return picked;
            });
            vm.OutputFolder = root;

            vm.ChooseCommand.Execute(null);

            Assert.Equal(root, seed);
            Assert.Equal(picked, vm.OutputFolder);
        }

        [Fact]
        public void Choose_KeepsTheFolder_WhenThePickerIsCancelled()
        {
            var vm = NewViewModel(new FakeStore(), pick: _ => null);
            vm.OutputFolder = root;

            vm.ChooseCommand.Execute(null);

            Assert.Equal(root, vm.OutputFolder);
        }

        #endregion
    }

    /// <summary>The toggle switch and the Settings window, hosted offscreen like the other accessibility tests.</summary>
    [Collection(WpfCollection.Name)]
    public class SettingsWindowTests
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

        static SettingsWindow ShowOffscreen(SettingsViewModel viewModel)
        {
            var window = new SettingsWindow(viewModel)
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

            public Settings Load()
            {
                return Stored;
            }

            public string? Save(Settings settings)
            {
                return SaveMessage;
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

                    Assert.Equal(36, toggle.ActualWidth);
                    Assert.Equal(20, toggle.ActualHeight);
                    Assert.Equal(14, thumb.Width);
                    Assert.Equal(14, thumb.Height);
                    Assert.Equal(surface, ((SolidColorBrush)track.Background).Color);
                    Assert.Equal(HorizontalAlignment.Left, thumb.HorizontalAlignment);

                    toggle.IsChecked = true;
                    Pump();
                    Assert.Equal(accent, ((SolidColorBrush)track.Background).Color);
                    Assert.Equal(onAccent, ((SolidColorBrush)thumb.Fill).Color);
                    Assert.Equal(HorizontalAlignment.Right, thumb.HorizontalAlignment);

                    Assert.Same(Application.Current.FindResource("AppFocusVisual"), toggle.FocusVisualStyle);
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
                AutomationProperties.SetName(toggle, "Force overwrite");

                var peer = UIElementAutomationPeer.CreatePeerForElement(toggle);
                var pattern = (IToggleProvider)peer.GetPattern(PatternInterface.Toggle);

                Assert.Equal("Force overwrite", peer.GetName());
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

        #region Window

        [Fact]
        public void Window_IsAModalDialogWithTheDocumentedBehavior()
        {
            RunSta(() =>
            {
                EnsureApplication();

                // Read before ShowOffscreen moves the window off the screen
                var fresh = new SettingsWindow(NewViewModel(Path.GetTempPath()));
                Assert.Equal(WindowStartupLocation.CenterOwner, fresh.WindowStartupLocation);
                fresh.Close();

                var window = ShowOffscreen(NewViewModel(Path.GetTempPath()));
                try
                {
                    Assert.Equal(440, window.Width);
                    Assert.Equal(SizeToContent.Height, window.SizeToContent);
                    Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
                    Assert.True(((Button)window.FindName("BtnSave")).IsDefault);
                    Assert.True(((Button)window.FindName("BtnCancel")).IsCancel);
                    Assert.NotEqual(18, window.FontSize);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Window_FocusesTheFolderField_AndFollowsTheDocumentedTabOrder()
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
                    var stops = Descendants<Control>(window)
                        .Where(c => c.IsTabStop && c.Focusable && c.IsEnabled && c.IsVisible && c is not ScrollViewer)
                        .Select(c => AutomationProperties.GetName(c) is { Length: > 0 } name ? name : c is ContentControl { Content: string s } ? s : c.GetType().Name)
                        .ToList();

                    Assert.Equal(
                        new[]
                        {
                            "Output folder", "Choose", "Launch", "Force overwrite", "Right-click context menus",
                            "Use environment variables in paths", "System theme", "Light theme", "Dark theme",
                            "Show the welcome panel", "Cancel", "Save changes"
                        },
                        stops);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Window_DisablesLaunch_ForAFolderThatDoesNotExist()
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
        public void Window_ShowsTheErrorStateInlineWithoutClosing()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var window = ShowOffscreen(NewViewModel(string.Empty));
                try
                {
                    var message = (TextBlock)window.FindName("OutputFolderMessage");
                    var danger = ((SolidColorBrush)Application.Current.FindResource("DangerBrush")).Color;

                    Assert.Equal(SettingsViewModel.DefaultHelperText, message.Text);

                    Assert.False(window.ViewModel.Save());
                    Pump();

                    Assert.True(window.IsVisible);
                    Assert.Equal(SettingsViewModel.EmptyFolderMessage, message.Text);
                    Assert.Equal(danger, ((SolidColorBrush)message.Foreground).Color);
                    Assert.True(Themes_HasError((TextBox)window.FindName("TextboxOutputFolder")));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Window_OpeningAndClosing_DoesNotPreviewOrRestoreATheme()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var before = keycuts.GUI.Themes.ThemeManager.Preference;
                var other = before == ThemePreference.Dark ? ThemePreference.Light : ThemePreference.Dark;
                var store = new FakeSettingsStore { Stored = new Settings { OutputFolder = Path.GetTempPath(), Theme = other } };

                var window = ShowOffscreen(new SettingsViewModel(store));
                Assert.Equal(other, window.ViewModel.Theme);
                Assert.Equal(before, keycuts.GUI.Themes.ThemeManager.Preference);

                window.Close();
                Assert.Equal(before, keycuts.GUI.Themes.ThemeManager.Preference);
            });
        }

        [Fact]
        public void Window_ScrollsASaveErrorIntoView_WhenTheBodyIsCapped()
        {
            // The save error is the last thing in the scrolling body, so a capped window scrolled to the top would hide it
            RunSta(() =>
            {
                EnsureApplication();
                var store = new FakeSettingsStore
                {
                    Stored = new Settings { OutputFolder = Path.GetTempPath() },
                    SaveMessage = Settings.PathUpdateFailedMessage
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

                    Assert.False(window.ViewModel.Save());
                    Pump();
                    window.UpdateLayout();
                    Pump();

                    var top = message.TranslatePoint(new Point(0, 0), scroller).Y;
                    var bottom = message.TranslatePoint(new Point(0, message.ActualHeight), scroller).Y;
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
        public void Window_CappedBelowItsContent_ScrollsTheBody_AndKeepsTheFooterVisible()
        {
            // What a 1080p screen at 200% scaling (540 DIPs high) does to the ~655 DIP window
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
                    var save = (Button)window.FindName("BtnSave");
                    var content = (FrameworkElement)window.Content;
                    var saveBottom = save.TranslatePoint(new Point(0, save.ActualHeight), content).Y;

                    Assert.True(window.ActualHeight <= 400);
                    Assert.True(scroller.ScrollableHeight > 0);
                    Assert.True(saveBottom <= content.ActualHeight, $"Save ends at {saveBottom}, content is {content.ActualHeight}");
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Window_CapsItsHeightToTheWorkArea()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var window = ShowOffscreen(NewViewModel(Path.GetTempPath()));
                try
                {
                    Assert.Equal(WindowLayout.GetWorkAreaInDips(window).Height, window.MaxHeight);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        static bool Themes_HasError(TextBox box)
        {
            return keycuts.GUI.Themes.TextBoxChrome.GetHasError(box);
        }

        [Fact]
        public void Window_TogglesAreNamedForScreenReaders()
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
                        new[] { "Force overwrite", "Right-click context menus", "Use environment variables in paths" },
                        toggles.Select(AutomationProperties.GetName));
                    Assert.All(toggles, toggle => Assert.NotNull(UIElementAutomationPeer.CreatePeerForElement(toggle).GetPattern(PatternInterface.Toggle)));
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
