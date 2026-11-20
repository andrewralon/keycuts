using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.Controls;
using keycuts.GUI.Themes;
using keycuts.GUI.ViewModels;
using keycuts.GUI.Views;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// #218: the tab bar that replaces the title bar, the window's two sizes and its keys (WPF's TabbedWindowTests, ported). The
    /// Win32 side (WM_NCHITTEST, a posted SC_MAXIMIZE) has no headless twin: here the element roles that answer it are checked, and
    /// NoMaximizeHook's answers to the messages themselves.
    /// </summary>
    public class MainWindowTests
    {
        #region The frame

        [AvaloniaFact]
        public void TheBar_IsTheCaption_ExceptTheTabsAndTheCaptionButtons()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows' frame; macOS has its own (OnTheMac_TheFrameIsMacOS)");
            WithWindow((window, shell) =>
            {
                Assert.Equal(WindowDecorations.BorderOnly, window.WindowDecorations);
                Assert.True(window.ExtendClientAreaToDecorationsHint);
                Assert.Equal(WindowDecorationsElementRole.TitleBar, Role(window, "DragArea"));
                Assert.Equal(WindowDecorationsElementRole.User, Role(window, "TabStrip"));
                Assert.Equal(WindowDecorationsElementRole.MinimizeButton, Role(window, "BtnMinimize"));
                Assert.Equal(WindowDecorationsElementRole.CloseButton, Role(window, "BtnClose"));
                // Not MaximizeButton: Windows 11 would open its snap layouts over it (correction 12)
                Assert.Equal(WindowDecorationsElementRole.User, Role(window, "BtnMaximize"));

                // The icon, the name and the grip let the pointer through to the caption under them
                var bar = Named<Grid>(window, "Bar");
                Assert.False(((Control)Named<Image>(window, "AppIcon").Parent!).IsHitTestVisible);
            });
        }

        [AvaloniaFact]
        public void TheWindow_IsNeverMaximized_AndOnlyTheLargeScreensResize()
        {
            WithWindow((window, shell) =>
            {
                var maximize = Named<Button>(window, "BtnMaximize");
                foreach (var show in new Action[] { shell.ShowCreator, shell.ShowManager, shell.ShowSettings, shell.ShowManager, shell.ShowCreator })
                {
                    show();
                    Dispatcher.UIThread.RunJobs();
                    var screen = shell.Mode.ToString();

                    Assert.False(window.CanMaximize, screen);
                    Assert.False(maximize.IsEnabled, screen);
                    Assert.True(Named<Button>(window, "BtnMinimize").IsEnabled, screen);
                    Assert.True(Named<Button>(window, "BtnClose").IsEnabled, screen);
                    Assert.Equal(shell.Mode != ShellMode.Creator, window.CanResize);
                    Assert.Equal(WindowState.Normal, window.WindowState);
                }
            }, withSettings: true);
        }

        [Theory]
        [InlineData(0x16CF0000u, 0x16CE0000u)]   // WS_OVERLAPPEDWINDOW | WS_VISIBLE | WS_CLIPCHILDREN: the box comes off
        [InlineData(0x96000000u, 0x96080000u)]   // BorderOnly's WS_POPUP: the window menu goes on
        public void WithStyles_AddsTheWindowMenu_AndNeverTheMaximizeBox(uint style, uint expected)
        {
            Assert.Equal(expected, NoMaximizeHook.WithStyles(style));
        }

        [Theory]
        [InlineData(0xF030, true)]    // SC_MAXIMIZE
        [InlineData(0xF032, true)]    // a double click on the caption
        [InlineData(0xF120, false)]   // SC_RESTORE
        [InlineData(0xF020, false)]   // SC_MINIMIZE
        [InlineData(0xF012, false)]   // SC_MOVE from the caption (a drag)
        [InlineData(0xF000, false)]   // SC_SIZE
        public void TheHook_RefusesOnlyMaximize(int command, bool refused)
        {
            var handled = false;
            NoMaximizeHook.WndProc(IntPtr.Zero, (uint)NoMaximizeHook.WmSysCommand, (IntPtr)command, IntPtr.Zero, ref handled);
            Assert.Equal(refused, handled);
        }

        [Theory]
        [InlineData(0x00AEu, true)]    // WM_NCUAHDRAWCAPTION: the classic caption buttons, painted over the tab bar
        [InlineData(0x00AFu, true)]    // WM_NCUAHDRAWFRAME
        [InlineData(0x0085u, false)]   // WM_NCPAINT
        [InlineData(0x00A1u, false)]   // WM_NCLBUTTONDOWN, which starts a drag
        public void TheHook_DropsOnlyTheClassicCaptionPaints(uint msg, bool dropped)
        {
            var handled = false;
            NoMaximizeHook.WndProc(IntPtr.Zero, msg, IntPtr.Zero, IntPtr.Zero, ref handled);
            Assert.Equal(dropped, handled);
        }

        #endregion The frame

        #region The tabs

        [AvaloniaFact]
        public void TheTabs_AreATabList_WithNamesAndSelectedState()
        {
            WithWindow((window, shell) =>
            {
                var rail = Named<RailTabControl>(window, "TabStrip");
                var tabs = new[] { "CreatorTab", "ManagerTab", "SettingsTab" }.Select(n => Named<TabItem>(window, n)).ToArray();
                Assert.Equal(tabs, rail.Items.Cast<TabItem>());
                Assert.Equal(AutomationControlType.Tab, ControlAutomationPeer.CreatePeerForElement(rail).GetAutomationControlType());

                foreach (var (tab, i) in tabs.Select((t, i) => (t, i)))
                {
                    var name = new[] { "creator", "manager", "settings" }[i];
                    Assert.Equal(AutomationControlType.TabItem, ControlAutomationPeer.CreatePeerForElement(tab).GetAutomationControlType());
                    Assert.Equal(name, AutomationProperties.GetName(tab));
                    // The key that picks it, in the tooltip after the name and as the accelerator a screen reader reads
                    Assert.Equal(PlatformKeys.Tip(name, (i + 1).ToString()), ToolTip.GetTip(tab));
                    Assert.Equal(PlatformKeys.Name((i + 1).ToString()), AutomationProperties.GetAcceleratorKey(tab));
                }

                // The gear is the tab, with no text of its own; the other two are text only
                Assert.Empty(tabs[2].GetVisualDescendants().OfType<TextBlock>());
                Assert.Equal("creator", tabs[0].Header);
                Assert.Empty(tabs[0].GetVisualDescendants().OfType<Icon>());
                Assert.Same(tabs[0], rail.SelectedItem);
            });
        }

        [AvaloniaFact]
        public void TheTabs_FollowTheShell_AndSwitchIt()
        {
            WithWindow((window, shell) =>
            {
                var rail = Named<RailTabControl>(window, "TabStrip");
                var creator = Named<TabItem>(window, "CreatorTab");
                var manager = Named<TabItem>(window, "ManagerTab");

                // Choosing a tab switches the screen...
                rail.SelectedItem = manager;
                Assert.Equal(ShellMode.Manager, shell.Mode);

                // ...and the shell switching (Escape, the empty state's button) moves the tab
                shell.ShowCreator();
                Assert.Same(creator, rail.SelectedItem);
                shell.ShowManager();
                Assert.Same(manager, rail.SelectedItem);
            });
        }

        [AvaloniaFact]
        public void TheSettingsTab_WithoutASettingsScreen_KeepsTheScreensTab()
        {
            WithWindow((window, shell) =>
            {
                var rail = Named<RailTabControl>(window, "TabStrip");
                rail.SelectedItem = Named<TabItem>(window, "SettingsTab");

                Assert.Same(Named<TabItem>(window, "CreatorTab"), rail.SelectedItem);
                Assert.Equal(ShellMode.Creator, shell.Mode);
            });
        }

        [AvaloniaFact]
        public void CtrlOneTwoAndThree_PickTheTabs_LeftToRight()
        {
            WithWindow((window, shell) =>
            {
                var rail = Named<RailTabControl>(window, "TabStrip");
                foreach (var (key, mode, tab) in new[]
                {
                    (Key.D2, ShellMode.Manager, "ManagerTab"),
                    (Key.D3, ShellMode.Settings, "SettingsTab"),
                    (Key.D1, ShellMode.Creator, "CreatorTab"),
                    (Key.NumPad2, ShellMode.Manager, "ManagerTab")
                })
                {
                    Press(window, key, PlatformKeys.RawCommand);
                    Assert.Equal(mode, shell.Mode);
                    Assert.Same(Named<TabItem>(window, tab), rail.SelectedItem);
                }

                // Not without Ctrl, nor with Ctrl+Shift
                Press(window, Key.D1, RawInputModifiers.None);
                Press(window, Key.D1, PlatformKeys.RawCommand | RawInputModifiers.Shift);
                Assert.Equal(ShellMode.Manager, shell.Mode);

                // While the icon picker has the tabs off, the keys don't switch either
                shell.ShowSettings();
                shell.Settings!.IsIconPickerOpen = true;
                Dispatcher.UIThread.RunJobs();
                Assert.False(rail.IsEnabled);
                Assert.True(Named<Button>(window, "BtnClose").IsEnabled);
                Press(window, Key.D1, PlatformKeys.RawCommand);
                Assert.Equal(ShellMode.Settings, shell.Mode);
                shell.Settings.IsIconPickerOpen = false;
            }, withSettings: true);
        }

        [AvaloniaFact]
        public void Escape_GoesBackToTheCreator_AndClosesTheWindowFromThere()
        {
            WithWindow((window, shell) =>
            {
                shell.ShowManager();
                Dispatcher.UIThread.RunJobs();
                Press(window, Key.Escape, RawInputModifiers.None);
                Assert.Equal(ShellMode.Creator, shell.Mode);

                var closed = false;
                window.Closed += (sender, e) => closed = true;
                // Only the press: the window is gone before its release
                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.None, null);
                Dispatcher.UIThread.RunJobs();
                Assert.True(closed);
            });
        }

        #endregion The tabs

        #region The caption buttons and the bar

        [AvaloniaFact]
        public void TheCaptionButtons_HaveNamesAndTooltips_AndAreNotTabStops()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows' frame; macOS has its own (OnTheMac_TheFrameIsMacOS)");
            WithWindow((window, shell) =>
            {
                foreach (var (name, expected) in new[] { ("BtnMinimize", "Minimize"), ("BtnMaximize", "Maximize"), ("BtnClose", "Close") })
                {
                    var button = Named<Button>(window, name);
                    Assert.Equal(expected, AutomationProperties.GetName(button));
                    Assert.Equal(expected, ToolTip.GetTip(button));
                    Assert.False(button.IsTabStop);
                    Assert.False(button.Focusable);
                    Assert.Equal(46, button.Bounds.Width);
                    Assert.Equal(48, button.Bounds.Height);
                }
            });
        }

        [AvaloniaFact]
        public void TheBar_Is48High_WithTabsOf40_AndTheCreatorIsMainWidth()
        {
            // Measured in Segoe UI: macOS draws the app in its own font (AppFontFamily)
            Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows font metrics");
            WithWindow((window, shell) =>
            {
                Assert.Equal("keycuts", window.Title);
                Assert.Equal(WindowLayout.CreatorWidth, window.Bounds.Width, 0.5);
                Assert.Equal(48, Named<Grid>(window, "Bar").Bounds.Height);
                foreach (var tab in new[] { "CreatorTab", "ManagerTab", "SettingsTab" })
                {
                    Assert.Equal(40, Named<TabItem>(window, tab).Bounds.Height);
                }

                // #218 asked for about 60 beyond the tabs, and the names at 16 with 24 after keycuts leave about 77
                var bar = Named<Grid>(window, "Bar");
                var rail = Named<RailTabControl>(window, "TabStrip");
                var tabsEnd = rail.TranslatePoint(new Point(rail.Bounds.Width, 0), bar)!.Value.X;
                var captionsStart = Named<Button>(window, "BtnMinimize").TranslatePoint(new Point(0, 0), bar)!.Value.X;
                Assert.InRange(captionsStart - tabsEnd, 65, 90);
            });
        }

        [Fact]
        public void TheCreatorWidth_IsMainWidthInTheTokens()
        {
            var tokens = XDocument.Load(RepoFiles.Find(Path.Combine("keycuts.App", "Themes", "Tokens.axaml")));
            var mainWidth = tokens.Descendants().Single(e => (string?)e.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) == "MainWidth");
            Assert.Equal(WindowLayout.CreatorWidth, double.Parse(mainWidth.Value.Trim(), System.Globalization.CultureInfo.InvariantCulture));
        }

        // The keycap alone at 36, in no socket (docs/mockups/icon-size-v2, R3)
        // macOS: ⌘, opens Settings, as in every Mac app, and ⌘W closes the window
        [AvaloniaFact]
        public void OnTheMac_CommandCommaOpensSettings_AndCommandWCloses()
        {
            Assert.SkipUnless(OperatingSystem.IsMacOS(), "macOS keys");
            WithWindow((window, shell) =>
            {
                Press(window, Key.OemComma, RawInputModifiers.Meta);
                Assert.Equal(ShellMode.Settings, shell.Mode);
                var closed = false;
                window.Closed += (sender, e) => closed = true;
                // Closed on the press, so there is no window to release the key on
                window.KeyPress(Key.W, RawInputModifiers.Meta, PhysicalKey.None, null);
                Dispatcher.UIThread.RunJobs();
                Assert.True(closed);
            }, withSettings: true);
        }

        [AvaloniaFact]
        public void TheAppIcon_InTheBar_IsTheOneFromSettings_WithoutItsBackground_At36()
        {
            WithWindow((window, shell) =>
            {
                var icon = Named<Image>(window, "AppIcon");
                Assert.Same(AppIcons.GetPreview(AppIcons.GetTransparent(AppIconManager.Current)), icon.Source);
                Assert.Equal(36, icon.Bounds.Width);
                Assert.Equal(36, icon.Bounds.Height);
                Assert.Equal(OperatingSystem.IsMacOS() ? RailView.MacBrandLeft : 8, ((StackPanel)icon.Parent!).Margin.Left);
            });
        }

        // Phase 5.1 (mockup macos-window-v1): macOS's own frame and traffic lights, zoom off, no caption buttons, no torn corner
        [AvaloniaFact]
        public void OnTheMac_TheFrameIsMacOS()
        {
            Assert.SkipUnless(OperatingSystem.IsMacOS(), "macOS only");
            WithWindow((window, shell) =>
            {
                Assert.Equal(WindowDecorations.Full, window.WindowDecorations);
                Assert.True(window.ExtendClientAreaToDecorationsHint);
                Assert.Equal(48, window.ExtendClientAreaTitleBarHeightHint);
                Assert.False(window.CanMaximize);
                Assert.False(Named<StackPanel>(window, "CaptionButtons").IsVisible);
                Assert.False(Named<Border>(window, "PageBorder").IsVisible);
                Assert.False(Named<TornCorner>(window, "Tear").IsVisible);
                Assert.Equal(88, RailView.MacBrandLeft);
            });
        }

        #endregion The caption buttons and the bar

        #region Sizes

        [AvaloniaFact]
        public void SettingsFromTheCreator_TakesTheLargeSize()
        {
            WithWindow((window, shell) =>
            {
                shell.ShowSettings();
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(WindowLayout.ManagerDefaultWidth, window.Bounds.Width, 0.5);
                Assert.Equal(WindowLayout.ManagerDefaultHeight, window.Bounds.Height, 0.5);
                Assert.True(window.CanResize);
                Assert.Equal(WindowLayout.ManagerMinWidth, window.MinWidth);
                Assert.Equal(WindowLayout.ManagerMinHeight, window.MinHeight);
                Assert.Equal(SizeToContent.Manual, window.SizeToContent);

                // The Creator is small again
                shell.ShowCreator();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(WindowLayout.CreatorWidth, window.Bounds.Width, 0.5);
                Assert.Equal(SizeToContent.Height, window.SizeToContent);
                Assert.False(window.CanResize);
            }, withSettings: true);
        }

        [AvaloniaFact]
        public void TheManagerAndSettings_ShareOneSize_AcrossTheCreator()
        {
            WithWindow((window, shell) =>
            {
                shell.ShowManager();
                Dispatcher.UIThread.RunJobs();
                shell.ShowSettings();
                Dispatcher.UIThread.RunJobs();
                window.Width = 940;
                window.Height = 600;
                Dispatcher.UIThread.RunJobs();
                shell.ShowCreator();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(WindowLayout.CreatorWidth, window.Bounds.Width, 0.5);
                shell.ShowManager();
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(940, window.Bounds.Width, 0.5);
                Assert.Equal(600, window.Bounds.Height, 0.5);
            }, withSettings: true);
        }

        [AvaloniaFact]
        public void TheCreator_KeepsTheTopLeft_TheManagerWasMovedTo()
        {
            WithWindow((window, shell) =>
            {
                shell.ShowManager();
                Dispatcher.UIThread.RunJobs();
                window.Position = new PixelPoint(40, 30);
                Dispatcher.UIThread.RunJobs();
                shell.ShowCreator();
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(new PixelPoint(40, 30), window.Position);
            });
        }

        [Fact]
        public void ClampToWorkArea_ShrinksThenSlidesOntoTheScreen()
        {
            var workArea = new Rect(0, 0, 1024, 728);
            Assert.Equal(new Rect(256, 0, 768, 470), WindowLayout.ClampToWorkArea(new Rect(900, -40, 768, 470), workArea));
            Assert.Equal(new Rect(0, 0, 1024, 728), WindowLayout.ClampToWorkArea(new Rect(10, 10, 1400, 900), workArea));
        }

        // Two columns at every size the window takes, the smallest included (the owner's pick, 2026-11-17: no stacking)
        [AvaloniaTheory]
        [InlineData(960, 544)]
        [InlineData(WindowLayout.ManagerDefaultWidth, WindowLayout.ManagerDefaultHeight)]
        [InlineData(WindowLayout.ManagerMinWidth, WindowLayout.ManagerMinHeight)]
        public void Settings_IsTwoColumns_AndFitsWithoutScrolling(double width, double height)
        {
            WithWindow((window, shell) =>
            {
                shell.ShowSettings();
                Dispatcher.UIThread.RunJobs();
                window.Width = width;
                window.Height = height;
                Dispatcher.UIThread.RunJobs();

                var view = window.GetVisualDescendants().OfType<SettingsView>().Single();
                var labels = view.GetVisualDescendants().OfType<SectionLabel>().ToList();
                var output = labels.Single(l => l.Text == "Output").TranslatePoint(new Point(0, 0), view)!.Value;
                var appearance = labels.Single(l => l.Text == "Appearance").TranslatePoint(new Point(0, 0), view)!.Value;
                Assert.Equal(output.Y, appearance.Y, 0.5);
                Assert.True(appearance.X > view.Bounds.Width / 2);

                // The left column's controls end before the rule, and the right one's buttons end inside the page
                var folder = Named<TextBox>(window, "TextboxOutputFolder");
                var ruleX = Named<Border>(window, "ColumnRule").TranslatePoint(new Point(0, 0), view)!.Value.X;
                Assert.True(folder.TranslatePoint(new Point(folder.Bounds.Width, 0), view)!.Value.X <= ruleX - 24 + 0.5);
                var restore = Named<Button>(window, "BtnRestoreDefaults");
                Assert.True(restore.TranslatePoint(new Point(restore.Bounds.Width, 0), view)!.Value.X <= view.Bounds.Width - 24 + 0.5);

                // All of it fits, without scrolling
                var scroller = Named<ScrollViewer>(window, "BodyScroller");
                Assert.Equal(0, scroller.Extent.Height - scroller.Viewport.Height, 0.5);
            }, withSettings: true);
        }

        #endregion Sizes

        #region The Manager

        // The table is whole rows, and what is left goes under it: 24 at the size the window opens at, and a whole number of rows taller
        [AvaloniaTheory]
        [InlineData(WindowLayout.ManagerMinHeight)]
        [InlineData(544)]
        public void Manager_Has24UnderItsLastRow(double height)
        {
            // Measured in Segoe UI: macOS draws the app in its own font (AppFontFamily)
            Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows font metrics");
            WithWindow((window, shell) =>
            {
                shell.ShowManager();
                Dispatcher.UIThread.RunJobs();
                window.Height = height;
                Dispatcher.UIThread.RunJobs();
                Dispatcher.UIThread.RunJobs();

                var host = Named<Border>(window, "TableHost");
                var bottom = host.TranslatePoint(new Point(0, host.Bounds.Height), window)!.Value.Y;
                // Avalonia's text sets the table 1px lower than WPF's, and the fit's 1px of slack (FitToRows) takes it from the 24
                Assert.InRange(window.Bounds.Height - bottom, 23, 24.5);
            }, withKeycuts: true);
        }

        // As the window widens the fixed columns come back one at a time: Runs, then Last run, then Modified
        [AvaloniaTheory]
        [InlineData(720, "")]
        [InlineData(767, "")]
        [InlineData(WindowLayout.ManagerDefaultWidth, "Runs")]
        [InlineData(827, "Runs")]
        [InlineData(828, "Runs Last run")]
        [InlineData(1088, "Runs Last run Modified")]
        public void Manager_ShowsItsFixedColumns_OneAtATime(double width, string expected)
        {
            WithWindow((window, shell) =>
            {
                shell.ShowManager();
                Dispatcher.UIThread.RunJobs();
                window.Width = width;
                Dispatcher.UIThread.RunJobs();

                var table = Named<DataGrid>(window, "Table");
                var shown = new[] { 4, 5, 6 }
                    .Where(i => table.Columns[i].IsVisible)
                    .Select(i => ((SortHeaderButton)table.Columns[i].Header!).Content);
                Assert.Equal(expected, string.Join(" ", shown));
            }, withKeycuts: true);
        }

        [AvaloniaFact]
        public void Manager_ShowsItsRows_AndUpDownMoveTheSelection_FromTheSearchBox()
        {
            WithWindow((window, shell) =>
            {
                shell.ShowManager();
                shell.ReloadAsync().GetAwaiter().GetResult();
                Dispatcher.UIThread.RunJobs();

                var table = Named<DataGrid>(window, "Table");
                var rows = table.GetVisualDescendants().OfType<DataGridRow>().ToList();
                Assert.Equal(2, rows.Count);
                Assert.Equal("2 keycuts", shell.Manager.CountText);

                var search = Named<TextBox>(window, "SearchBox");
                Assert.True(search.IsEnabled);
                search.Focus();
                Press(window, Key.Down, RawInputModifiers.None);
                Assert.NotNull(shell.Manager.SelectedRow);
                Assert.Same(search, window.FocusManager!.GetFocusedElement());
            }, withKeycuts: true);
        }

        #endregion The Manager

        #region Helpers

        internal static T Named<T>(Visual root, string name) where T : Control
        {
            return root.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name)
                ?? throw new InvalidOperationException("No " + typeof(T).Name + " named " + name);
        }

        static WindowDecorationsElementRole Role(Visual root, string name)
        {
            return WindowDecorationProperties.GetElementRole(Named<Control>(root, name));
        }

        internal static void Press(Window window, Key key, RawInputModifiers modifiers)
        {
            window.KeyPress(key, modifiers, PhysicalKey.None, null);
            window.KeyRelease(key, modifiers, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();
        }

        sealed class MemorySettingsStore : ISettingsStore
        {
            public Settings Load()
            {
                return new Settings { OutputFolder = Path.GetTempPath() };
            }

            public string? SaveOptions(Settings settings)
            {
                return null;
            }

            public FolderSaveResult SaveOutputFolder(string folder)
            {
                return new FolderSaveResult(true, null);
            }
        }

        internal static IReadOnlyList<Keycut> FixtureKeycuts()
        {
            return new[]
            {
                new Keycut { Name = "gmail", Kind = KeycutKind.Url, RawType = ShortcutType.Url, Target = "https://mail.google.com", FilePath = Path.Combine(Path.GetTempPath(), "gmail.bat"), Description = "Inbox" },
                new Keycut { Name = "dl", Kind = KeycutKind.Folder, RawType = ShortcutType.Folder, Target = Path.GetTempPath(), FilePath = Path.Combine(Path.GetTempPath(), "dl.bat") }
            };
        }

        internal static void WithWindow(Action<MainWindow, ShellViewModel> body, bool withKeycuts = false, bool withSettings = false)
        {
            ThemeManager.Apply(Palette.Light);
            Func<CancellationToken, Task<IReadOnlyList<Keycut>>>? loader = withKeycuts ? _ => Task.FromResult(FixtureKeycuts()) : null;
            // A Settings screen over a store in memory: nothing reads or writes the registry
            Func<SettingsViewModel>? settings = withSettings ? () => new SettingsViewModel(new MemorySettingsStore()) : null;
            var shell = new ShellViewModel(() => Task.FromResult(withKeycuts ? 2 : 3), loader, settingsFactory: settings);
            var window = new MainWindow(shell);
            window.Show();
            Dispatcher.UIThread.RunJobs();
            try
            {
                body(window, shell);
            }
            finally
            {
                if (window.IsVisible)
                {
                    window.Close();
                }
            }
        }

        #endregion Helpers
    }
}
