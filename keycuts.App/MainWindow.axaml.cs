using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using keycuts.GUI.Controls;
using keycuts.GUI.Themes;
using keycuts.GUI.ViewModels;
using keycuts.GUI.Views;

namespace keycuts.GUI
{
    /// <summary>
    /// The single application window (WPF's MainWindow). Hosts the creator, the manager or settings (#218) and resizes itself per screen.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ShellViewModel shell;

        // The size the Manager and Settings share, once the window has left it for the Creator
        private Size? largeSize;

        private bool syncingTabs;

        private ShellMode shownMode;

        /// <summary>For the XAML previewer only.</summary>
        public MainWindow()
            : this(new ShellViewModel(() => System.Threading.Tasks.Task.FromResult(0)))
        {
        }

        public MainWindow(ShellViewModel shell)
        {
            InitializeComponent();
            this.shell = shell;
            DataContext = shell;

            shownMode = shell.Mode;
            shell.PropertyChanged += Shell_PropertyChanged;
            Opened += (sender, e) => _ = shell.ReloadAsync();

            // The tab bar replaces the title bar (#218). The tabs show the shell's screen and switch it.
            SelectTabForMode();
            Rail.Tabs.SelectionChanged += Tabs_SelectionChanged;
            NoMaximizeHook.Attach(this);
            // Turning resizable (the Manager, Settings) or back (the Creator) never moves the window as seen
            FrameAnchor.Attach(this);
            KeyDown += Main_KeyDown;

            if (OperatingSystem.IsMacOS())
            {
                UseMacFrame();
            }

            // The torn corner follows the page's size and the theme (high contrast keeps the rectangle)
            Frame.SizeChanged += (sender, e) => UpdateTear();
            ThemeManager.Applied += ThemeManager_Applied;
            Closed += (sender, e) => ThemeManager.Applied -= ThemeManager_Applied;
            UpdateTear();

            // Pick up keycuts changed outside the app
            Activated += (sender, e) =>
            {
                if (shell.Mode == ShellMode.Manager)
                {
                    _ = shell.ReloadAsync();
                }
            };
        }

        public ShellViewModel Shell
        {
            get { return shell; }
        }

        private void Shell_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ShellViewModel.Mode))
            {
                return;
            }

            SelectTabForMode();
            var previous = shownMode;
            shownMode = shell.Mode;
            ApplyMode(previous, shell.Mode);
        }

        private void SelectTabForMode()
        {
            syncingTabs = true;
            try
            {
                Rail.Tabs.SelectedItem = shell.Mode switch
                {
                    ShellMode.Manager => Rail.ManagerItem,
                    ShellMode.Settings => Rail.SettingsItem,
                    _ => Rail.CreatorItem
                };
            }
            finally
            {
                syncingTabs = false;
            }
        }

        private void Tabs_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (syncingTabs || !ReferenceEquals(e.Source, Rail.Tabs))
            {
                return;
            }

            var selected = Rail.Tabs.SelectedItem;
            if (ReferenceEquals(selected, Rail.SettingsItem))
            {
                shell.ShowSettings();
                // Without a way to make its screen (a test's shell), Settings can't open, and the screen's own tab stays selected
                SelectTabForMode();
            }
            else if (ReferenceEquals(selected, Rail.ManagerItem))
            {
                shell.ShowManager();
            }
            else if (ReferenceEquals(selected, Rail.CreatorItem))
            {
                shell.ShowCreator();
            }
        }

        private void Main_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5 && shell.Mode == ShellMode.Manager)
            {
                e.Handled = true;
                _ = shell.ReloadAsync();
                return;
            }

            // Ctrl+1, Ctrl+2 and Ctrl+3 (⌘ on macOS) pick the tabs left to right, as in a browser; nothing while the tabs are off
            // (the icon picker). On macOS ⌘, is Settings too, as in every Mac app.
            var settingsKey = PlatformKeys.IsMac && e.Key == Key.OemComma;
            if (e.KeyModifiers == PlatformKeys.Command && (settingsKey ? 3 : TabNumber(e.Key)) is int tab)
            {
                e.Handled = true;
                if (shell.AreTabsEnabled)
                {
                    ShowTab(tab);
                }

                return;
            }

            // ⌘W closes the window on macOS; keycuts stays in the menu bar (Phase 6.1)
            if (PlatformKeys.IsMac && e.KeyModifiers == KeyModifiers.Meta && e.Key == Key.W)
            {
                e.Handled = true;
                Close();
                return;
            }

            if (e.Key != Key.Escape)
            {
                return;
            }

            e.Handled = true;
            if (shell.HandleEscape() == EscapeResult.CloseWindow)
            {
                Close();
            }
        }

        private static int? TabNumber(Key key)
        {
            return key switch
            {
                Key.D1 or Key.NumPad1 => 1,
                Key.D2 or Key.NumPad2 => 2,
                Key.D3 or Key.NumPad3 => 3,
                _ => null
            };
        }

        /// <summary>Picks a tab by number (1 creator, 2 manager, 3 settings), as Ctrl+1 to 3 do: nothing while the tabs are off.</summary>
        public void PickTab(int tab)
        {
            if (shell.AreTabsEnabled)
            {
                ShowTab(tab);
            }
        }

        private void ShowTab(int tab)
        {
            switch (tab)
            {
                case 1:
                    shell.ShowCreator();
                    break;
                case 2:
                    shell.ShowManager();
                    break;
                default:
                    shell.ShowSettings();
                    SelectTabForMode();
                    break;
            }
        }

        /// <summary>
        /// Two sizes (docs/styleguide.md, Window widths): the Creator is small, MainWidth wide and as tall as its content; the
        /// Manager and Settings share the large one, which is resizable and remembered. The tabs never move: the window keeps its left.
        /// </summary>
        private void ApplyMode(ShellMode previous, ShellMode mode)
        {
            var wasLarge = IsLarge(previous);

            if (!IsLarge(mode))
            {
                ShowSmallSize(leavingLarge: wasLarge);
            }
            else if (!wasLarge)
            {
                ShowLargeSize();
            }
        }

        /// <summary>
        /// Phase 5.1: macOS draws the frame, its rounded top corners and shadow, and its own traffic lights, which sit in the tab bar,
        /// centered in its height. Zoom stays off (<c>CanMaximize</c>). The page has no border of its own. As on Windows the bottom-right
        /// corner is torn, always (issue #292, #308): the window is transparent behind the page, so the desktop shows through the tear.
        /// </summary>
        private void UseMacFrame()
        {
            WindowDecorations = WindowDecorations.Full;
            ExtendClientAreaTitleBarHeightHint = (double)this.FindResource("RailHeight")!;
            MacTrafficLights.Attach(this, (double)this.FindResource("RailHeight")!);
            PageBorder.IsVisible = false;
            MakeTransparent();

            // Issue #308: a hint set before the native window exists is not honored (the corner came up black); the same hint set on
            // the open window is. So set it again once the window is open: off first, so the change is seen.
            Opened += (sender, e) =>
            {
                TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
                Avalonia.Threading.Dispatcher.UIThread.Post(MakeTransparent);
            };
        }

        private void MakeTransparent()
        {
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Background = Avalonia.Media.Brushes.Transparent;
        }

        /// <summary>Whether the window shows its torn corner: always, except in high contrast, which keeps the plain rectangle.</summary>
        public static bool ShowsTear(Palette palette)
        {
            return palette != Palette.HighContrast;
        }

        private void ThemeManager_Applied(object? sender, EventArgs e)
        {
            UpdateTear();
        }

        // The page is clipped to the torn shape, so the desktop shows through the tear; the tear's own drawing sits on top
        private void UpdateTear()
        {
            var torn = ShowsTear(ThemeManager.Current);
            Frame.Clip = torn ? TornCornerShape.Default.GetWindowClip(Frame.Bounds.Size) : null;
            Tear.IsVisible = torn;
        }

        /// <summary>The Manager and Settings share the large size; the Creator has the small one.</summary>
        public static bool IsLarge(ShellMode mode)
        {
            return mode != ShellMode.Creator;
        }

        /// <summary>The work area of the screen the window is on, in DIPs; null where there is no screen (headless).</summary>
        public Rect? GetWorkArea()
        {
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen == null)
            {
                return null;
            }

            var area = screen.WorkingArea;
            var scale = screen.Scaling;
            return new Rect(area.X / scale, area.Y / scale, area.Width / scale, area.Height / scale);
        }

        // From the Creator to the Manager or Settings: the large size, as it was last left (by either), kept on the screen
        private void ShowLargeSize()
        {
            var size = largeSize ?? new Size(WindowLayout.ManagerDefaultWidth, WindowLayout.ManagerDefaultHeight);
            var scale = DesktopScaling;
            var left = Position.X / scale;
            var top = Position.Y / scale;
            var bounds = new Rect(left, top, size.Width, size.Height);
            if (GetWorkArea() is Rect workArea)
            {
                bounds = WindowLayout.ClampToWorkArea(bounds, workArea);
            }

            // Moved before the window turns resizable, and only when the screen asks for it: after that, FrameAnchor puts the first
            // move or resize where the window is seen, and a move of our own in between would show as a shake (7 px, for a frame)
            var position = new PixelPoint((int)Math.Round(bounds.Left * scale), (int)Math.Round(bounds.Top * scale));
            if (position != Position)
            {
                Position = position;
            }

            SizeToContent = SizeToContent.Manual;
            CanResize = true;
            Tear.IsResizeGrip = true;
            MinWidth = WindowLayout.ManagerMinWidth;
            MinHeight = WindowLayout.ManagerMinHeight;
            Width = bounds.Width;
            Height = bounds.Height;
        }

        // Back to the Creator: MainWidth, its content's height, no resizing, at the top-left the large window was left at
        private void ShowSmallSize(bool leavingLarge)
        {
            // Only the large size is remembered: the Creator's follows its content. A minimized window keeps the last one.
            if (leavingLarge && WindowState == WindowState.Normal)
            {
                largeSize = new Size(Bounds.Width, Bounds.Height);
            }

            MinWidth = 0;
            MinHeight = 0;
            CanResize = false;
            Tear.IsResizeGrip = false;
            Width = WindowLayout.CreatorWidth;
            SizeToContent = SizeToContent.Height;
        }
    }
}
