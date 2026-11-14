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
using keycuts.GUI;
using keycuts.GUI.Controls;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// #218: the tab bar that replaces the title bar. The window has to do everything its frame did, so these ask the real window the
    /// question Windows asks (<c>WM_NCHITTEST</c>) and look at what it answers: caption over the icon, name and empty bar, client over
    /// the tabs and caption buttons, the maximize button over ours on the Manager only (for Windows 11's snap layouts), and a resize
    /// edge only where the window can be resized.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class TabbedWindowTests
    {
        private const int WmNcHitTest = 0x0084;
        private const int WmNcMouseMove = 0x00A0;
        private const int WmNcLButtonDown = 0x00A1;
        private const int WmNcLButtonUp = 0x00A2;
        private const int WmNcMouseLeave = 0x02A2;
        private const int HtLeft = 10;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        #region Hit testing

        [Fact]
        public void TheBar_IsTheCaption_ExceptTheTabsAndTheCaptionButtons()
        {
            WithWindow((window, shell) =>
            {
                Assert.Equal(WindowCaptionHook.HtCaption, HitTest(window, "Bar", 30, 24));   // the icon and name
                Assert.Equal(WindowCaptionHook.HtCaption, HitTest(window, "Bar", 400, 24));  // the empty bar
                Assert.Equal(WindowCaptionHook.HtClient, HitTest(window, "CreatorTab", 20, 20));
                Assert.Equal(WindowCaptionHook.HtClient, HitTest(window, "ManagerTab", 20, 20));
                Assert.Equal(WindowCaptionHook.HtClient, HitTest(window, "SettingsTab", 20, 20));
                Assert.Equal(WindowCaptionHook.HtClient, HitTest(window, "BtnMinimize", 23, 24));
                Assert.Equal(WindowCaptionHook.HtClient, HitTest(window, "BtnClose", 23, 24));
                Assert.Equal(WindowCaptionHook.HtClient, HitTest(window, "Frame", 100, 200));  // the page
            });
        }

        [Fact]
        public void TheMaximizeButton_AnswersHtMaxButton_OnTheManagerOnly()
        {
            WithWindow((window, shell) =>
            {
                // The Creator is fixed-size: maximize is disabled, so the bar is a plain caption there and a click is a click on a dead button
                Assert.NotEqual(WindowCaptionHook.HtMaxButton, HitTest(window, "BtnMaximize", 23, 24));

                shell.ShowManager();
                Settle(window);
                Assert.Equal(WindowCaptionHook.HtMaxButton, HitTest(window, "BtnMaximize", 23, 24));
                Assert.Equal(WindowCaptionHook.HtMaxButton, HitTest(window, "BtnMaximize", 1, 1));
                Assert.Equal(WindowCaptionHook.HtMaxButton, HitTest(window, "BtnMaximize", 44, 46));

                shell.ShowCreator();
                Settle(window);
                Assert.NotEqual(WindowCaptionHook.HtMaxButton, HitTest(window, "BtnMaximize", 23, 24));
            });
        }

        [Fact]
        public void ResizeEdges_ExistOnTheManagerOnly()
        {
            WithWindow((window, shell) =>
            {
                // The Creator is ResizeMode.CanMinimize: WindowChrome would still answer a resize edge (cursor and all) at its border
                Assert.Equal(WindowCaptionHook.HtClient, HitTest(window, "Frame", 1, 200));

                shell.ShowManager();
                Settle(window);
                Assert.Equal(HtLeft, HitTest(window, "Frame", 1, 200));

                shell.ShowCreator();
                Settle(window);
                Assert.Equal(WindowCaptionHook.HtClient, HitTest(window, "Frame", 1, 200));
            });
        }

        [Fact]
        public void WhenMaximized_TheWindowLiesInsideTheWorkArea_AndTheWholeBarStaysTheCaption()
        {
            WithWindow((window, shell) =>
            {
                shell.ShowManager();
                Settle(window);
                window.WindowState = WindowState.Maximized;
                Settle(window);

                try
                {
                    // Windows places a maximized window as if its frame were still there, so it overhangs the work area unless the content is pulled in
                    var frame = (FrameworkElement)Named(window, "Frame");
                    var topLeft = frame.PointToScreen(new Point(0, 0));
                    var bottomRight = frame.PointToScreen(new Point(frame.ActualWidth, frame.ActualHeight));
                    var dpi = VisualTreeHelper.GetDpi(window);
                    var work = WindowLayout.GetWorkAreaInDips(window);

                    Assert.Equal(work.Left * dpi.DpiScaleX, topLeft.X, 1.0);
                    Assert.Equal(work.Top * dpi.DpiScaleY, topLeft.Y, 1.0);
                    Assert.Equal(work.Right * dpi.DpiScaleX, bottomRight.X, 1.0);
                    Assert.Equal(work.Bottom * dpi.DpiScaleY, bottomRight.Y, 1.0);

                    // The bottom of the empty bar is still the caption, not the first row of the page
                    var bar = (FrameworkElement)Named(window, "Bar");
                    Assert.Equal(WindowCaptionHook.HtCaption, HitTest(window, "Bar", 400, bar.ActualHeight - 1));

                    Assert.Equal("Restore", AutomationProperties.GetName((Button)Named(window, "BtnMaximize")));
                }
                finally
                {
                    window.WindowState = WindowState.Normal;
                    Settle(window);
                }

                Assert.Equal("Maximize", AutomationProperties.GetName((Button)Named(window, "BtnMaximize")));
                Assert.Equal(new Thickness(0), ((FrameworkElement)Named(window, "Frame")).Margin);
            });
        }

        #endregion Hit testing

        #region The maximize button, as Windows sees it

        [Fact]
        public void OverTheMaximizeButton_TheNonClientMouseMessages_DriveItsLook_AndAClickMaximizes()
        {
            WithWindow((window, shell) =>
            {
                shell.ShowManager();
                Settle(window);
                var hwnd = new WindowInteropHelper(window).Handle;
                var button = (Button)Named(window, "BtnMaximize");
                var over = (IntPtr)WindowCaptionHook.HtMaxButton;

                SendMessage(hwnd, WmNcMouseMove, over, IntPtr.Zero);
                Assert.True(CaptionButtonState.GetIsHot(button));
                Assert.False(CaptionButtonState.GetIsPressed(button));

                SendMessage(hwnd, WmNcLButtonDown, over, IntPtr.Zero);
                Assert.True(CaptionButtonState.GetIsPressed(button));

                // Moving off it, or leaving the window, lets go
                SendMessage(hwnd, WmNcMouseMove, (IntPtr)WindowCaptionHook.HtCaption, IntPtr.Zero);
                Assert.False(CaptionButtonState.GetIsHot(button));
                Assert.False(CaptionButtonState.GetIsPressed(button));

                SendMessage(hwnd, WmNcMouseMove, over, IntPtr.Zero);
                SendMessage(hwnd, WmNcLButtonDown, over, IntPtr.Zero);
                SendMessage(hwnd, WmNcMouseLeave, IntPtr.Zero, IntPtr.Zero);
                Assert.False(CaptionButtonState.GetIsHot(button));
                Assert.False(CaptionButtonState.GetIsPressed(button));

                // A release with no press on the button (the press began elsewhere) does nothing
                SendMessage(hwnd, WmNcLButtonUp, over, IntPtr.Zero);
                Settle(window);
                Assert.Equal(WindowState.Normal, window.WindowState);

                SendMessage(hwnd, WmNcLButtonDown, over, IntPtr.Zero);
                SendMessage(hwnd, WmNcLButtonUp, over, IntPtr.Zero);
                Settle(window);

                try
                {
                    Assert.Equal(WindowState.Maximized, window.WindowState);
                    Assert.False(CaptionButtonState.GetIsPressed(button));
                }
                finally
                {
                    window.WindowState = WindowState.Normal;
                    Settle(window);
                }
            });
        }

        [Theory]
        [InlineData(0, 0, 46, 48, true)]
        [InlineData(45.9, 47.9, 46, 48, true)]
        [InlineData(46, 10, 46, 48, false)]
        [InlineData(10, 48, 46, 48, false)]
        [InlineData(-0.1, 10, 46, 48, false)]
        [InlineData(10, -0.1, 46, 48, false)]
        public void IsOver_IsHalfOpen(double x, double y, double width, double height, bool expected)
        {
            Assert.Equal(expected, WindowCaptionHook.IsOver(new Point(x, y), width, height));
        }

        [Theory]
        [InlineData(100, 200)]
        [InlineData(-20000, -20000)]
        [InlineData(-1, 5)]
        [InlineData(2559, 1439)]
        public void ScreenPoint_ReadsTwoSigned16BitHalves(int x, int y)
        {
            // A second monitor to the left or above makes the coordinates negative, which a plain mask and shift would turn into 65535 - |n|
            var lParam = (IntPtr)(((y & 0xFFFF) << 16) | (x & 0xFFFF));

            Assert.Equal(new Point(x, y), WindowCaptionHook.ScreenPoint(lParam));
        }

        #endregion The maximize button, as Windows sees it

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
                    Assert.Equal(name, tab.ToolTip);
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
        public void TheSettingsTab_OpensSettings_AndTheScreensTabStaysSelected()
        {
            WithWindow((window, shell) =>
            {
                var rail = (RailTabControl)Named(window, "TabStrip");
                var creator = (TabItem)Named(window, "CreatorTab");
                var settings = (TabItem)Named(window, "SettingsTab");
                var opened = 0;

                // Settings is still a dialog until its screen lands (#218, second part). The window's own handler would open it, with
                // the real registry behind it, so it is taken off and the request is only counted
                var handler = typeof(MainWindow).GetMethod("Shell_SettingsRequested", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.NotNull(handler);
                shell.SettingsRequested -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), window, handler!);
                shell.SettingsRequested += (sender, e) => opened++;

                rail.SelectedItem = settings;

                Assert.Equal(1, opened);
                Assert.Same(creator, rail.SelectedItem);
                Assert.Equal(ShellMode.Creator, shell.Mode);
            });
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
        public void Maximize_IsDisabledOnTheFixedSizeScreens_AsResizeModeCanMinimizeMakesItOnAStandardWindow()
        {
            WithWindow((window, shell) =>
            {
                var button = (Button)Named(window, "BtnMaximize");
                Assert.Equal(ResizeMode.CanMinimize, window.ResizeMode);
                Assert.False(button.IsEnabled);

                shell.ShowManager();
                Settle(window);
                Assert.Equal(ResizeMode.CanResize, window.ResizeMode);
                Assert.True(button.IsEnabled);

                shell.ShowCreator();
                Settle(window);
                Assert.False(button.IsEnabled);
            });
        }

        [Fact]
        public void TheCaptionButtons_Work_ThroughUiAutomation()
        {
            WithWindow((window, shell) =>
            {
                shell.ShowManager();
                Settle(window);
                var maximize = (Button)Named(window, "BtnMaximize");

                ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(maximize).GetPattern(PatternInterface.Invoke)).Invoke();
                Settle(window);

                try
                {
                    Assert.Equal(WindowState.Maximized, window.WindowState);
                }
                finally
                {
                    window.WindowState = WindowState.Normal;
                    Settle(window);
                }
            });
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
        public void TheTabBar_FitsInTheCreator_WithAbout60OfEmptyBar()
        {
            WithWindow((window, shell) =>
            {
                var bar = (FrameworkElement)Named(window, "Bar");
                var rail = (FrameworkElement)Named(window, "TabStrip");
                var captions = (FrameworkElement)Named(window, "BtnMinimize");

                var tabsEnd = rail.TranslatePoint(new Point(rail.ActualWidth, 0), bar).X;
                var captionsStart = captions.TranslatePoint(new Point(0, 0), bar).X;
                var empty = captionsStart - tabsEnd;

                // "About 60 beyond the icon and name" (#218): enough to grab, not so much that it looks lost
                Assert.InRange(empty, 56, 80);
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

        [Fact]
        public void TheAppIcon_InTheBar_IsTheOneFromSettings_AtTwenty()
        {
            WithWindow((window, shell) =>
            {
                var icon = Descendants<System.Windows.Controls.Image>(window).Single(i => i.Name == "AppIcon");
                Assert.NotNull(icon.Source);
                Assert.Equal(20, icon.Width);
                Assert.Equal(20, icon.Height);
                Assert.Equal(28, ((FrameworkElement)icon.Parent).Width);
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

        private static void WithWindow(Action<MainWindow, ShellViewModel> body)
        {
            RunSta(() =>
            {
                EnsureApplication();
                var shell = new ShellViewModel(() => Task.FromResult(3));
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
