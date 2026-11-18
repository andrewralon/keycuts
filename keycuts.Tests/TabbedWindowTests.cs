#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.Controls;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// #218: the tab bar that replaces the title bar. The window has to do everything its frame did, so these ask the real window the
    /// question Windows asks (<c>WM_NCHITTEST</c>) and look at what it answers: caption over the icon, name and empty bar, client over
    /// the tabs and caption buttons, and a resize edge only where the window can be resized. The window is never maximized, on any screen.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class TabbedWindowTests
    {
        private const int WmNcHitTest = 0x0084;
        private const int HtClient = 1;
        private const int HtCaption = 2;
        private const int HtMaxButton = 9;
        private const int HtLeft = 10;
        private const int GwlStyle = -16;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        #region Hit testing

        [Fact]
        public void TheBar_IsTheCaption_ExceptTheTabsAndTheCaptionButtons()
        {
            WithWindow((window, shell) =>
            {
                Assert.Equal(HtCaption, HitTest(window, "Bar", 30, 24));   // the icon and name
                Assert.Equal(HtCaption, HitTest(window, "Bar", 400, 24));  // the empty bar
                Assert.Equal(HtClient, HitTest(window, "CreatorTab", 20, 20));
                Assert.Equal(HtClient, HitTest(window, "ManagerTab", 20, 20));
                Assert.Equal(HtClient, HitTest(window, "SettingsTab", 20, 20));
                Assert.Equal(HtClient, HitTest(window, "BtnMinimize", 23, 24));
                Assert.Equal(HtClient, HitTest(window, "BtnClose", 23, 24));
                Assert.Equal(HtClient, HitTest(window, "Frame", 100, 200));  // the page
            });
        }

        [Fact]
        public void TheMaximizeButton_IsNeverTheMaximizeButtonToWindows()
        {
            WithWindow((window, shell) =>
            {
                // Windows 11 would open its snap layouts over a point that answers HTMAXBUTTON, and a click there would maximize
                foreach (var show in new Action[] { shell.ShowCreator, shell.ShowManager, shell.ShowSettings })
                {
                    show();
                    Settle(window);
                    Assert.NotEqual(HtMaxButton, HitTest(window, "BtnMaximize", 23, 24));
                }
            }, withSettings: true);
        }

        [Fact]
        public void ResizeEdges_ExistOnTheManagerOnly()
        {
            WithWindow((window, shell) =>
            {
                // The Creator is ResizeMode.CanMinimize: WindowChrome would still answer a resize edge (cursor and all) at its border
                Assert.Equal(HtClient, HitTest(window, "Frame", 1, 200));

                shell.ShowManager();
                Settle(window);
                Assert.Equal(HtLeft, HitTest(window, "Frame", 1, 200));

                shell.ShowCreator();
                Settle(window);
                Assert.Equal(HtClient, HitTest(window, "Frame", 1, 200));
            });
        }

        #endregion Hit testing

        #region Never maximized

        [Fact]
        public void TheWindow_IsNeverMaximized_OnAnyScreen()
        {
            WithWindow((window, shell) =>
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                foreach (var show in new Action[] { shell.ShowCreator, shell.ShowManager, shell.ShowSettings, shell.ShowManager, shell.ShowCreator })
                {
                    show();
                    Settle(window);
                    var screen = shell.Mode.ToString();

                    // No maximize box, though the Manager is resizable: Windows offers no maximize (double click, Win+Up, a drag to the top, the window menu)
                    Assert.True((GetWindowLong(hwnd, GwlStyle) & NoMaximizeHook.WsMaximizeBox) == 0, screen);

                    // The command itself is refused, both the plain one and the caption double click's (SC_MAXIMIZE | HTCAPTION)
                    SendMessage(hwnd, NoMaximizeHook.WmSysCommand, (IntPtr)NoMaximizeHook.ScMaximize, IntPtr.Zero);
                    Settle(window);
                    Assert.Equal(WindowState.Normal, window.WindowState);
                    SendMessage(hwnd, NoMaximizeHook.WmSysCommand, (IntPtr)(NoMaximizeHook.ScMaximize | HtCaption), IntPtr.Zero);
                    Settle(window);
                    Assert.Equal(WindowState.Normal, window.WindowState);

                    // And setting the state directly is undone
                    window.WindowState = WindowState.Maximized;
                    Settle(window);
                    Assert.Equal(WindowState.Normal, window.WindowState);
                }

                // The Manager is still resizable, from its edges
                shell.ShowManager();
                Settle(window);
                Assert.Equal(ResizeMode.CanResize, window.ResizeMode);
                Assert.Equal(HtLeft, HitTest(window, "Frame", 1, 200));
            }, withSettings: true);
        }

        [Theory]
        [InlineData(0x16CF0000, 0x16CE0000)]   // WS_OVERLAPPEDWINDOW | WS_VISIBLE | WS_CLIPCHILDREN: the box comes off
        [InlineData(0x16CE0000, 0x16CE0000)]   // already without it
        [InlineData(unchecked((int)0x96CF0000), unchecked((int)0x96CE0000))]  // WS_POPUP, the top bit, survives
        public void WithoutMaximizeBox_TakesOffOnlyThatBit(int style, int expected)
        {
            Assert.Equal(expected, NoMaximizeHook.WithoutMaximizeBox(style));
        }

        [Theory]
        [InlineData(0xF030, true)]    // SC_MAXIMIZE
        [InlineData(0xF032, true)]    // a double click on the caption
        [InlineData(0xF120, false)]   // SC_RESTORE
        [InlineData(0xF020, false)]   // SC_MINIMIZE
        [InlineData(0xF012, false)]   // SC_MOVE from the caption (a drag)
        [InlineData(0xF000, false)]   // SC_SIZE
        public void IsMaximizeCommand_ReadsTheCommandWithoutItsLowBits(int command, bool expected)
        {
            Assert.Equal(expected, NoMaximizeHook.IsMaximizeCommand((IntPtr)command));
        }

        #endregion Never maximized

        #region No system caption

        [Theory]
        [InlineData(0x00AE, true)]    // WM_NCUAHDRAWCAPTION: the classic caption buttons, painted over the tab bar
        [InlineData(0x00AF, true)]    // WM_NCUAHDRAWFRAME
        [InlineData(0x0085, false)]   // WM_NCPAINT
        [InlineData(0x0086, false)]   // WM_NCACTIVATE, which WindowChrome handles
        [InlineData(0x00A1, false)]   // WM_NCLBUTTONDOWN, which starts a drag
        public void IsSystemCaptionPaint_IsOnlyTheThemeCaptionAndFrameMessages(int msg, bool expected)
        {
            Assert.Equal(expected, NoSystemCaptionHook.IsSystemCaptionPaint(msg));
        }

        #endregion No system caption

        #region The tabs

        [Fact]
        public void TheTabs_AreATabList_WithNamesAndSelectedState()
        {
            WithWindow((window, shell) =>
            {
                var rail = (RailTabControl)Named(window, "TabStrip");
                var creator = (TabItem)Named(window, "CreatorTab");
                var manager = (TabItem)Named(window, "ManagerTab");
                var settings = (TabItem)Named(window, "SettingsTab");

                Assert.Equal(new[] { creator, manager, settings }, rail.Items.Cast<TabItem>());
                Assert.Equal(AutomationControlType.Tab, UIElementAutomationPeer.CreatePeerForElement(rail).GetAutomationControlType());

                foreach (var (tab, tabName, name) in new[] { (creator, "CreatorTab", "creator"), (manager, "ManagerTab", "manager"), (settings, "SettingsTab", "settings") })
                {
                    var peer = TabPeer(window, tabName);
                    Assert.Equal(AutomationControlType.TabItem, peer.GetAutomationControlType());
                    Assert.Equal(name, peer.GetName());
                    // The key that picks it, in the tooltip after the name and as the accelerator a screen reader reads
                    var number = Array.IndexOf(new[] { "creator", "manager", "settings" }, name) + 1;
                    Assert.Equal(name + " (Ctrl+" + number + ")", tab.ToolTip);
                    Assert.Equal("Ctrl+" + number, AutomationProperties.GetAcceleratorKey(tab));
                }

                // The gear is the tab, with no text of its own
                Assert.Empty(Descendants<TextBlock>(settings));

                Assert.True(((ISelectionItemProvider)TabPeer(window, "CreatorTab").GetPattern(PatternInterface.SelectionItem)).IsSelected);
                Assert.False(((ISelectionItemProvider)TabPeer(window, "ManagerTab").GetPattern(PatternInterface.SelectionItem)).IsSelected);
            });
        }

        [Fact]
        public void TheTabs_FollowTheShell_AndSwitchIt()
        {
            WithWindow((window, shell) =>
            {
                var rail = (RailTabControl)Named(window, "TabStrip");
                var creator = (TabItem)Named(window, "CreatorTab");
                var manager = (TabItem)Named(window, "ManagerTab");
                Assert.Same(creator, rail.SelectedItem);

                // Choosing a tab switches the screen...
                rail.SelectedItem = manager;
                Assert.Equal(ShellMode.Manager, shell.Mode);

                // ...and the shell switching (Escape, the empty state's button) moves the tab
                shell.ShowCreator();
                Assert.Same(creator, rail.SelectedItem);
                shell.ShowManager();
                Assert.Same(manager, rail.SelectedItem);

                // Through UI Automation, as a screen reader does it
                ((ISelectionItemProvider)TabPeer(window, "CreatorTab").GetPattern(PatternInterface.SelectionItem)).Select();
                Assert.Equal(ShellMode.Creator, shell.Mode);
            });
        }

        [Fact]
        public void TheSettingsTab_ShowsTheSettingsScreen_AtTheManagersSize()
        {
            WithWindow((window, shell) =>
            {
                var rail = (RailTabControl)Named(window, "TabStrip");
                var settings = (TabItem)Named(window, "SettingsTab");
                var maximize = (Button)Named(window, "BtnMaximize");

                shell.ShowManager();
                Settle(window);
                window.Width = 1000;
                window.Height = 640;
                Settle(window);
                var where = new Point(window.Left, window.Top);

                rail.SelectedItem = settings;
                Settle(window);

                Assert.Equal(ShellMode.Settings, shell.Mode);
                Assert.Same(settings, rail.SelectedItem);
                Assert.Contains(Descendants<keycuts.GUI.Views.SettingsView>(window), v => ReferenceEquals(v.DataContext, shell.Settings));

                // The large size the Manager left it at: nothing moves, nothing grows to fit Settings, and it is still resizable
                Assert.Equal(1000, window.ActualWidth, 0.5);
                Assert.Equal(640, window.ActualHeight, 0.5);
                Assert.Equal(where, new Point(window.Left, window.Top));
                Assert.Equal(SizeToContent.Manual, window.SizeToContent);
                Assert.Equal(ResizeMode.CanResize, window.ResizeMode);
                Assert.False(maximize.IsEnabled);
                Assert.Equal(double.PositiveInfinity, window.MaxHeight);

                // The Creator is small again
                shell.ShowCreator();
                Settle(window);
                Assert.Equal(WindowLayout.CreatorWidth, window.ActualWidth, 0.5);
                Assert.Equal(SizeToContent.Height, window.SizeToContent);
                Assert.Equal(ResizeMode.CanMinimize, window.ResizeMode);
            }, withSettings: true);
        }

        // The table is whole rows, and what is left goes under it: 24 at the size the window opens at (its smallest), with no part
        // row's worth added, and the same 24 a whole number of rows taller (the old default, 544)
        [Theory]
        [InlineData(WindowLayout.ManagerMinHeight)]
        [InlineData(544)]
        public void Manager_Has24UnderItsLastRow(double height)
        {
            WithWindow((window, shell) =>
            {
                shell.ShowManager();
                Settle(window);
                Assert.Equal(WindowLayout.ManagerMinHeight, window.ActualHeight, 0.5);
                window.Height = height;
                Settle(window);

                var host = (FrameworkElement)Named(window, "TableHost");
                var under = window.ActualHeight - host.TranslatePoint(new Point(0, host.ActualHeight), window).Y;
                Assert.Equal(24, under, 0.5);
            }, withKeycuts: true);
        }

        // #187: the window opens just wide enough for Runs, the count a cleanup sorts by, and no other fixed column
        [Fact]
        public void Manager_OpensWideEnoughForRuns_AndNoOtherFixedColumn()
        {
            WithWindow((window, shell) =>
            {
                shell.ShowManager();
                Settle(window);

                Assert.Equal(768, window.ActualWidth, 0.5);
                var table = (DataGrid)Named(window, "Table");
                var shown = new[] { 4, 5, 6 }
                    .Where(i => table.Columns[i].Visibility == Visibility.Visible)
                    .Select(i => ((ContentControl)table.Columns[i].Header).Content);
                Assert.Equal(new object[] { "Runs" }, shown);
            }, withKeycuts: true);
        }

        // As the window widens the fixed columns come back one at a time: Runs, then Last run, then Modified
        [Theory]
        [InlineData(720, "")]
        [InlineData(767, "")]
        [InlineData(768, "Runs")]
        [InlineData(827, "Runs")]
        [InlineData(828, "Runs Last run")]
        [InlineData(1087, "Runs Last run")]
        [InlineData(1088, "Runs Last run Modified")]
        public void Manager_ShowsItsFixedColumns_OneAtATime(double width, string expected)
        {
            WithWindow((window, shell) =>
            {
                shell.ShowManager();
                Settle(window);
                window.Width = width;
                Settle(window);

                var table = (DataGrid)Named(window, "Table");
                var shown = new[] { 4, 5, 6 }
                    .Where(i => table.Columns[i].Visibility == Visibility.Visible)
                    .Select(i => ((ContentControl)table.Columns[i].Header).Content);
                Assert.Equal(expected, string.Join(" ", shown));
            }, withKeycuts: true);
        }

        [Fact]
        public void SettingsFromTheCreator_TakesTheLargeSize()
        {
            WithWindow((window, shell) =>
            {
                shell.ShowSettings();
                Settle(window);

                Assert.Equal(WindowLayout.ManagerDefaultWidth, window.ActualWidth, 0.5);
                Assert.Equal(WindowLayout.ManagerDefaultHeight, window.ActualHeight, 0.5);
                Assert.Equal(ResizeMode.CanResize, window.ResizeMode);
                Assert.Equal(WindowLayout.ManagerMinWidth, window.MinWidth);
                Assert.Equal(WindowLayout.ManagerMinHeight, window.MinHeight);
            }, withSettings: true);
        }

        // Two columns at every size the window takes, the smallest included (the owner's pick, 2026-11-17: no stacking)
        [Theory]
        [InlineData(960, 544)]
        [InlineData(WindowLayout.ManagerDefaultWidth, WindowLayout.ManagerDefaultHeight)]
        [InlineData(WindowLayout.ManagerMinWidth, WindowLayout.ManagerMinHeight)]
        public void Settings_IsTwoColumns_AndFitsWithoutScrolling(double width, double height)
        {
            WithWindow((window, shell) =>
            {
                shell.ShowSettings();
                Settle(window);
                window.Width = width;
                window.Height = height;
                Settle(window);

                var view = Descendants<keycuts.GUI.Views.SettingsView>(window).Single();

                // Output on the left and Appearance on the right, their section labels level at the top of the page
                var output = Descendants<SectionLabel>(view).Single(l => l.Text == "Output");
                var appearance = Descendants<SectionLabel>(view).Single(l => l.Text == "Appearance");
                var outputAt = output.TranslatePoint(new Point(0, 0), view);
                var appearanceAt = appearance.TranslatePoint(new Point(0, 0), view);
                Assert.Equal(outputAt.Y, appearanceAt.Y, 0.5);
                Assert.True(appearanceAt.X > view.ActualWidth / 2);

                // The left column's toggles end before the rule, and the right one's buttons end inside the page
                var folder = (FrameworkElement)Named(window, "TextboxOutputFolder");
                var rule = (FrameworkElement)Named(window, "ColumnRule");
                Assert.Equal(Visibility.Visible, rule.Visibility);
                var ruleX = rule.TranslatePoint(new Point(0, 0), view).X;
                Assert.True(folder.TranslatePoint(new Point(folder.ActualWidth, 0), view).X <= ruleX - 24 + 0.5);
                var restore = (FrameworkElement)Named(window, "BtnRestoreDefaults");
                Assert.True(restore.TranslatePoint(new Point(restore.ActualWidth, 0), view).X <= view.ActualWidth - 24 + 0.5);

                // All of it fits, without scrolling
                Assert.Equal(0, ((ScrollViewer)Named(window, "BodyScroller")).ScrollableHeight, 0.5);
            }, withSettings: true);
        }

        [Fact]
        public void TheManagerAndSettings_ShareOneSize_AcrossTheCreator()
        {
            WithWindow((window, shell) =>
            {
                shell.ShowManager();
                Settle(window);
                window.Width = 900;
                window.Height = 640;
                Settle(window);

                // Resized in Settings, then the Creator, then the Manager again: the Manager comes back at Settings' size.
                // Both sizes fit a 1024 × 768 screen (CI's), since a large size is clamped to the work area.
                shell.ShowSettings();
                Settle(window);
                window.Width = 940;
                window.Height = 600;
                Settle(window);
                shell.ShowCreator();
                Settle(window);
                Assert.Equal(WindowLayout.CreatorWidth, window.ActualWidth, 0.5);
                shell.ShowManager();
                Settle(window);

                Assert.Equal(940, window.ActualWidth, 0.5);
                Assert.Equal(600, window.ActualHeight, 0.5);

                // And Settings from the Creator comes back at it too
                shell.ShowCreator();
                Settle(window);
                shell.ShowSettings();
                Settle(window);
                Assert.Equal(940, window.ActualWidth, 0.5);
                Assert.Equal(600, window.ActualHeight, 0.5);
            }, withSettings: true);
        }

        [Fact]
        public void TheCreator_KeepsTheTopLeft_TheManagerWasMovedTo()
        {
            WithWindow((window, shell) =>
            {
                shell.ShowManager();
                Settle(window);

                // Moved up and to the left, still on the screen: the Creator opens right there, not where it was before
                var workArea = WindowLayout.GetWorkAreaInDips(window);
                window.Left = workArea.Left + 8;
                window.Top = workArea.Top + 4;
                Settle(window);
                shell.ShowCreator();
                Settle(window);

                Assert.Equal(workArea.Left + 8, window.Left, 0.5);
                Assert.Equal(workArea.Top + 4, window.Top, 0.5);
            });
        }

        /// <summary>A key that reaches the window, as one no control took bubbles up to it. True if the window took it.</summary>
        static bool KeyDown(UIElement source, Key key, ModifierKeys modifiers)
        {
            var args = new KeyEventArgs(new FakeKeyboardDevice(modifiers), PresentationSource.FromVisual(source), 0, key)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };
            source.RaiseEvent(args);
            return args.Handled;
        }

        [Fact]
        public void CtrlOneTwoAndThree_PickTheTabs_LeftToRight()
        {
            WithWindow((window, shell) =>
            {
                var rail = (RailTabControl)Named(window, "TabStrip");

                // From anywhere in the window, a text box included, and from the number pad too
                foreach (var (key, mode, tab) in new[]
                {
                    (Key.D2, ShellMode.Manager, "ManagerTab"),
                    (Key.D3, ShellMode.Settings, "SettingsTab"),
                    (Key.D1, ShellMode.Creator, "CreatorTab"),
                    (Key.NumPad2, ShellMode.Manager, "ManagerTab")
                })
                {
                    var box = Descendants<TextBox>(window).First(t => t.IsVisible);
                    Assert.True(KeyDown(box, key, ModifierKeys.Control), key + " is taken");
                    Settle(window);
                    Assert.Equal(mode, shell.Mode);
                    Assert.Same(Named(window, tab), rail.SelectedItem);
                }

                // Not without Ctrl, nor with Ctrl+Shift
                Assert.False(KeyDown(window, Key.D1, ModifierKeys.None));
                Assert.False(KeyDown(window, Key.D1, ModifierKeys.Control | ModifierKeys.Shift));
                Assert.Equal(ShellMode.Manager, shell.Mode);

                // While the icon picker has the tabs off, the keys don't switch either
                shell.ShowSettings();
                Settle(window);
                shell.Settings!.IsIconPickerOpen = true;
                Settle(window);
                Assert.True(KeyDown(window, Key.D1, ModifierKeys.Control));
                Settle(window);
                Assert.Equal(ShellMode.Settings, shell.Mode);
                shell.Settings.IsIconPickerOpen = false;
            }, withSettings: true);
        }

        [Fact]
        public void WhileTheIconPickerIsOpen_TheTabsAreOff_ButTheCaptionButtonsArent()
        {
            WithWindow((window, shell) =>
            {
                shell.ShowSettings();
                Settle(window);
                shell.Settings!.IsIconPickerOpen = true;
                Settle(window);

                Assert.False(((RailTabControl)Named(window, "TabStrip")).IsEnabled);
                Assert.True(((Button)Named(window, "BtnMinimize")).IsEnabled);
                Assert.True(((Button)Named(window, "BtnClose")).IsEnabled);
                // Dragging is the bar's, which the tabs being off doesn't change
                Assert.Equal(HtCaption, HitTest(window, "Bar", 400, 24));

                shell.Settings.IsIconPickerOpen = false;
                Settle(window);
                Assert.True(((RailTabControl)Named(window, "TabStrip")).IsEnabled);
            }, withSettings: true);
        }

        [Fact]
        public void TheSettingsTab_WithoutASettingsScreen_KeepsTheScreensTab()
        {
            WithWindow((window, shell) =>
            {
                var rail = (RailTabControl)Named(window, "TabStrip");
                var creator = (TabItem)Named(window, "CreatorTab");

                rail.SelectedItem = (TabItem)Named(window, "SettingsTab");

                Assert.Same(creator, rail.SelectedItem);
                Assert.Equal(ShellMode.Creator, shell.Mode);
            });
        }

        [Fact]
        public void ArrowingThroughTheTabs_KeepsTheFocusOnTheTabs_WhileAClickLandsInThePage()
        {
            WithWindow((window, shell) =>
            {
                var creator = (TabItem)Named(window, "CreatorTab");
                var manager = (TabItem)Named(window, "ManagerTab");

                // An arrow on a tab: the key reaches the tab list, and keyboard navigation moves the focus, which selects the tab.
                // The navigation itself belongs to the input manager, which a raised event doesn't pass through, so it is done here.
                void Arrow(TabItem from, FocusNavigationDirection direction)
                {
                    var key = direction == FocusNavigationDirection.Right ? Key.Right : Key.Left;
                    var source = PresentationSource.FromVisual(window)!;
                    from.RaiseEvent(new KeyEventArgs(new FakeKeyboardDevice(), source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                    from.MoveFocus(new TraversalRequest(direction));
                    Settle(window);
                }

                Keyboard.Focus(creator);
                Settle(window);

                Arrow(creator, FocusNavigationDirection.Right);
                Assert.Equal(ShellMode.Manager, shell.Mode);
                Assert.Same(manager, Keyboard.FocusedElement);

                // The search box turns on when the load ends, and still doesn't take the focus from the tabs
                shell.ReloadAsync().GetAwaiter().GetResult();
                Settle(window);
                Assert.True(Descendants<TextBox>(window).Single(t => t.Name == "SearchBox").IsEnabled);
                Assert.Same(manager, Keyboard.FocusedElement);

                // So the next arrow still moves through the tabs
                Arrow(manager, FocusNavigationDirection.Left);
                Assert.Equal(ShellMode.Creator, shell.Mode);
                Assert.Same(creator, Keyboard.FocusedElement);

                // A click on a tab goes on into the page, as the old switch buttons did: the Manager's search box
                manager.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
                Keyboard.Focus(manager);
                Settle(window);
                shell.ReloadAsync().GetAwaiter().GetResult();
                Settle(window);
                Assert.Equal(ShellMode.Manager, shell.Mode);
                Assert.Equal("SearchBox", (Keyboard.FocusedElement as FrameworkElement)?.Name);
            }, withKeycuts: true);
        }

        [Theory]
        [InlineData(Key.Tab, ModifierKeys.Control, true)]
        [InlineData(Key.Tab, ModifierKeys.Control | ModifierKeys.Shift, true)]
        [InlineData(Key.PageUp, ModifierKeys.Control, true)]
        [InlineData(Key.PageDown, ModifierKeys.Control, true)]
        [InlineData(Key.Tab, ModifierKeys.None, false)]
        [InlineData(Key.PageDown, ModifierKeys.None, false)]
        [InlineData(Key.Right, ModifierKeys.Control, false)]
        [InlineData(Key.D1, ModifierKeys.Control, false)]
        public void TheTabs_LeaveOutTheTabCyclingShortcuts(Key key, ModifierKeys modifiers, bool ignored)
        {
            // #218 declined Ctrl+Tab and Ctrl+1/2/3 for now (maybe later); a TabControl brings the first on its own
            Assert.Equal(ignored, RailTabControl.IsTabCycleKey(key, modifiers));
        }

        [Fact]
        public void TheTabs_AreOneTabStop()
        {
            WithWindow((window, shell) =>
            {
                var rail = (RailTabControl)Named(window, "TabStrip");

                Assert.Equal(KeyboardNavigationMode.Once, KeyboardNavigation.GetTabNavigation(rail));
                Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetDirectionalNavigation(rail));
            });
        }

        #endregion The tabs

        #region The caption buttons

        [Fact]
        public void TheCaptionButtons_HaveNamesAndTooltips_AndAreNotTabStops()
        {
            WithWindow((window, shell) =>
            {
                foreach (var (name, expected) in new[] { ("BtnMinimize", "Minimize"), ("BtnMaximize", "Maximize"), ("BtnClose", "Close") })
                {
                    var button = (Button)Named(window, name);
                    Assert.Equal(expected, AutomationProperties.GetName(button));
                    Assert.Equal(expected, button.ToolTip);
                    Assert.False(button.IsTabStop);
                    Assert.False(button.Focusable);
                    Assert.Equal(46, button.Width);
                    Assert.Equal(48, button.Height);
                }
            });
        }

        [Fact]
        public void Maximize_IsDisabledOnEveryScreen_TheResizableManagerToo()
        {
            WithWindow((window, shell) =>
            {
                var button = (Button)Named(window, "BtnMaximize");
                Assert.False(button.IsEnabled);

                shell.ShowManager();
                Settle(window);
                Assert.Equal(ResizeMode.CanResize, window.ResizeMode);
                Assert.False(button.IsEnabled);
                Assert.False(UIElementAutomationPeer.CreatePeerForElement(button).IsEnabled());

                shell.ShowSettings();
                Settle(window);
                Assert.False(button.IsEnabled);

                shell.ShowCreator();
                Settle(window);
                Assert.False(button.IsEnabled);

                // Minimize and close still work
                Assert.True(((Button)Named(window, "BtnMinimize")).IsEnabled);
                Assert.True(((Button)Named(window, "BtnClose")).IsEnabled);
            }, withSettings: true);
        }

        #endregion The caption buttons

        #region The window and its screens

        [Fact]
        public void TheWindow_HasNoTitleBarOfItsOwn_AndTheCreatorIsMainWidth()
        {
            WithWindow((window, shell) =>
            {
                var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(window);
                Assert.NotNull(chrome);
                Assert.Equal(48, chrome.CaptionHeight);
                Assert.Equal(0, chrome.GlassFrameThickness.Left);
                Assert.False(chrome.UseAeroCaptionButtons);

                // The taskbar and Alt+Tab still say keycuts, whichever screen is showing
                Assert.Equal("keycuts", window.Title);
                Assert.Equal(WindowLayout.CreatorWidth, window.ActualWidth, 0.5);

                var bar = (FrameworkElement)Named(window, "Bar");
                Assert.Equal(48, bar.ActualHeight);
                foreach (var tab in new[] { "CreatorTab", "ManagerTab", "SettingsTab" })
                {
                    Assert.Equal(40, ((FrameworkElement)Named(window, tab)).ActualHeight);
                }
            });
        }

        [Fact]
        public void TheCreatorWidth_IsMainWidthInTheTokens()
        {
            var tokens = XDocument.Load(TokensPath());
            var mainWidth = tokens.Descendants().Single(e => (string?)e.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) == "MainWidth");

            Assert.Equal(WindowLayout.CreatorWidth, double.Parse(mainWidth.Value.Trim(), System.Globalization.CultureInfo.InvariantCulture));
        }

        [Fact]
        public void TheCreatorAndManagerTabs_AreTextOnly()
        {
            WithWindow((window, shell) =>
            {
                foreach (var (tab, text) in new[] { ("CreatorTab", "creator"), ("ManagerTab", "manager") })
                {
                    var item = (TabItem)Named(window, tab);
                    Assert.Equal(text, item.Header);
                    Assert.Empty(Descendants<keycuts.GUI.Controls.Icon>(item));
                }
            });
        }

        [Fact]
        public void TheTabBar_FitsInTheCreator_WithRoomToGrab()
        {
            WithWindow((window, shell) =>
            {
                var bar = (FrameworkElement)Named(window, "Bar");
                var rail = (FrameworkElement)Named(window, "TabStrip");
                var captions = (FrameworkElement)Named(window, "BtnMinimize");

                var tabsEnd = rail.TranslatePoint(new Point(rail.ActualWidth, 0), bar).X;
                var captionsStart = captions.TranslatePoint(new Point(0, 0), bar).X;
                var empty = captionsStart - tabsEnd;

                // #218 asked for about 60 beyond the tabs; the text-only tabs left about 110 at the same 600, and the names at 16 with
                // 24 after keycuts (the owner, 2026-11-17) leave about 77
                Assert.InRange(empty, 65, 90);
            });
        }

        [Fact]
        public void TheScreens_HaveNoHeadingsOrSwitchButtons_AndTheFooterHasNoGear()
        {
            WithWindow((window, shell) =>
            {
                foreach (var gone in new[] { "BtnManager", "BtnCreator", "BtnSettings" })
                {
                    Assert.DoesNotContain(Descendants<Button>(window), b => b.Name == gone);
                }

                Assert.DoesNotContain(Descendants<TextBlock>(window), t => t.Text is "keycuts creator" or "keycuts manager");

                shell.ShowManager();
                Settle(window);
                Assert.DoesNotContain(Descendants<TextBlock>(window), t => t.Text is "keycuts creator" or "keycuts manager");
                Assert.DoesNotContain(Descendants<Button>(window), b => b.Name is "BtnManager" or "BtnCreator" or "BtnSettings");
            });
        }

        // The keycap alone at 36, in no socket (docs/mockups/icon-size-v2, R3), whether or not the other icons are transparent
        [Fact]
        public void TheAppIcon_InTheBar_IsTheOneFromSettings_WithoutItsBackground_At36()
        {
            WithWindow((window, shell) =>
            {
                var icon = Descendants<System.Windows.Controls.Image>(window).Single(i => i.Name == "AppIcon");
                Assert.Same(keycuts.GUI.Themes.AppIcons.GetPreview(keycuts.GUI.Themes.AppIcons.GetTransparent(keycuts.GUI.Themes.AppIconManager.Current)), icon.Source);
                Assert.Equal(36, icon.Width);
                Assert.Equal(36, icon.Height);
                Assert.IsType<StackPanel>(icon.Parent);
                Assert.Equal(8, ((FrameworkElement)icon.Parent).Margin.Left);
            });
        }

        #endregion The window and its screens

        #region The tab shape

        [Fact]
        public void FolderTabShape_HasASquareLeftSide_ACutCorner_AFlatTop_AndASlantedRightSide()
        {
            var points = FolderTabShape.GetPoints(100, 40, 8, 16, 0);

            Assert.Equal(
                new[] { new Point(0, 40), new Point(0, 8), new Point(8, 0), new Point(84, 0), new Point(100, 40) },
                points);
        }

        [Fact]
        public void FolderTabShape_DrawsItsStrokeInsideItsBounds()
        {
            var points = FolderTabShape.GetPoints(100, 40, 8, 16, 1);

            Assert.Equal(0.5, points[0].X);
            Assert.Equal(0.5, points[2].Y);
            Assert.Equal(99.5, points[4].X);
            Assert.Equal(40, points[0].Y);
            Assert.Equal(40, points[4].Y);
        }

        [Fact]
        public void FolderTabShape_SurvivesATabNarrowerThanItsSlant()
        {
            var points = FolderTabShape.GetPoints(10, 40, 8, 16, 1);

            Assert.All(points, p => Assert.True(p.X >= 0 && p.X <= 10 && p.Y >= 0 && p.Y <= 40, p.ToString()));
            Assert.True(points[3].X >= points[2].X, "the top never runs backwards");
        }

        #endregion The tab shape

        #region Helpers

        private static int HitTest(Window window, string elementName, double x, double y)
        {
            var element = (FrameworkElement)Named(window, elementName);
            var screen = element.PointToScreen(new Point(x, y));
            var lParam = (IntPtr)((((int)screen.Y & 0xFFFF) << 16) | ((int)screen.X & 0xFFFF));
            return (int)SendMessage(new WindowInteropHelper(window).Handle, WmNcHitTest, IntPtr.Zero, lParam);
        }

        // The names in the tab bar belong to its own view's name scope, so Window.FindName can't see them: walk the tree
        private static AutomationPeer TabPeer(Window window, string tabName)
        {
            var rail = (RailTabControl)Named(window, "TabStrip");
            var tab = Named(window, tabName);
            var listPeer = UIElementAutomationPeer.CreatePeerForElement(rail);
            return listPeer.GetChildren().Single(c => AutomationProperties.GetName((DependencyObject)tab) == c.GetName() && c.GetPattern(PatternInterface.SelectionItem) != null);
        }

        private static object Named(Window window, string name)
        {
            return Descendants<FrameworkElement>(window).FirstOrDefault(e => e.Name == name)
                ?? (name == "Frame" ? (object)window.Content : throw new InvalidOperationException("No element named " + name));
        }

        private static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
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

        private sealed class MemorySettingsStore : ISettingsStore
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

        private static void WithWindow(Action<MainWindow, ShellViewModel> body, bool withKeycuts = false, bool withSettings = false)
        {
            RunSta(() =>
            {
                EnsureApplication();

                // With keycuts to load, the Manager turns its search box on once the load ends, as it does in the app
                Func<CancellationToken, Task<System.Collections.Generic.IReadOnlyList<Keycut>>>? loader = withKeycuts
                    ? _ => Task.FromResult<System.Collections.Generic.IReadOnlyList<Keycut>>(new[]
                    {
                        new Keycut { Name = "gmail", Kind = KeycutKind.Url, RawType = ShortcutType.Url, Target = "https://mail.google.com", FilePath = @"C:\fixture\gmail.bat" }
                    })
                    : null;
                // A Settings screen over a store in memory: nothing reads or writes the registry
                Func<SettingsViewModel>? settings = withSettings ? () => new SettingsViewModel(new MemorySettingsStore()) : null;
                var shell = new ShellViewModel(() => Task.FromResult(3), loader, settingsFactory: settings);
                var window = new MainWindow(shell)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -20000,
                    Top = -20000,
                    ShowInTaskbar = false,
                    ShowActivated = false
                };

                window.Show();
                Settle(window);
                try
                {
                    body(window, shell);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        private static void Settle(Window window)
        {
            Pump();
            window.UpdateLayout();
            Pump();
        }

        private static void Pump()
        {
            // A frame, not a bare Invoke: posted window messages (a system command) are only taken by a message loop
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        private static void EnsureApplication()
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

        private static void RunSta(Action action)
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

        private static string TokensPath()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "keycuts.sln")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, "keycuts.GUI", "Themes", "Tokens.xaml");
        }

        #endregion Helpers
    }
}
