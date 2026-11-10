using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

        #region UI Handlers

        private void Settings_Loaded(object sender, RoutedEventArgs e)
        {
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
