using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace keycuts.GUI
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window, INotifyPropertyChanged
    {
        #region Fields

        private string outputFolder;

        private bool forceOverwrite;

        private bool rightClickContextMenu;

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Fields

        #region Properties

        public string LabelOutputFolder { get; } = "Output Folder";

        public string LabelForceOverwrite { get; } = "Force Overwrite";

        public string LabelRightClickContextMenu { get; } = "Right Click Context Menu";

        public Settings Settings { get; set; }

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

        public bool RightClickContextMenu
        {
            get { return rightClickContextMenu; }
            set
            {
                rightClickContextMenu = value;
                NotifyPropertyChanged("RightClickContextMenu");
            }
        }

        #endregion Properties

        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = this;

            Settings = new Settings();
            LoadSettings();
        }

        public void LoadSettings()
        {
            Settings.LoadSettings();

            OutputFolder = Settings.OutputFolder;
            ForceOverwrite = Settings.ForceOverwrite;
        }

        #region UI Handlers

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            var settings = new Settings()
            {
                OutputFolder = OutputFolder,
                ForceOverwrite = (bool)CheckboxForceOverwrite.IsChecked,
                RightClickContextMenu = RightClickContextMenu
            };

            FormLogic.SaveSettings(settings);
        }

        #endregion UI Handlers

        #region OnPropertyChanged Handler

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion OnPropertyChanged Handler
    }
}
