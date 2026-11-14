using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI
{
    /// <summary>
    /// The single application window. Hosts the creator, the manager or settings (#218) and resizes itself per screen.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ShellViewModel shell;

        private Point? creatorLocation;

        private Size? managerSize;

        private bool syncingTabs;

        private ShellMode shownMode;

        private WindowCaptionHook? captionHook;

        public MainWindow(ShellViewModel shell)
        {
            InitializeComponent();
            this.shell = shell;
            DataContext = shell;

            shownMode = shell.Mode;
            shell.PropertyChanged += Shell_PropertyChanged;
            Loaded += (sender, e) => _ = shell.ReloadAsync();

            // The tab bar replaces the title bar (#218). The tabs show the shell's screen and switch it.
            SelectTabForMode();
            Rail.Tabs.SelectionChanged += Tabs_SelectionChanged;
            // Windows 11's snap layouts need the window to answer "this is the maximize button" over ours
            SourceInitialized += (sender, e) => captionHook = new WindowCaptionHook(this, Rail.MaximizeButton);
            StateChanged += (sender, e) => FitMaximizedWindow();

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
            if (IsLoaded)
            {
                ApplyMode(previous, shell.Mode);
            }
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

        private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (syncingTabs || !ReferenceEquals(e.OriginalSource, Rail.Tabs))
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

        private void Main_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5 && shell.Mode == ShellMode.Manager)
            {
                e.Handled = true;
                _ = shell.ReloadAsync();
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

        /// <summary>
        /// The Manager is resizable and remembers its size; the Creator and Settings are MainWidth wide and fit their content's
        /// height, Settings capped to the work area so it scrolls on a short screen. The tabs never move: the window keeps its left.
        /// </summary>
        private void ApplyMode(ShellMode previous, ShellMode mode)
        {
            if (mode == ShellMode.Manager)
            {
                ShowManagerSize();
            }
            else
            {
                ShowFixedSize(leavingManager: previous == ShellMode.Manager, mode == ShellMode.Settings);
            }
        }

        /// <summary>
        /// With no frame of its own, a maximized window is placed by Windows as if it still had one: it overhangs the work area by the
        /// size of the resize border on every side (measured: -8,-8 to 1928,1160 against a 1920 x 1152 work area at 100%, which is what <see cref="WindowLayout.GetFrameThickness"/> returns). The content is
        /// pulled in by that much so it lies inside the work area, and the caption area grows by the same amount at the top so it still
        /// covers the whole bar.
        /// </summary>
        private void FitMaximizedWindow()
        {
            var overhang = WindowState == WindowState.Maximized ? WindowLayout.GetFrameThickness(this) : new Thickness(0);
            Frame.Margin = overhang;

            var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(this);
            if (chrome != null)
            {
                chrome.CaptionHeight = (double)FindResource("RailHeight") + overhang.Top;
            }
        }

        /// <summary>The edges that resize the window: the system's thickness on the Manager, none on the fixed-size screens.</summary>
        private void SetResizeBorder(Thickness thickness)
        {
            var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(this);
            if (chrome != null)
            {
                chrome.ResizeBorderThickness = thickness;
            }
        }

        private void ShowManagerSize()
        {
            creatorLocation = new Point(Left, Top);

            SizeToContent = SizeToContent.Manual;
            // Settings caps the height to its monitor's work area; the Manager can be as tall as any screen it is moved to
            MaxHeight = double.PositiveInfinity;
            ResizeMode = ResizeMode.CanResize;
            SetResizeBorder(WindowLayout.GetFrameThickness(this));
            MinWidth = WindowLayout.ManagerMinWidth;
            MinHeight = WindowLayout.ManagerMinHeight;

            var size = managerSize ?? new Size(WindowLayout.ManagerDefaultWidth, WindowLayout.ManagerDefaultHeight);
            var bounds = WindowLayout.ClampToWorkArea(new Rect(Left, Top, size.Width, size.Height), WindowLayout.GetWorkAreaInDips(this));

            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
        }

        private void ShowFixedSize(bool leavingManager, bool capToWorkArea)
        {
            if (leavingManager)
            {
                // Only the Manager's own size is remembered, not the Creator's or Settings'
                if (WindowState == WindowState.Normal)
                {
                    managerSize = new Size(ActualWidth, ActualHeight);
                }
                else
                {
                    managerSize = RestoreBounds.Size;
                    WindowState = WindowState.Normal;
                }
            }

            MinWidth = 0;
            MinHeight = 0;
            ResizeMode = ResizeMode.CanMinimize;
            SetResizeBorder(new Thickness(0));
            MaxHeight = capToWorkArea ? WindowLayout.GetWorkAreaInDips(this).Height : double.PositiveInfinity;
            Width = WindowLayout.CreatorWidth;
            SizeToContent = SizeToContent.Height;

            if (leavingManager && creatorLocation.HasValue)
            {
                Left = creatorLocation.Value.X;
                Top = creatorLocation.Value.Y;
            }

            if (capToWorkArea)
            {
                // Settings is taller than the Creator: once it has its height, slide it up if its bottom is off the screen
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(KeepOnScreen));
            }
        }

        private void KeepOnScreen()
        {
            var bounds = WindowLayout.ClampToWorkArea(new Rect(Left, Top, ActualWidth, ActualHeight), WindowLayout.GetWorkAreaInDips(this));
            Left = bounds.Left;
            Top = bounds.Top;
        }
    }
}
