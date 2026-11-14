#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.Themes;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>The app icon choices, the files behind them, and the shortcut update. Nothing touches HKCU or the real shortcuts.</summary>
    public class AppIconTests : IDisposable
    {
        public static readonly int[] IcoSizes = { 16, 20, 24, 32, 40, 48, 64, 96, 256 };

        private readonly string root = Path.Combine(Path.GetTempPath(), "keycuts-icon-tests-" + Guid.NewGuid().ToString("N"));

        public AppIconTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        #region Choices

        [Theory]
        [InlineData("BlackAndWhite", AppIconChoice.BlackAndWhite)]
        [InlineData("accent", AppIconChoice.Accent)]
        [InlineData("PORCELAIN", AppIconChoice.Porcelain)]
        [InlineData("Onyx", AppIconChoice.Onyx)]
        [InlineData("Cobalt", AppIconChoice.Cobalt)]
        [InlineData("Twilight", AppIconChoice.Twilight)]
        [InlineData("Classic", AppIconChoice.Classic)]
        [InlineData(null, AppIconChoice.BlackAndWhite)]
        [InlineData("", AppIconChoice.BlackAndWhite)]
        [InlineData("Neon", AppIconChoice.BlackAndWhite)]
        [InlineData("3", AppIconChoice.BlackAndWhite)]
        [InlineData(" Cobalt", AppIconChoice.BlackAndWhite)]
        public void Parse_ReadsANameInAnyCase_AndAnythingElseIsTheDefault(string? value, AppIconChoice expected)
        {
            Assert.Equal(expected, AppIconChoices.Parse(value!));
        }

        [Fact]
        public void Parse_ReadsBackEveryChoiceAsWritten()
        {
            foreach (AppIconChoice choice in Enum.GetValues(typeof(AppIconChoice)))
            {
                Assert.Equal(choice, AppIconChoices.Parse(choice.ToString()));
            }
        }

        // The owner's mapping (2026-11-13): Black & White is Porcelain in light mode and Onyx in dark mode; Accent is Cobalt in
        // light mode and Twilight in dark mode. The rest never change.
        [Theory]
        [InlineData(AppIconChoice.BlackAndWhite, AppIconArt.Porcelain, AppIconArt.Onyx)]
        [InlineData(AppIconChoice.Accent, AppIconArt.Cobalt, AppIconArt.Twilight)]
        [InlineData(AppIconChoice.Porcelain, AppIconArt.Porcelain, AppIconArt.Porcelain)]
        [InlineData(AppIconChoice.Onyx, AppIconArt.Onyx, AppIconArt.Onyx)]
        [InlineData(AppIconChoice.Cobalt, AppIconArt.Cobalt, AppIconArt.Cobalt)]
        [InlineData(AppIconChoice.Twilight, AppIconArt.Twilight, AppIconArt.Twilight)]
        [InlineData(AppIconChoice.Classic, AppIconArt.Classic, AppIconArt.Classic)]
        public void Resolve_GivesEachChoiceItsLightAndDarkDrawing(AppIconChoice choice, AppIconArt light, AppIconArt dark)
        {
            Assert.Equal(light, AppIcons.Resolve(choice, light: true));
            Assert.Equal(dark, AppIcons.Resolve(choice, light: false));
        }

        [Theory]
        [InlineData(ThemeVariant.Light, "#000000", true)]
        [InlineData(ThemeVariant.Dark, "#FFFFFF", false)]
        [InlineData(ThemeVariant.HighContrast, "#FFFFFF", true)]
        [InlineData(ThemeVariant.HighContrast, "#000000", false)]
        // Aquatic and Desert, Windows 11's high contrast themes: a dark navy and a light cream window
        [InlineData(ThemeVariant.HighContrast, "#202020", false)]
        [InlineData(ThemeVariant.HighContrast, "#FFFAEF", true)]
        public void IsLight_FollowsTheVariant_AndTheWindowColorInHighContrast(ThemeVariant variant, string window, bool expected)
        {
            Assert.Equal(expected, AppIcons.IsLight(variant, (Color)ColorConverter.ConvertFromString(window)));
        }

        [Fact]
        public void Options_AreTheSevenChoicesInThePickersOrder_WithTheOwnersNamesAndDescriptions()
        {
            Assert.Equal(
                new[]
                {
                    "Black & White: Porcelain in light mode, Onyx in dark mode.",
                    "Accent: Cobalt in light mode, Twilight in dark mode.",
                    "Porcelain: Charcoal keycap on a light background.",
                    "Onyx: White keycap on a dark background.",
                    "Cobalt: Blue keycap on a light background.",
                    "Twilight: Blue keycap on a dark background.",
                    "Classic: Team Ralon FTW!",
                },
                AppIcons.Options.Select(o => $"{o.Name}: {o.Description}"));

            Assert.Equal(Enum.GetValues(typeof(AppIconChoice)).Cast<AppIconChoice>(), AppIcons.Options.Select(o => o.Choice));
            Assert.Equal(new[] { true, true, false, false, false, false, false }, AppIcons.Options.Select(o => o.FollowsTheme));
            Assert.Equal(
                new[] { AppIcons.FollowsThemeGroup, AppIcons.AlwaysTheSameGroup },
                AppIcons.Options.Select(o => o.Group).Distinct());
            Assert.Equal(new[] { AppIconChoice.Classic }, AppIcons.Options.Where(o => o.IsWide).Select(o => o.Choice));
        }

        #endregion

        #region Files

        // The icons the app ships: every size the title bar, the taskbar and Explorer ask for at 100-200 percent scaling, 32-bit,
        // with 256 as PNG and the rest as bitmaps (tools/New-AppIcons.ps1)
        [Theory]
        [InlineData("porcelain")]
        [InlineData("onyx")]
        [InlineData("cobalt")]
        [InlineData("twilight")]
        [InlineData("classic")]
        [InlineData("key-white")]
        [InlineData("key-charcoal")]
        [InlineData("key-blue")]
        [InlineData("classic-transparent")]
        public void IconFiles_HaveEverySizeAsA32BitFrame_AndAPngPerSize(string name)
        {
            var folder = Path.Combine(FindRepoRoot(), "assets", "icons", name);
            var frames = ReadIcoDirectory(Path.Combine(folder, name + ".ico"));

            Assert.Equal(IcoSizes, frames.Select(f => f.Side));
            Assert.All(frames, f => Assert.Equal(32, f.Bits));
            Assert.All(frames, f => Assert.Equal(f.Side == 256, f.IsPng));

            foreach (var side in IcoSizes)
            {
                Assert.True(File.Exists(Path.Combine(folder, $"{name}-{side}.png")), $"{name}-{side}.png is missing");
            }

            // The original icon has no big version; the keycaps have one
            Assert.Equal(!name.StartsWith("classic", StringComparison.Ordinal), File.Exists(Path.Combine(folder, $"{name}-1024.png")));
        }

        [Fact]
        public void EveryDrawing_IsCopiedBesideTheExe_ForTheShortcuts()
        {
            foreach (AppIconArt art in Enum.GetValues(typeof(AppIconArt)))
            {
                var path = Path.Combine(AppContext.BaseDirectory, AppIcons.FolderName, AppIcons.GetFileName(art));
                Assert.True(File.Exists(path), path + " is missing");
            }
        }

        // The exe's own icon and the installer's (Start menu shortcut, Installed apps) are the default's light drawing
        [Fact]
        public void TheExeAndTheInstaller_UsePorcelain()
        {
            var repo = FindRepoRoot();
            var expected = Path.GetFullPath(Path.Combine(repo, "assets", "icons", "porcelain", "porcelain.ico"));

            var csproj = XDocument.Load(Path.Combine(repo, "keycuts.GUI", "keycuts.GUI.csproj"));
            var applicationIcon = csproj.Descendants("ApplicationIcon").Single().Value;
            Assert.Equal(expected, Path.GetFullPath(Path.Combine(repo, "keycuts.GUI", applicationIcon)));

            var package = XDocument.Load(Path.Combine(repo, "installer", "Package.wxs"));
            var icon = package.Descendants().Single(e => e.Name.LocalName == "Icon" && (string?)e.Attribute("Id") == "keycuts.ico");
            Assert.Equal(expected, Path.GetFullPath(Path.Combine(repo, "installer", (string)icon.Attribute("SourceFile")!)));
        }

        #endregion

        #region Shortcuts

        [Fact]
        public void PointAt_ChangesOnlyShortcutsToTheExe_AndOnlyOnce()
        {
            var exe = Path.Combine(root, "app", "keycuts.GUI.exe");
            var other = Path.Combine(root, "app", "other.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(exe)!);
            File.WriteAllText(exe, string.Empty);
            File.WriteAllText(other, string.Empty);
            var icon = Path.Combine(root, "app", "icons", "cobalt.ico");

            var startMenu = Path.Combine(root, "Start Menu");
            var taskbar = Path.Combine(root, "TaskBar");
            var ours = CreateShortcut(Path.Combine(startMenu, "keycuts.lnk"), exe);
            var pinned = CreateShortcut(Path.Combine(taskbar, "keycuts.lnk"), exe);
            var theirs = CreateShortcut(Path.Combine(startMenu, "other.lnk"), other);
            var before = ShortcutIcons.GetIconLocation(theirs);
            var folders = new[] { startMenu, taskbar, Path.Combine(root, "missing"), string.Empty };

            var changed = ShortcutIcons.PointAt(folders, exe, icon);

            Assert.Equal(new[] { ours, pinned }.OrderBy(p => p), changed.OrderBy(p => p));
            Assert.Equal(icon + ",0", ShortcutIcons.GetIconLocation(ours));
            Assert.Equal(icon + ",0", ShortcutIcons.GetIconLocation(pinned));
            Assert.Equal(before, ShortcutIcons.GetIconLocation(theirs));

            // Already showing it: nothing is written again
            Assert.Empty(ShortcutIcons.PointAt(folders, exe, icon));
        }

        [Fact]
        public void PointAt_SkipsABrokenShortcut_AndStillChangesTheOthers()
        {
            var exe = Path.Combine(root, "keycuts.GUI.exe");
            File.WriteAllText(exe, string.Empty);
            var folder = Path.Combine(root, "Start Menu");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "broken.lnk"), "not a shortcut");
            var good = CreateShortcut(Path.Combine(folder, "keycuts.lnk"), exe);

            var changed = ShortcutIcons.PointAt(new[] { folder }, exe, Path.Combine(root, "porcelain.ico"));

            Assert.Equal(new[] { good }, changed);
        }

        private static string CreateShortcut(string path, string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            dynamic shortcut = shell.CreateShortcut(path);
            shortcut.TargetPath = target;
            shortcut.Save();
            return path;
        }

        #endregion

        internal static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "keycuts.sln")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new InvalidOperationException("keycuts.sln not found above " + AppContext.BaseDirectory);
        }

        private record IcoFrame(int Side, int Bits, bool IsPng);

        private static List<IcoFrame> ReadIcoDirectory(string path)
        {
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(0, BitConverter.ToUInt16(bytes, 0));
            Assert.Equal(1, BitConverter.ToUInt16(bytes, 2));
            var frames = new List<IcoFrame>();
            for (var i = 0; i < BitConverter.ToUInt16(bytes, 4); i++)
            {
                var at = 6 + 16 * i;
                var offset = BitConverter.ToInt32(bytes, at + 12);
                frames.Add(new IcoFrame(bytes[at] == 0 ? 256 : bytes[at], BitConverter.ToUInt16(bytes, at + 6), bytes[offset] == 0x89 && bytes[offset + 1] == (byte)'P'));
            }

            return frames;
        }
    }

    /// <summary>The icon row and the picker in the Settings window, hosted offscreen. The icon service is a fake.</summary>
    [Collection(WpfCollection.Name)]
    public class AppIconSettingsTests
    {
        private class RecordingIconService : IAppIconService
        {
            public RecordingIconService(AppIconChoice choice = AppIconChoice.BlackAndWhite)
            {
                Choice = choice;
            }

            public AppIconChoice Choice { get; private set; }

            public List<AppIconChoice> Applied { get; } = new();

            public string? Message { get; set; }

            public string? Apply(AppIconChoice choice)
            {
                Choice = choice;
                Applied.Add(choice);
                return Message;
            }
        }

        private class FakeStore : ISettingsStore
        {
            public int Saves { get; private set; }

            public Settings Load()
            {
                return new Settings { OutputFolder = Path.GetTempPath() };
            }

            public string? Save(Settings settings)
            {
                Saves++;
                return null;
            }
        }

        [Fact]
        public void SelectedIcon_StartsAtTheSavedChoice_AndAPickAppliesAtOnce_NotOnSave()
        {
            var icons = new RecordingIconService(AppIconChoice.Twilight);
            var store = new FakeStore();
            var viewModel = new SettingsViewModel(store, appIcon: icons);
            Assert.Equal(AppIconChoice.Twilight, viewModel.SelectedIcon.Choice);
            Assert.Equal("Icon: Twilight. Change", viewModel.IconButtonName);

            viewModel.SelectedIcon = AppIcons.Get(AppIconChoice.Classic);

            Assert.Equal(new[] { AppIconChoice.Classic }, icons.Applied);
            Assert.Equal(0, store.Saves);
            Assert.Equal("Icon: Classic. Change", viewModel.IconButtonName);

            // The same pick again, and the null a list sets when it loses its selection, do nothing
            viewModel.SelectedIcon = AppIcons.Get(AppIconChoice.Classic);
            viewModel.SelectedIcon = null!;
            Assert.Single(icons.Applied);
            Assert.Equal(AppIconChoice.Classic, viewModel.SelectedIcon.Choice);
        }

        [Fact]
        public void APickThatCouldNotBeSaved_SaysSoInThePicker()
        {
            var icons = new RecordingIconService { Message = AppIconService.SaveFailedMessage };
            var viewModel = new SettingsViewModel(new FakeStore(), appIcon: icons);
            Assert.False(viewModel.HasIconError);
            Assert.Equal("Applies as soon as you pick one.", viewModel.IconPickerNote);

            viewModel.SelectedIcon = AppIcons.Get(AppIconChoice.Cobalt);

            Assert.True(viewModel.HasIconError);
            Assert.Equal(AppIconService.SaveFailedMessage, viewModel.IconPickerNote);
        }

        [Fact]
        public void ThePicker_OpensAndCloses_AndDisablesTheRestOfTheWindowMeanwhile()
        {
            var viewModel = new SettingsViewModel(new FakeStore());
            Assert.False(viewModel.IsIconPickerOpen);
            Assert.True(viewModel.IsBodyEnabled);

            viewModel.OpenIconPickerCommand.Execute(null);
            Assert.True(viewModel.IsIconPickerOpen);
            Assert.False(viewModel.IsBodyEnabled);

            viewModel.CloseIconPickerCommand.Execute(null);
            Assert.False(viewModel.IsIconPickerOpen);
            Assert.True(viewModel.IsBodyEnabled);
        }

        [Fact]
        public void Window_TheIconRowIsFirstInAppearance_AndItsButtonShowsTheCurrentIcon()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var window = ShowOffscreen(new SettingsViewModel(new FakeStore(), appIcon: new RecordingIconService()));
                try
                {
                    var button = (Button)window.FindName("BtnIcon");
                    var themes = (Panel)window.FindName("ThemeChoices");
                    Assert.True(button.TranslatePoint(new Point(0, 0), window).Y < themes.TranslatePoint(new Point(0, 0), window).Y);
                    Assert.Equal("Icon: Black & White. Change", AutomationProperties.GetName(button));
                    Assert.Equal("Porcelain in light mode, Onyx in dark mode.", ((TextBlock)window.FindName("IconDescription")).Text);

                    // A theme option shows both drawings, light at the top left and dark smaller over it at the bottom right
                    var images = Descendants<Image>(button).Where(i => i.IsVisible).ToList();
                    Assert.Equal(2, images.Count);
                    var back = images.Single(i => i.ActualWidth > 33);
                    var front = images.Single(i => i.ActualWidth <= 33);
                    Assert.Same(AppIcons.GetPreview(AppIconArt.Porcelain), back.Source);
                    Assert.Same(AppIcons.GetPreview(AppIconArt.Onyx), front.Source);
                    Assert.True(front.TranslatePoint(new Point(0, 0), back).X > 0);
                    Assert.True(front.TranslatePoint(new Point(0, 0), back).Y > 0);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void Window_ThePickerListsTheSevenCards_InTwoGroups_AndAClickAppliesAndStaysOpen()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var icons = new RecordingIconService();
                var viewModel = new SettingsViewModel(new FakeStore(), appIcon: icons);
                var window = ShowOffscreen(viewModel);
                try
                {
                    var picker = (FrameworkElement)window.FindName("IconPicker");
                    Assert.False(picker.IsVisible);

                    ((Button)window.FindName("BtnIcon")).Command.Execute(null);
                    Pump();
                    Assert.True(picker.IsVisible);
                    // The page behind is off, so Tab stays in the picker
                    Assert.False(((FrameworkElement)window.FindName("BodyScroller")).IsEnabled);

                    var cards = Descendants<ListBoxItem>(picker).ToList();
                    Assert.Equal(AppIcons.Options.Select(o => o.Name), cards.Select(AutomationProperties.GetName));
                    Assert.True(cards.Single(c => c.IsSelected).IsKeyboardFocused);
                    var headers = Descendants<GroupItem>(picker).Select(g => ((CollectionViewGroup)g.DataContext).Name).ToList();
                    Assert.Equal(new object[] { AppIcons.FollowsThemeGroup, AppIcons.AlwaysTheSameGroup }, headers);

                    // Two cards to a row, and Classic alone on the last one
                    Assert.Equal(cards[2].TranslatePoint(new Point(0, 0), picker).Y, cards[3].TranslatePoint(new Point(0, 0), picker).Y, 1);
                    Assert.True(cards[6].TranslatePoint(new Point(0, 0), picker).Y > cards[5].TranslatePoint(new Point(0, 0), picker).Y);
                    Assert.Equal(cards[2].ActualWidth * 2 + 8, cards[6].ActualWidth, 1);

                    cards[4].IsSelected = true;
                    Pump();

                    Assert.Equal(new[] { AppIconChoice.Cobalt }, icons.Applied);
                    Assert.True(picker.IsVisible);
                    Assert.Equal("Icon: Cobalt. Change", AutomationProperties.GetName((Button)window.FindName("BtnIcon")));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // The list's own Foreground is the system text color, which is black even in the app's Dark palette (PR #215 review)
        [Fact]
        public void Window_CardNamesUseThePalettesTextColor()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var viewModel = new SettingsViewModel(new FakeStore());
                var window = ShowOffscreen(viewModel);
                try
                {
                    viewModel.OpenIconPickerCommand.Execute(null);
                    Pump();
                    var picker = (FrameworkElement)window.FindName("IconPicker");
                    var textBrush = (Brush)Application.Current.FindResource("TextBrush");

                    var names = Descendants<TextBlock>(picker).Where(t => AppIcons.Options.Any(o => o.Name == t.Text)).ToList();
                    Assert.Equal(AppIcons.Options.Count, names.Count);
                    Assert.All(names, name => Assert.Same(textBrush, name.Foreground));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // Settings is capped to the screen at high scaling (FitToWorkArea): the cards scroll, two to a row, and the header and
        // the close button stay in view (PR #215 review)
        [Fact]
        public void Window_OnAShortScreen_TheCardsScroll_AndTheCloseButtonStaysInView()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var viewModel = new SettingsViewModel(new FakeStore());
                var window = ShowOffscreen(viewModel);
                try
                {
                    window.MaxHeight = 420;
                    window.UpdateLayout();
                    viewModel.OpenIconPickerCommand.Execute(null);
                    Pump();

                    var scroller = (ScrollViewer)window.FindName("IconScroller");
                    var sheet = (FrameworkElement)window.FindName("IconPickerSheet");
                    var close = (Button)window.FindName("BtnCloseIconPicker");
                    var content = (FrameworkElement)window.Content;
                    Assert.True(scroller.ScrollableHeight > 0);
                    Assert.Equal(Visibility.Visible, scroller.ComputedVerticalScrollBarVisibility);
                    Assert.True(sheet.TranslatePoint(new Point(0, sheet.ActualHeight), content).Y <= content.ActualHeight);
                    Assert.True(close.TranslatePoint(new Point(0, 0), content).Y >= 0);

                    var cards = Descendants<ListBoxItem>(sheet).ToList();
                    Assert.Equal(cards[0].TranslatePoint(new Point(0, 0), sheet).Y, cards[1].TranslatePoint(new Point(0, 0), sheet).Y, 1);

                    // The last card can be scrolled to
                    cards[^1].BringIntoView();
                    Pump();
                    Assert.True(scroller.VerticalOffset > 0);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // Escape is the main window's (the shell's) since Settings became a screen (#218): with the picker open it closes only the
        // picker, and the focus goes back to the icon button; the screen stays
        [Fact]
        public void Window_EscapeClosesOnlyThePicker_AndGivesTheFocusBackToTheIconButton()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var viewModel = new SettingsViewModel(new FakeStore());
                var shell = new ShellViewModel(() => System.Threading.Tasks.Task.FromResult(3), settingsFactory: () => viewModel);
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
                shell.ShowSettings();
                Pump();
                window.UpdateLayout();
                var closed = false;
                window.Closed += (_, _) => closed = true;
                try
                {
                    viewModel.OpenIconPickerCommand.Execute(null);
                    Pump();
                    Assert.False(shell.AreTabsEnabled);
                    var card = Descendants<ListBoxItem>(window).Single(c => c.IsSelected);

                    card.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(card), 0, Key.Escape)
                    {
                        RoutedEvent = Keyboard.KeyDownEvent
                    });
                    Pump();

                    Assert.False(viewModel.IsIconPickerOpen);
                    Assert.False(closed);
                    Assert.Equal(ShellMode.Settings, shell.Mode);
                    Assert.True(shell.AreTabsEnabled);
                    Assert.True(Descendants<Button>(window).Single(b => b.Name == "BtnIcon").IsKeyboardFocused);
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
        public void Window_TheCloseButtonClosesThePicker()
        {
            RunSta(() =>
            {
                EnsureApplication();
                var viewModel = new SettingsViewModel(new FakeStore());
                var window = ShowOffscreen(viewModel);
                try
                {
                    viewModel.OpenIconPickerCommand.Execute(null);
                    Pump();
                    var close = (Button)window.FindName("BtnCloseIconPicker");
                    Assert.Equal("Close the icon picker", AutomationProperties.GetName(close));

                    close.Command.Execute(null);
                    Pump();

                    Assert.False(((FrameworkElement)window.FindName("IconPicker")).IsVisible);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void EveryDrawing_LoadsAsAWindowIcon_AndA256PixelPreview()
        {
            RunSta(() =>
            {
                EnsureApplication();
                foreach (AppIconArt art in Enum.GetValues(typeof(AppIconArt)))
                {
                    var icon = AppIcons.GetWindowIcon(art);
                    Assert.Equal(AppIconTests.IcoSizes, icon.Decoder.Frames.Select(f => f.PixelWidth).OrderBy(s => s));
                    Assert.Equal(256, AppIcons.GetPreview(art).PixelWidth);
                    Assert.True(icon.IsFrozen);
                }
            });
        }

        /// <summary>The Settings screen (#218) in a window of its own, MainWidth wide; its names are in the view's name scope.</summary>
        private sealed class SettingsHost : Window
        {
            public SettingsHost(SettingsViewModel viewModel)
            {
                View = new keycuts.GUI.Views.SettingsView { DataContext = viewModel };
                Content = View;
                Width = WindowLayout.CreatorWidth;
                SizeToContent = SizeToContent.Height;
            }

            public keycuts.GUI.Views.SettingsView View { get; }

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
    }
}
