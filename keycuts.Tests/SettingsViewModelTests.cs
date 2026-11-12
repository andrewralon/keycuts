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
                AskBeforeReplacing = false,
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
            Assert.False(vm.AskBeforeReplacing);
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
            vm.AskBeforeReplacing = true;
            vm.RightClickContextMenus = true;
            vm.UseEnvironmentVariables = false;
            vm.Theme = ThemePreference.Light;

            Assert.True(vm.Save());

            var result = Assert.Single(store.Saves);
            Assert.Equal(folder, result.OutputFolder);
            Assert.True(result.AskBeforeReplacing);
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
            vm.AskBeforeReplacing = true;
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

            vm.AskBeforeReplacing = !vm.AskBeforeReplacing;
            vm.RightClickContextMenus = !vm.RightClickContextMenus;

            Assert.Equal(new[] { nameof(SettingsViewModel.AskBeforeReplacing), nameof(SettingsViewModel.RightClickContextMenus) }, changed);
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

            public int Saves { get; private set; }

            public Settings Load()
            {
                return Stored;
            }

            public string? Save(Settings settings)
            {
                Saves++;
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
                    Assert.Equal(500, window.Width);
                    Assert.Equal(SizeToContent.Height, window.SizeToContent);
                    Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
                    Assert.True(((Button)window.FindName("BtnSave")).IsDefault);
                    // Escape and the close button leave without saving (#192); see Window_Escape_ClosesWithoutSaving
                    Assert.Null(window.FindName("BtnCancel"));
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
                            "Output folder", "Choose", "Launch", "Ask before replacing a keycut", "Right-click context menus",
                            "Use environment variables in paths", "System theme", "Light theme", "Dark theme",
                            "Check for updates", "Show the welcome panel", "Save"
                        },
                        stops);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        #region Shorter layout (#192)

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

        // The Cancel button's IsCancel used to do this. The key starts in a control and bubbles up to the window, as a typed one does,
        // so it has to work from wherever the focus is: the folder box, a switch, and the footer buttons
        [Theory]
        [InlineData("TextboxOutputFolder")]
        [InlineData("ToggleAskBeforeReplacing")]
        [InlineData("BtnShowWelcome")]
        [InlineData("BtnSave")]
        public void Window_Escape_ClosesWithoutSaving(string focusedControl)
        {
            RunSta(() =>
            {
                EnsureApplication();
                var store = new FakeSettingsStore { Stored = new Settings { OutputFolder = Path.GetTempPath() } };
                var window = ShowOffscreen(new SettingsViewModel(store));
                var closed = false;
                window.Closed += (_, _) => closed = true;
                try
                {
                    // A change that Save would have stored
                    window.ViewModel.OutputFolder = Path.Combine(Path.GetTempPath(), "changed");
                    var control = (UIElement)window.FindName(focusedControl);

                    control.RaiseEvent(KeyDown(control, Key.A));
                    Assert.False(closed);

                    var escape = KeyDown(control, Key.Escape);
                    control.RaiseEvent(escape);
                    Pump();

                    Assert.True(closed);
                    Assert.True(escape.Handled);
                    Assert.Equal(0, store.Saves);
                    Assert.False(window.ViewModel.StoreChanged);
                }
                finally
                {
                    if (!closed)
                    {
                        window.Close();
                    }
                }
            });
        }

        [Fact]
        public void Window_IconOnlyButtons_HaveTooltips_AndKeepTheirAutomationNames()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var window = ShowOffscreen(NewViewModel(Path.GetTempPath()));
                try
                {
                    var choose = (Button)window.FindName("BtnChoose");
                    var launch = (Button)window.FindName("BtnLaunch");

                    Assert.Equal("Choose a folder", choose.ToolTip);
                    Assert.Equal("Open the output folder", launch.ToolTip);
                    Assert.Equal("Choose", AutomationProperties.GetName(choose));
                    Assert.Equal("Launch", AutomationProperties.GetName(launch));
                    // Launch is disabled for a missing folder, and a bare icon should still say what it is
                    Assert.True(ToolTipService.GetShowOnDisabled(launch));

                    // Icon only: no visible text is left in either
                    Assert.Empty(Descendants<TextBlock>(choose));
                    Assert.Empty(Descendants<TextBlock>(launch));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Window_FolderMessage_SitsOnTheLabelsLine_AndALongErrorWrapsWithoutLeavingTheWindow()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var longError = "Choose a folder you have permission to write to. Ask your administrator if this folder is on a shared drive.";
                var store = new FakeSettingsStore { Stored = new Settings { OutputFolder = Path.GetTempPath() } };
                var window = ShowOffscreen(new SettingsViewModel(store, checkFolder: _ => longError));
                try
                {
                    var root = (FrameworkElement)window.Content;
                    var label = Descendants<TextBlock>(window).Single(t => t.Text == "Output folder");
                    var message = (TextBlock)window.FindName("OutputFolderMessage");
                    var box = (TextBox)window.FindName("TextboxOutputFolder");
                    var launch = (Button)window.FindName("BtnLaunch");

                    Assert.Equal("New keycuts files are saved here.", message.Text);
                    var oneLine = BoundsIn(message, root);
                    Assert.True(Math.Abs(oneLine.Top - BoundsIn(label, root).Top) < 4, "The message is on the label's line");
                    Assert.True(oneLine.Bottom <= BoundsIn(box, root).Top, "...and above the box, not beside it");
                    Assert.Equal(BoundsIn(launch, root).Right, oneLine.Right, 1);

                    // The same text block shows the validation error, in the danger color, and wraps it to the right of the label
                    window.ViewModel.OutputFolder = Path.GetTempPath();
                    Assert.False(window.ViewModel.Save());
                    Pump();
                    window.UpdateLayout();

                    var wrapped = BoundsIn(message, root);
                    Assert.Equal(longError, message.Text);
                    Assert.True(wrapped.Height > oneLine.Height * 1.5, "A long error takes more than one line");
                    Assert.True(wrapped.Left >= BoundsIn(label, root).Right, "...beside the label, not over it");
                    Assert.Equal(BoundsIn(launch, root).Right, wrapped.Right, 1);
                    Assert.True(wrapped.Bottom <= BoundsIn(box, root).Top, "...and still above the box");
                    Assert.Equal(TextAlignment.Right, message.TextAlignment);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Window_ThemeChoices_ShareTheLabelsLine_AtTheRightEdge()
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
                    var toggle = BoundsIn((ToggleButton)window.FindName("ToggleAskBeforeReplacing"), root);

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
        public void Window_SectionsHaveNoDividers_ButTheFooterKeepsItsBorder()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var window = ShowOffscreen(NewViewModel(Path.GetTempPath()));
                try
                {
                    var body = (StackPanel)((ScrollViewer)window.FindName("BodyScroller")).Content;

                    Assert.DoesNotContain(body.Children.Cast<UIElement>(), child => child is Border);
                    Assert.Contains(Descendants<Border>(window), border => border.BorderThickness == new Thickness(0, 1, 0, 0));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Window_ShowsOnlyTheShortenedText_ButKeepsTheHelpTextForScreenReaders()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var window = ShowOffscreen(NewViewModel(Path.GetTempPath()));
                try
                {
                    var texts = Descendants<TextBlock>(window).Where(t => t.IsVisible).Select(t => t.Text).ToList();

                    Assert.Contains("Settings", texts);
                    Assert.DoesNotContain(texts, t => t.Contains("Choose how keycuts creates"));
                    Assert.DoesNotContain(texts, t => t.Contains("Nothing about you"));
                    Assert.DoesNotContain(texts, t => t.StartsWith("Confirm before a new keycut", StringComparison.Ordinal));
                    Assert.DoesNotContain(texts, t => t is "Cancel" or "Choose" or "Launch" or "Save changes");
                    Assert.Contains("Once a day, ask GitHub whether a newer version is out.", texts);
                    Assert.Contains("Save", texts);
                    Assert.Contains(@"Save paths as %USERPROFILE%\… or similar. Hold Shift to preview or create a keycut like this, even when disabled.", texts);

                    // The visible notes went, not the help text: it is all a screen reader has for "Ask before replacing a keycut" now
                    Assert.Equal("Confirm before a new keycut replaces one with the same name.",
                        AutomationProperties.GetHelpText((DependencyObject)window.FindName("ToggleAskBeforeReplacing")));

                    // "Flip" is right whichever way the setting is: Shift turns it on for one keycut, or off
                    var variables = AutomationProperties.GetHelpText((DependencyObject)window.FindName("ToggleEnvironmentVariables"));
                    Assert.Contains("Hold Shift while creating to flip it for one keycut", variables);
                    Assert.Equal("Save", AutomationProperties.GetName((DependencyObject)window.FindName("BtnSave")));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        #endregion

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
            // What a 1080p screen at 200% scaling (540 DIPs high) does to the ~670 DIP window
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

        // #171: Check now and the version show with a checker; Download only while a newer release is known
        [Fact]
        public void Window_WithAnUpdateChecker_ShowsCheckNow_AndDownloadOnlyWhenAnUpdateIsKnown()
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
                    Assert.Equal("Check for updates now", AutomationProperties.GetName(checkNow));
                    Assert.Equal("Version 1.1.1", ((TextBlock)window.FindName("CurrentVersionText")).Text);
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
        public void Window_WithoutAnUpdateChecker_HidesCheckNow()
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
                        new[] { "Ask before replacing a keycut", "Right-click context menus", "Use environment variables in paths", "Check for updates" },
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
