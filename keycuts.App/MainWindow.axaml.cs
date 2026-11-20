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
            KeyDown += Main_KeyDown;

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

            // Ctrl+1, Ctrl+2 and Ctrl+3 pick the tabs left to right, as in a browser; nothing while the tabs are off (the icon picker)
            if (e.KeyModifiers == KeyModifiers.Control && TabNumber(e.Key) is int tab)
            {
                e.Handled = true;
                if (shell.AreTabsEnabled)
                {
                    ShowTab(tab);
                }

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
            var visibleBefore = VisibleFrame.TopLeft(this);

            if (!IsLarge(mode))
            {
                ShowSmallSize(leavingLarge: wasLarge);
            }
            else if (!wasLarge)
            {
                ShowLargeSize();
            }

            // Windows gives a resizable window invisible resize borders (7 px a side at 100 %), outside what it draws, and keeps
            // the window's position: the window as seen would move 7 right going to the Manager and back on the Creator. Once the
            // new size has taken, it's moved back to where it was seen.
            if (visibleBefore is { } before && wasLarge != IsLarge(mode))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (VisibleFrame.TopLeft(this) is { } after && after != before)
                    {
                        Position = new PixelPoint(Position.X + before.X - after.X, Position.Y + before.Y - after.Y);
                    }
                }, Avalonia.Threading.DispatcherPriority.Background);
            }
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
            SizeToContent = SizeToContent.Manual;
            CanResize = true;
            Tear.IsResizeGrip = true;
            MinWidth = WindowLayout.ManagerMinWidth;
            MinHeight = WindowLayout.ManagerMinHeight;

            var size = largeSize ?? new Size(WindowLayout.ManagerDefaultWidth, WindowLayout.ManagerDefaultHeight);
            var scale = DesktopScaling;
            var left = Position.X / scale;
            var top = Position.Y / scale;
            var bounds = new Rect(left, top, size.Width, size.Height);
            if (GetWorkArea() is Rect workArea)
            {
                bounds = WindowLayout.ClampToWorkArea(bounds, workArea);
            }

            Position = new PixelPoint((int)Math.Round(bounds.Left * scale), (int)Math.Round(bounds.Top * scale));
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
