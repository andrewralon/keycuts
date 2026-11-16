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

        // The size the Manager and Settings share, once the window has left it for the Creator
        private Size? largeSize;

        private bool syncingTabs;

        private ShellMode shownMode;

        // Held here: an HwndSource keeps only weak references to its hooks
        private readonly NoMaximizeHook noMaximize;

        private readonly NoSystemCaptionHook noSystemCaption;

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
            // The window is never maximized, on any screen; the Manager and Settings are still resized from their edges
            noMaximize = new NoMaximizeHook(this);
            // Windows would paint its classic caption buttons over the tab bar, after a drag from the bar
            noSystemCaption = new NoSystemCaptionHook(this);

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
        /// Two sizes (docs/styleguide.md, Window widths): the Creator is small, MainWidth wide and as tall as its content; the
        /// Manager and Settings share the large one, which is resizable and remembered. Settings never grows the window to fit
        /// itself: it lays out in what the Manager had. The tabs never move: the window keeps its left.
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

            // Between the Manager and Settings the window stays as it is
        }

        /// <summary>The Manager and Settings share the large size; the Creator has the small one.</summary>
        public static bool IsLarge(ShellMode mode)
        {
            return mode != ShellMode.Creator;
        }

        /// <summary>The edges that resize the window: the system's thickness at the large size, none on the Creator.</summary>
        private void SetResizeBorder(Thickness thickness)
        {
            var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(this);
            if (chrome != null)
            {
                chrome.ResizeBorderThickness = thickness;
            }
        }

        // From the Creator to the Manager or Settings: the large size, as it was last left (by either), kept on the screen
        private void ShowLargeSize()
        {
            creatorLocation = new Point(Left, Top);

            SizeToContent = SizeToContent.Manual;
            ResizeMode = ResizeMode.CanResize;
            SetResizeBorder(WindowLayout.GetFrameThickness(this));
            MinWidth = WindowLayout.ManagerMinWidth;
            MinHeight = WindowLayout.ManagerMinHeight;

            var size = largeSize ?? new Size(WindowLayout.ManagerDefaultWidth, WindowLayout.ManagerDefaultHeight);
            var bounds = WindowLayout.ClampToWorkArea(new Rect(Left, Top, size.Width, size.Height), WindowLayout.GetWorkAreaInDips(this));

            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
        }

        // Back to the Creator: MainWidth, its content's height, no resizing, where the Creator was
        private void ShowSmallSize(bool leavingLarge)
        {
            if (leavingLarge)
            {
                // Only the large size is remembered: the Creator's follows its content
                if (WindowState == WindowState.Normal)
                {
                    largeSize = new Size(ActualWidth, ActualHeight);
                }
                else
                {
                    largeSize = RestoreBounds.Size;
                    WindowState = WindowState.Normal;
                }
            }

            MinWidth = 0;
            MinHeight = 0;
            ResizeMode = ResizeMode.CanMinimize;
            SetResizeBorder(new Thickness(0));
            Width = WindowLayout.CreatorWidth;
            SizeToContent = SizeToContent.Height;

            if (leavingLarge && creatorLocation.HasValue)
            {
                Left = creatorLocation.Value.X;
                Top = creatorLocation.Value.Y;
            }
        }
    }
}
