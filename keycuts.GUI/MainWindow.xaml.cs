using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
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
            if (e.PropertyName == nameof(ShellViewModel.Mode) && IsLoaded)
            {
                ApplyMode(shell.Mode);
            }
        }

        private void Shell_SettingsRequested(object? sender, EventArgs e)
        {
            var settingsWindow = new SettingsWindow { Owner = this };
            // A save that reported a problem (for example PATH) may still have stored the folder, so a later Cancel refreshes too
            var saved = settingsWindow.ShowDialog() == true || settingsWindow.ViewModel.StoreChanged;

            if (saved)
            {
                // The environment-variables setting may have changed; the preview follows it
                shell.Creator.UseEnvironmentVariablesSetting = Common.RegistryStuff.GetUseEnvironmentVariables();
            }

            if (settingsWindow.ShowWelcomeRequested)
            {
                shell.ShowWelcome();
            }

            // The output folder may have changed
            if (saved || settingsWindow.ShowWelcomeRequested)
            {
                _ = shell.ReloadAsync();
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
            var bounds = WindowLayout.ClampToWorkArea(new Rect(Left, Top, size.Width, size.Height), WindowLayout.GetWorkAreaInDips(this));

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
    }
}
