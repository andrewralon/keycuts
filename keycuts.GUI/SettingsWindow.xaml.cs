using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.ComponentModel;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;
using keycuts.Common;
using keycuts.GUI.Themes;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI
{
    /// <summary>
    /// The modal Settings dialog. Save validates and closes with a true result; Cancel and Escape close without saving.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel viewModel;

        private bool initializing;

        // Set by a theme click, so closing only restores the saved theme when something was previewed
        private bool previewedTheme;

        public SettingsWindow()
            : this(null)
        {
        }

        /// <param name="viewModel">Tests pass one over a fake store; the app uses the registry-backed one.</param>
        public SettingsWindow(SettingsViewModel? viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel ?? new SettingsViewModel(new RunnerSettingsStore(), PickFolder, OpenFolder);
            DataContext = this.viewModel;

            // Checking the saved choice raises Checked; that is not a preview, so the palette is left alone
            initializing = true;
            ThemeChoices.Children.OfType<RadioButton>().First(choice => (ThemePreference)choice.Tag == this.viewModel.Theme).IsChecked = true;
            initializing = false;

            this.viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        /// <summary>Set by Show welcome, which closes the window; the main window then shows the first-run panel.</summary>
        public bool ShowWelcomeRequested { get; private set; }

        public SettingsViewModel ViewModel
        {
            get { return viewModel; }
        }

        private string? PickFolder(string current)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose the output folder" };

            if (!string.IsNullOrWhiteSpace(current) && System.IO.Directory.Exists(current))
            {
                dialog.InitialDirectory = current;
            }

            return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
        }

        private static void OpenFolder(string folder)
        {
            try
            {
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception || e is InvalidOperationException)
            {
                // The folder was deleted or Explorer refused; Launch stays a no-op rather than crashing the dialog
            }
        }

        // At high DPI or large text the content can be taller than the screen: cap the height so the body scrolls and the
        // footer stays reachable, then keep the window on the screen. Uses the owner's monitor, where CenterOwner places it.
        private void FitToWorkArea()
        {
            var workArea = WindowLayout.GetWorkAreaInDips(Owner ?? this);
            MaxHeight = workArea.Height;

            if (WindowStartupLocation == WindowStartupLocation.CenterOwner)
            {
                UpdateLayout();
                var bounds = WindowLayout.ClampToWorkArea(new Rect(Left, Top, ActualWidth, ActualHeight), workArea);
                Left = bounds.Left;
                Top = bounds.Top;
            }
        }

        // The LiveSetting on a text block alone doesn't raise the event; the peer has to, after the bindings update
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsViewModel.OutputFolderError) && viewModel.HasOutputFolderError)
            {
                AnnounceLiveRegion(OutputFolderMessage);
            }
            else if (e.PropertyName == nameof(SettingsViewModel.SaveError) && viewModel.HasSaveError)
            {
                AnnounceLiveRegion(SaveErrorMessage);
            }
        }

        // Also scrolls the message into view: when the window is height-capped, the save error sits below the visible part of the body
        private void AnnounceLiveRegion(FrameworkElement element)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                element.BringIntoView();

                if (UIElementAutomationPeer.CreatePeerForElement(element) is AutomationPeer peer)
                {
                    peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
                }
            }));
        }

        #region UI Handlers

        private void Settings_Loaded(object sender, RoutedEventArgs e)
        {
            FitToWorkArea();
            TextboxOutputFolder.Focus();
            TextboxOutputFolder.SelectAll();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.Save())
            {
                DialogResult = true;
            }
            else if (viewModel.HasOutputFolderError)
            {
                TextboxOutputFolder.Focus();
            }
        }

        // Unsaved changes are dropped, as with Cancel
        private void ShowWelcome_Click(object sender, RoutedEventArgs e)
        {
            ShowWelcomeRequested = true;
            DialogResult = false;
        }

        // A click previews the theme right away; only Save keeps it.
        private void Theme_Checked(object sender, RoutedEventArgs e)
        {
            var theme = (ThemePreference)((RadioButton)sender).Tag;
            viewModel.Theme = theme;

            if (!initializing)
            {
                previewedTheme = true;
                ThemeManager.SetPreference(theme);
            }
        }

        // Drops an unsaved preview. After a save this reads back the same choice, so nothing changes.
        private void Settings_Closed(object? sender, EventArgs e)
        {
            if (previewedTheme)
            {
                ThemeManager.SetPreference(RegistryStuff.GetThemePreference());
            }
        }

        #endregion UI Handlers
    }
}
