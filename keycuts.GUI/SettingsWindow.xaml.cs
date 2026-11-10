using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using keycuts.Common;
using keycuts.GUI.Themes;

namespace keycuts.GUI
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window, INotifyPropertyChanged
    {
        #region Fields

        private MainFormLogic mainFormLogic;

        private Settings settings;

        private string outputFolder = string.Empty;

        private bool forceOverwrite;

        private bool rightClickContextMenus;

        private ThemePreference theme;

        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion Fields

        #region Properties

        public string LabelOutputFolder { get; } = "Output Folder";

        public string LabelForceOverwrite { get; } = "Force Overwrite (if shortcut exists)";

        public string LabelRightClickContextMenus { get; } = "Right Click Context Menus";

        public string LabelUseEnvironmentVariables { get; } = "Use Environment Variables in Paths";

        private bool useEnvironmentVariables;

        /// <summary>Save new file and folder keycuts as %USERPROFILE%\... and the like (#31). Shift flips it per keycut.</summary>
        public bool UseEnvironmentVariables
        {
            get { return useEnvironmentVariables; }
            set
            {
                useEnvironmentVariables = value;
                NotifyPropertyChanged();
            }
        }

        public string LabelTheme { get; } = "Theme";

        /// <summary>Set by Show welcome, which closes the window; the main window then shows the first-run panel.</summary>
        public bool ShowWelcomeRequested { get; private set; }

        public string OutputFolder
        {
            get { return outputFolder; }
            set
            {
                outputFolder = value;
                NotifyPropertyChanged("OutputFolder");
            }
        }

        public bool ForceOverwrite
        {
            get { return forceOverwrite; }
            set
            {
                forceOverwrite = value;
                NotifyPropertyChanged("ForceOverwrite");
            }
        }

        public bool RightClickContextMenus
        {
            get { return rightClickContextMenus; }
            set
            {
                rightClickContextMenus = value;
                NotifyPropertyChanged("RightClickContextMenus");
            }
        }

        #endregion Properties

        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = this;

            settings = new Settings();
            mainFormLogic = new MainFormLogic();
            LoadSettings();
        }

        private void LoadSettings()
        {
            settings.LoadSettings();

            OutputFolder = settings.OutputFolder;
            ForceOverwrite = settings.ForceOverwrite;
            RightClickContextMenus = settings.RightClickContextMenus;
            UseEnvironmentVariables = settings.UseEnvironmentVariables;

            theme = settings.Theme;
            ThemeChoices.Children.OfType<RadioButton>().First(choice => (ThemePreference)choice.Tag == theme).IsChecked = true;
        }

        private void CloseWindow()
        {
            Close();
        }

        #region UI Handlers

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            var settings = new Settings()
            {
                OutputFolder = OutputFolder,
                ForceOverwrite = CheckboxForceOverwrite.IsChecked == true,
                RightClickContextMenus = RightClickContextMenus,
                UseEnvironmentVariables = UseEnvironmentVariables,
                Theme = theme
            };

            var error = mainFormLogic.SaveSettings(settings);

            if (error != null)
            {
                MessageBox.Show(this, error, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Unsaved changes are dropped, as with Escape
        private void ShowWelcome_Click(object sender, RoutedEventArgs e)
        {
            ShowWelcomeRequested = true;
            CloseWindow();
        }

        // A click previews the theme right away; only Save keeps it.
        private void Theme_Checked(object sender, RoutedEventArgs e)
        {
            theme = (ThemePreference)((RadioButton)sender).Tag;
            ThemeManager.SetPreference(theme);
        }

        // Drops an unsaved preview. After a save this reads back the same choice, so nothing changes.
        private void Settings_Closed(object? sender, EventArgs e)
        {
            ThemeManager.SetPreference(RegistryStuff.GetThemePreference());
        }

        private void Settings_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CloseWindow();
            }
        }

        #endregion UI Handlers

        #region OnPropertyChanged Handler

        protected void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion OnPropertyChanged Handler
    }
}
