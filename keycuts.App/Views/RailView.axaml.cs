using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using keycuts.GUI.Themes;

namespace keycuts.GUI.Views
{
    /// <summary>
    /// The tab bar that replaces the title bar (#218): the app icon and name, the tabs, the draggable bar and the caption buttons.
    /// The window decides what the tabs do (<see cref="Tabs"/>); this view keeps the icon in step with the app icon choice.
    /// Maximize is always disabled: the window is never maximized (<see cref="NoMaximizeHook"/>).
    /// </summary>
    public partial class RailView : UserControl
    {
        public RailView()
        {
            InitializeComponent();
            Tabs = this.FindControl<TabControl>("TabStrip")!;
            CreatorItem = this.FindControl<TabItem>("CreatorTab")!;
            ManagerItem = this.FindControl<TabItem>("ManagerTab")!;
            SettingsItem = this.FindControl<TabItem>("SettingsTab")!;
            UpdateAppIcon();
        }

        public TabControl Tabs { get; }

        public TabItem CreatorItem { get; }

        public TabItem ManagerItem { get; }

        public TabItem SettingsItem { get; }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            AppIconManager.Changed += AppIconManager_Changed;
            UpdateAppIcon();
        }

        protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            AppIconManager.Changed -= AppIconManager_Changed;
            base.OnDetachedFromVisualTree(e);
        }

        private void AppIconManager_Changed(object? sender, EventArgs e)
        {
            UpdateAppIcon();
        }

        /// <summary>The icon picked in Settings, always without its background (the bar is its background), its 256 px image drawn at 36.</summary>
        private void UpdateAppIcon()
        {
            this.FindControl<Image>("AppIcon")!.Source = AppIcons.GetPreview(AppIconManager.TabBar);
        }

        private void Minimize_Click(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is Window window)
            {
                window.WindowState = WindowState.Minimized;
            }
        }

        private void Close_Click(object? sender, RoutedEventArgs e)
        {
            (TopLevel.GetTopLevel(this) as Window)?.Close();
        }
    }
}
