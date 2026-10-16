using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI
{
    /// <summary>
    /// The single application window. Hosts the creator or the manager and resizes itself per mode.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ShellViewModel shell;

        private Point? creatorLocation;

        private Size? managerSize;

        public MainWindow(ShellViewModel shell)
        {
            InitializeComponent();
            this.shell = shell;
            DataContext = shell;

            shell.PropertyChanged += Shell_PropertyChanged;
            shell.SettingsRequested += Shell_SettingsRequested;
            Loaded += (sender, e) => _ = shell.ReloadAsync();
        }

        public ShellViewModel Shell
        {
            get { return shell; }
        }

        private void Shell_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ShellViewModel.Mode) && IsLoaded)
            {
                ApplyMode(shell.Mode);
            }
        }

        private void Shell_SettingsRequested(object? sender, EventArgs e)
        {
            var settingsWindow = new SettingsWindow { Owner = this };
            settingsWindow.ShowDialog();

            // The output folder may have changed
            _ = shell.ReloadAsync();
        }

        private void Main_KeyDown(object sender, KeyEventArgs e)
        {
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

        private void ApplyMode(ShellMode mode)
        {
            if (mode == ShellMode.Manager)
            {
                ShowManagerSize();
            }
            else
            {
                ShowCreatorSize();
            }
        }

        private void ShowManagerSize()
        {
            creatorLocation = new Point(Left, Top);

            SizeToContent = SizeToContent.Manual;
            ResizeMode = ResizeMode.CanResize;
            MinWidth = WindowLayout.ManagerMinWidth;
            MinHeight = WindowLayout.ManagerMinHeight;

            var size = managerSize ?? new Size(WindowLayout.ManagerDefaultWidth, WindowLayout.ManagerDefaultHeight);
            var bounds = WindowLayout.ClampToWorkArea(new Rect(Left, Top, size.Width, size.Height), GetWorkAreaInDips());

            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
        }

        private void ShowCreatorSize()
        {
            if (WindowState == WindowState.Normal)
            {
                managerSize = new Size(ActualWidth, ActualHeight);
            }
            else
            {
                managerSize = RestoreBounds.Size;
                WindowState = WindowState.Normal;
            }

            MinWidth = 0;
            MinHeight = 0;
            ResizeMode = ResizeMode.CanMinimize;
            Width = WindowLayout.CreatorWidth;
            SizeToContent = SizeToContent.Height;

            if (creatorLocation.HasValue)
            {
                Left = creatorLocation.Value.X;
                Top = creatorLocation.Value.Y;
            }
        }

        private Rect GetWorkAreaInDips()
        {
            var handle = new WindowInteropHelper(this).Handle;
            var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };

            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
            {
                return SystemParameters.WorkArea;
            }

            var dpi = VisualTreeHelper.GetDpi(this);
            return new Rect(
                info.Work.Left / dpi.DpiScaleX,
                info.Work.Top / dpi.DpiScaleY,
                (info.Work.Right - info.Work.Left) / dpi.DpiScaleX,
                (info.Work.Bottom - info.Work.Top) / dpi.DpiScaleY);
        }

        #region External Methods

        private const int MonitorDefaultToNearest = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect Work;
            public int Flags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        #endregion External Methods
    }
}
