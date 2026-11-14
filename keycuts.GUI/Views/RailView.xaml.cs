using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using keycuts.GUI.Themes;

namespace keycuts.GUI.Views
{
    /// <summary>
    /// The tab bar that replaces the title bar (#218): the app icon and name, the tabs, the draggable bar and the caption buttons.
    /// The window decides what the tabs do (<see cref="Tabs"/>); this view keeps the caption buttons and the icon in step with the window.
    /// </summary>
    public partial class RailView : UserControl
    {
        private static readonly Geometry MaximizeGeometry = Geometry.Parse("M0.5,0.5 H9.5 V9.5 H0.5 Z");

        private static readonly Geometry RestoreGeometry = Geometry.Parse("M2.5,2.5 V0.5 H9.5 V7.5 H7.5 M0.5,2.5 H7.5 V9.5 H0.5 Z");

        private Window? window;

        public RailView()
        {
            InitializeComponent();
            Loaded += RailView_Loaded;
            Unloaded += RailView_Unloaded;
        }

        public TabControl Tabs
        {
            get { return TabStrip; }
        }

        public TabItem CreatorItem
        {
            get { return CreatorTab; }
        }

        public TabItem ManagerItem
        {
            get { return ManagerTab; }
        }

        public TabItem SettingsItem
        {
            get { return SettingsTab; }
        }

        public Button MaximizeButton
        {
            get { return BtnMaximize; }
        }

        private void RailView_Loaded(object sender, RoutedEventArgs e)
        {
            window = Window.GetWindow(this);
            if (window == null)
            {
                return;
            }

            window.StateChanged += Window_StateChanged;
            DependencyPropertyDescriptor.FromProperty(Window.ResizeModeProperty, typeof(Window))?.AddValueChanged(window, Window_ResizeModeChanged);
            DependencyPropertyDescriptor.FromProperty(Window.IconProperty, typeof(Window))?.AddValueChanged(window, Window_IconChanged);

            UpdateCaptionButtons();
            UpdateAppIcon();
        }

        private void RailView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (window == null)
            {
                return;
            }

            window.StateChanged -= Window_StateChanged;
            DependencyPropertyDescriptor.FromProperty(Window.ResizeModeProperty, typeof(Window))?.RemoveValueChanged(window, Window_ResizeModeChanged);
            DependencyPropertyDescriptor.FromProperty(Window.IconProperty, typeof(Window))?.RemoveValueChanged(window, Window_IconChanged);
            window = null;
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            UpdateCaptionButtons();
        }

        private void Window_ResizeModeChanged(object? sender, EventArgs e)
        {
            UpdateCaptionButtons();
        }

        private void Window_IconChanged(object? sender, EventArgs e)
        {
            UpdateAppIcon();
        }

        /// <summary>Maximize is off on the fixed-size screens, as <c>ResizeMode.CanMinimize</c> makes it on a standard window; its glyph and name follow the window's state.</summary>
        private void UpdateCaptionButtons()
        {
            if (window == null)
            {
                return;
            }

            var maximized = window.WindowState == WindowState.Maximized;
            BtnMaximize.IsEnabled = window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;
            MaximizeGlyph.Data = maximized ? RestoreGeometry : MaximizeGeometry;

            var name = maximized ? "Restore" : "Maximize";
            BtnMaximize.ToolTip = name;
            System.Windows.Automation.AutomationProperties.SetName(BtnMaximize, name);
        }

        /// <summary>The icon picked in Settings, at the window's icon: the 256 px frame drawn at 20, so it is sharp at any DPI.</summary>
        private void UpdateAppIcon()
        {
            AppIcon.Source = AppIcons.GetPreview(AppIconManager.Current);
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            if (window != null)
            {
                SystemCommands.MinimizeWindow(window);
            }
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (window == null)
            {
                return;
            }

            if (window.WindowState == WindowState.Maximized)
            {
                SystemCommands.RestoreWindow(window);
            }
            else
            {
                SystemCommands.MaximizeWindow(window);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (window != null)
            {
                SystemCommands.CloseWindow(window);
            }
        }
    }
}
