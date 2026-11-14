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
    /// <summary>The Settings screen's rules (#218). The store is a fake and folders live under a temp root: nothing touches HKCU or the real output folder.</summary>
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

        #region Load, and storing each change (#218)

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
        public void Opening_StoresNothing()
        {
            var store = new FakeStore();
            var themes = new List<ThemePreference>();

            _ = new SettingsViewModel(store, applyTheme: themes.Add);

            Assert.Empty(store.Saves);
            Assert.Empty(themes);
        }

        [Fact]
        public void EachToggle_IsStoredTheMomentItChanges_WithTheStoredFolder()
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            var stored = 0;
            vm.Stored += (sender, e) => stored++;

            vm.AskBeforeReplacing = true;
            Assert.True(Assert.Single(store.Saves).AskBeforeReplacing);

            vm.RightClickContextMenus = true;
            vm.UseEnvironmentVariables = false;
            vm.CheckForUpdates = false;

            Assert.Equal(4, store.Saves.Count);
            var last = store.Saves[^1];
            Assert.True(last.AskBeforeReplacing);
            Assert.True(last.RightClickContextMenus);
            Assert.False(last.UseEnvironmentVariables);
            Assert.False(last.CheckForUpdates);
            Assert.All(store.Saves, s => Assert.Equal(@"C:\Shortcuts", s.OutputFolder));
            Assert.Equal(4, stored);
        }

        [Fact]
        public void SettingAToggleToTheValueItHas_StoresNothing()
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);

            vm.AskBeforeReplacing = vm.AskBeforeReplacing;
            vm.Theme = vm.Theme;

            Assert.Empty(store.Saves);
        }

        [Fact]
        public void TheTheme_IsAppliedAndStoredAtOnce()
        {
            var store = new FakeStore();
            var themes = new List<ThemePreference>();
            var vm = new SettingsViewModel(store, applyTheme: themes.Add);

            vm.Theme = ThemePreference.Light;

            Assert.Equal(new[] { ThemePreference.Light }, themes);
            Assert.Equal(ThemePreference.Light, Assert.Single(store.Saves).Theme);
        }

        [Fact]
        public void EditingTheFolder_StoresNothing_UntilItIsCommitted()
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            var folder = Path.Combine(root, "keys");
            var changed = 0;
            vm.OutputFolderChanged += (sender, e) => changed++;

            vm.OutputFolder = "  \"" + folder + "\"  ";
            Assert.Empty(store.Saves);
            Assert.False(Directory.Exists(folder));

            // A toggle changed meanwhile stores the folder that was stored, not the half-typed one
            vm.AskBeforeReplacing = true;
            Assert.Equal(@"C:\Shortcuts", Assert.Single(store.Saves).OutputFolder);

            Assert.True(vm.CommitOutputFolder());

            Assert.Equal(folder, store.Saves[^1].OutputFolder);
            Assert.True(store.Saves[^1].AskBeforeReplacing);
            Assert.Equal(folder, vm.OutputFolder);
            Assert.True(Directory.Exists(folder));
            Assert.Equal(1, changed);

            // ...and after that, a toggle stores the new folder
            vm.RightClickContextMenus = true;
            Assert.Equal(folder, store.Saves[^1].OutputFolder);
        }

        [Fact]
        public void CommittingTheFolderItAlreadyHas_StoresNothing()
        {
            var store = new FakeStore { Stored = new Settings { OutputFolder = root } };
            var vm = NewViewModel(store);
            var changed = 0;
            vm.OutputFolderChanged += (sender, e) => changed++;

            Assert.True(vm.CommitOutputFolder());
            vm.OutputFolder = " " + root.ToUpperInvariant() + " ";
            Assert.True(vm.CommitOutputFolder());

            Assert.Empty(store.Saves);
            Assert.Equal(0, changed);
        }

        [Fact]
        public void AStoreMessage_ShowsOnThePage_UntilAChangeStoresCleanly()
        {
            var store = new FakeStore { SaveMessage = Settings.ContextMenuFailedMessage };
            var vm = NewViewModel(store);
            var stored = 0;
            vm.Stored += (sender, e) => stored++;

            vm.RightClickContextMenus = true;

            Assert.Equal(Settings.ContextMenuFailedMessage, vm.SaveError);
            Assert.True(vm.HasSaveError);
            // The values may be stored even so, so the rest of the app still follows
            Assert.Equal(1, stored);

            store.SaveMessage = null;
            vm.AskBeforeReplacing = true;
            Assert.Null(vm.SaveError);
        }

        [Fact]
        public void APathFailure_StillCountsAsANewFolder()
        {
            // Settings.SaveSettings stores the folder before PATH fails, so the count must follow it
            var store = new FakeStore { SaveMessage = Settings.PathUpdateFailedMessage };
            var vm = NewViewModel(store);
            var changed = 0;
            vm.OutputFolderChanged += (sender, e) => changed++;

            vm.OutputFolder = Path.Combine(root, "keys");
            Assert.True(vm.CommitOutputFolder());

            Assert.Equal(Settings.PathUpdateFailedMessage, vm.SaveError);
            Assert.Equal(1, changed);
        }

        [Fact]
        public void ShowWelcome_AsksTheShell()
        {
            var vm = NewViewModel(new FakeStore());
            var asked = 0;
            vm.ShowWelcomeRequested += (sender, e) => asked++;

            vm.ShowWelcomeCommand.Execute(null);

            Assert.Equal(1, asked);
        }

        #endregion

        #region Restore defaults (#218)

        [Fact]
        public void RestoreDefaults_PutsEverySettingBack_ButNeverTheFolder_AndStoresOnce()
        {
            var store = new FakeStore
            {
                Stored = new Settings
                {
                    OutputFolder = @"D:\Elsewhere",
                    AskBeforeReplacing = false,
                    RightClickContextMenus = false,
                    UseEnvironmentVariables = true,
                    Theme = ThemePreference.Dark,
                    CheckForUpdates = false
                }
            };
            var icon = new InMemoryAppIconService(AppIconChoice.Classic);
            var themes = new List<ThemePreference>();
            var vm = new SettingsViewModel(store, appIcon: icon, applyTheme: themes.Add);
            // An edit in the box that wasn't committed stays an edit
            vm.OutputFolder = @"E:\Typing";

            vm.RestoreDefaultsCommand.Execute(null);

            var saved = Assert.Single(store.Saves);
            var defaults = Settings.Defaults();
            Assert.Equal(@"D:\Elsewhere", saved.OutputFolder);
            Assert.Equal(defaults.AskBeforeReplacing, saved.AskBeforeReplacing);
            Assert.True(saved.AskBeforeReplacing);
            Assert.True(saved.RightClickContextMenus);
            Assert.False(saved.UseEnvironmentVariables);
            Assert.Equal(ThemePreference.System, saved.Theme);
            Assert.True(saved.CheckForUpdates);

            Assert.True(vm.AskBeforeReplacing);
            Assert.True(vm.RightClickContextMenus);
            Assert.False(vm.UseEnvironmentVariables);
            Assert.Equal(ThemePreference.System, vm.Theme);
            Assert.True(vm.CheckForUpdates);
            Assert.Equal(@"E:\Typing", vm.OutputFolder);

            Assert.Equal(new[] { ThemePreference.System }, themes);
            Assert.Equal(Settings.DefaultAppIcon, icon.Choice);
            Assert.Equal(Settings.DefaultAppIcon, vm.SelectedIcon.Choice);
        }

        [Fact]
        public void RestoreDefaults_AsksFirst_AndANoChangesNothing()
        {
            var store = new FakeStore();
            var asked = 0;
            var vm = new SettingsViewModel(store, confirmRestoreDefaults: () =>
            {
                asked++;
                return false;
            });

            vm.RestoreDefaultsCommand.Execute(null);

            Assert.Equal(1, asked);
            Assert.Empty(store.Saves);
            Assert.False(vm.AskBeforeReplacing);
            Assert.Equal(ThemePreference.Dark, vm.Theme);
        }

        [Fact]
        public void RestoreDefaults_NotifiesEveryValueItChanged()
        {
            var vm = NewViewModel(new FakeStore());
            var changed = new List<string?>();
            vm.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);

            vm.RestoreDefaultsCommand.Execute(null);

            Assert.Contains(nameof(SettingsViewModel.AskBeforeReplacing), changed);
            Assert.Contains(nameof(SettingsViewModel.RightClickContextMenus), changed);
            Assert.Contains(nameof(SettingsViewModel.UseEnvironmentVariables), changed);
            Assert.Contains(nameof(SettingsViewModel.CheckForUpdates), changed);
            Assert.Contains(nameof(SettingsViewModel.Theme), changed);
        }

        [Fact]
        public void Defaults_AreWhatANewInstallHas()
        {
            var defaults = Settings.Defaults();

            Assert.Equal(Runner.DefaultAskBeforeReplacing, defaults.AskBeforeReplacing);
            Assert.True(defaults.RightClickContextMenus);
            Assert.False(defaults.UseEnvironmentVariables);
            Assert.Equal(ThemePreference.System, defaults.Theme);
            Assert.True(defaults.CheckForUpdates);
            Assert.Equal(AppIconChoice.BlackAndWhite, Settings.DefaultAppIcon);
            Assert.Equal(string.Empty, defaults.OutputFolder);
        }

        [Fact]
        public void TheConfirmation_SaysTheFolderStays()
        {
            var confirm = ConfirmViewModel.ForRestoreDefaults();

            Assert.Equal("Restore defaults", confirm.ConfirmText);
            Assert.Contains("output folder stays", confirm.Intro);
            Assert.False(confirm.ShowTargets);
            Assert.False(confirm.ShowWarnings);
        }

        #endregion

        #region Validation

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Commit_RejectsAnEmptyFolder(string folder)
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            vm.OutputFolder = folder;

            Assert.False(vm.CommitOutputFolder());

            Assert.Equal(SettingsViewModel.EmptyFolderMessage, vm.OutputFolderError);
            Assert.True(vm.HasOutputFolderError);
            Assert.Equal(SettingsViewModel.EmptyFolderMessage, vm.OutputFolderMessage);
            Assert.Empty(store.Saves);
        }

        [Theory]
        [InlineData(@"relative\folder")]
        [InlineData(@"C:folder")]
        public void Commit_RejectsAFolderThatIsNotAFullPath(string folder)
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            vm.OutputFolder = folder;

            Assert.False(vm.CommitOutputFolder());

            Assert.Equal(Settings.InvalidOutputFolderMessage, vm.OutputFolderError);
            Assert.Empty(store.Saves);
        }

        [Fact]
        public void Commit_RejectsAFolderThatCannotBeCreated()
        {
            // A file where the parent folder should be
            var blocker = Path.Combine(root, "blocker");
            File.WriteAllText(blocker, "x");
            var store = new FakeStore();
            var vm = NewViewModel(store);
            vm.OutputFolder = Path.Combine(blocker, "keys");

            Assert.False(vm.CommitOutputFolder());

            Assert.Equal(SettingsViewModel.UnwritableFolderMessage, vm.OutputFolderError);
            Assert.Empty(store.Saves);
        }

        [Fact]
        public void Commit_RejectsAPathThatIsAFile()
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            var file = Path.Combine(root, "afile");
            File.WriteAllText(file, "x");
            vm.OutputFolder = file;

            Assert.False(vm.CommitOutputFolder());

            Assert.Equal(SettingsViewModel.UnwritableFolderMessage, vm.OutputFolderError);
            Assert.Empty(store.Saves);
        }

        [Fact]
        public void Commit_RejectsAnExistingFolderThatIsNotWritable()
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

                Assert.False(vm.CommitOutputFolder());

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
        public void ARefusedFolder_KeepsTheStoredOne_ForTheNextToggle()
        {
            var store = new FakeStore();
            var vm = NewViewModel(store);
            vm.OutputFolder = @"relative\folder";
            Assert.False(vm.CommitOutputFolder());

            vm.AskBeforeReplacing = true;

            Assert.Equal(@"C:\Shortcuts", Assert.Single(store.Saves).OutputFolder);
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
            vm.CommitOutputFolder();
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
            var store = new FakeStore();
            var vm = NewViewModel(store, pick: current =>
            {
                seed = current;
                return picked;
            });
            vm.OutputFolder = root;

            vm.ChooseCommand.Execute(null);

            Assert.Equal(root, seed);
            Assert.Equal(picked, vm.OutputFolder);
            // A pick from the folder button is a commit: stored at once (#218)
            Assert.Equal(picked, Assert.Single(store.Saves).OutputFolder);
        }

        [Fact]
        public void Choose_KeepsTheFolder_WhenThePickerIsCancelled()
        {
            var store = new FakeStore();
            var vm = NewViewModel(store, pick: _ => null);
            vm.OutputFolder = root;

            vm.ChooseCommand.Execute(null);

            Assert.Equal(root, vm.OutputFolder);
            Assert.Empty(store.Saves);
        }

        #endregion
    }

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

        /// <summary>The Settings screen in a window of its own, MainWidth wide and as tall as its content, as the main window shows it.</summary>
        private sealed class SettingsHost : Window
        {
            public SettingsHost(SettingsViewModel viewModel)
            {
                ViewModel = viewModel;
                View = new keycuts.GUI.Views.SettingsView { DataContext = viewModel };
                Content = View;
                Width = WindowLayout.CreatorWidth;
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

            public string? Save(Settings settings)
            {
                Saves++;
                Last = settings;
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
        public void Screen_HasNoHeadingNoSaveButton_AndSaysChangesAreSaved()
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

                    var note = (FrameworkElement)window.FindName("SavedNote");
                    Assert.True(note.IsVisible);
                    Assert.Contains(SettingsViewModel.SavedAsYouGoNote, texts);
                    Assert.Equal(12, Descendants<keycuts.GUI.Controls.Icon>(note).Single().Size);
                    var noteText = Descendants<TextBlock>(note).Single();
                    Assert.Same(Application.Current.FindResource("Caption"), noteText.Style);

                    // The last row: show welcome at the left, restore defaults at the right, both lowercase
                    var welcome = (Button)window.FindName("BtnShowWelcome");
                    var restore = (Button)window.FindName("BtnRestoreDefaults");
                    Assert.Equal("show welcome", welcome.Content);
                    Assert.Equal("restore defaults", restore.Content);
                    var root = (FrameworkElement)window.Content;
                    Assert.True(BoundsIn(welcome, root).Right < BoundsIn(restore, root).Left);
                    Assert.Equal(BoundsIn((FrameworkElement)window.FindName("BtnLaunch"), root).Right, BoundsIn(restore, root).Right, 1);
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
                            "Use environment variables in paths", "Icon: Black & White. Change", "System theme", "Light theme", "Dark theme",
                            "Check for updates", "show the welcome panel", "restore defaults"
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
        public void Screen_SectionsAreLabels_WithNoDividers_24Apart()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var window = ShowOffscreen(NewViewModel(Path.GetTempPath()));
                try
                {
                    var body = (StackPanel)((ScrollViewer)window.FindName("BodyScroller")).Content;
                    var labels = body.Children.OfType<keycuts.GUI.Controls.SectionLabel>().ToList();

                    Assert.DoesNotContain(body.Children.Cast<UIElement>(), child => child is Border or Separator);
                    Assert.Equal(new[] { "Output", "Behavior", "Appearance", "Updates" }, labels.Select(l => l.Text));
                    Assert.Equal(0, labels[0].Margin.Top);
                    Assert.All(labels.Skip(1), l => Assert.Equal(new Thickness(0, 24, 0, 8), l.Margin));
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
                    Assert.Contains("Once a day, ask GitHub whether a newer version is out.", texts);
                    Assert.Contains(@"Save paths as %USERPROFILE%\… or similar. Hold Shift to preview or create a keycut like this, even when disabled.", texts);

                    Assert.Equal("Once a day, ask GitHub whether a newer version is out.",
                        AutomationProperties.GetHelpText((DependencyObject)window.FindName("ToggleCheckForUpdates")));
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
                    Assert.Equal("355", ((TextBlock)footer.FindName("CountText")).Text);
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
