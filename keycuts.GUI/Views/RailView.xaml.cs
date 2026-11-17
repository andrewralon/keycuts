using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using keycuts.GUI.Themes;

namespace keycuts.GUI.Views
{
    /// <summary>
    /// The tab bar that replaces the title bar (#218): the app icon and name, the tabs, the draggable bar and the caption buttons.
    /// The window decides what the tabs do (<see cref="Tabs"/>); this view keeps the icon in step with the window. Maximize is always
    /// disabled: the window is never maximized (<see cref="NoMaximizeHook"/>).
    /// </summary>
    public partial class RailView : UserControl
    {
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

        private void RailView_Loaded(object sender, RoutedEventArgs e)
        {
            window = Window.GetWindow(this);
            if (window == null)
            {
                return;
            }

            DependencyPropertyDescriptor.FromProperty(Window.IconProperty, typeof(Window))?.AddValueChanged(window, Window_IconChanged);

            UpdateAppIcon();
        }

        private void RailView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (window == null)
            {
                return;
            }

            DependencyPropertyDescriptor.FromProperty(Window.IconProperty, typeof(Window))?.RemoveValueChanged(window, Window_IconChanged);
            window = null;
        }

        private void Window_IconChanged(object? sender, EventArgs e)
        {
            UpdateAppIcon();
        }

        /// <summary>
        /// The icon picked in Settings, always without its background (the bar is its background): the 256 px frame drawn at 36,
        /// so it is sharp at any DPI.
        /// </summary>
        private void UpdateAppIcon()
        {
            AppIcon.Source = AppIcons.GetPreview(AppIconManager.TabBar);
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            if (window != null)
            {
                SystemCommands.MinimizeWindow(window);
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
