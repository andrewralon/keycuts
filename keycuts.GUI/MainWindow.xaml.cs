using keycuts.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace keycuts.GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Fields

        private MainFormLogic mainFormLogic;

        private CommonLogic commonLogic;

        private string destination;

        private string shortcutName;

        private SettingsWindow settingsWindow = null;

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Fields

        #region Properties

        public string Step1 { get { return Steps[0]; } }

        public string Step2 { get { return Steps[1]; } }

        public string Step3 { get { return Steps[2]; } }

        public string Instructions { get { return string.Join("\n", Steps[3], Steps[4], Steps[5]); } }

        public string Destination
        {
            get { return destination; }
            set
            {
                destination = value;
                NotifyPropertyChanged("Destination");
            }
        }

        public string ShortcutName
        {
            get { return shortcutName; }
            set
            {
                shortcutName = value;
                NotifyPropertyChanged("ShortcutName");
            }
        }

        public string[] Steps = new string[]
        {
            "1. Drag or paste a file, folder, or URL",
            "2. Name the shortcut",
            "3. Create the shortcut",
            "* Type Windows + R",
            "* Type the shortcut name",
            "* Press enter.... Enjoy!"
        };

        #endregion Properties

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            mainFormLogic = new MainFormLogic();
            commonLogic = new CommonLogic();
        }

        private void CreateShortcut()
        {
            ShortcutName = TextboxShortcut.Text;

            BtnCreateShortcut.IsEnabled = false;
            BtnCreateShortcut.IsChecked = true;

            var result = commonLogic.CreateShortcut(Destination, ShortcutName);

            if (result == ExitCode.FileAlreadyExists)
            {
                var dialogResult = MessageBox.Show(this,
                    "Shortcut file already exists. Overwrite it?",
                    "File already exists", MessageBoxButton.YesNo);

                if (dialogResult == MessageBoxResult.Yes)
                {
                    result = commonLogic.CreateShortcut(Destination, ShortcutName, true);
                }
            }

            System.Threading.Thread.Sleep(400);
            System.Media.SystemSounds.Beep.Play();
            BtnCreateShortcut.IsEnabled = true;
            BtnCreateShortcut.IsChecked = false;

            if (result != (int)ExitCode.Success)
            {
                MessageBox.Show($"Error: {result}");
            }
        }

        private void OpenSettings()
        {
            mainFormLogic.OpenSettings(settingsWindow);
        }

        private void ChangeOutputFolder(string outputFolder)
        {
            commonLogic.SetOutputFolder(outputFolder);
        }

        private void CloseWindow()
        {
            Close();
        }

        #region UI Handlers - Buttons, Keys

        private void SettingsIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            OpenSettings();
        }

        private void CreateShortcut_Click(object sender, RoutedEventArgs e)
        {
            CreateShortcut();
        }

        private void Main_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CreateShortcut();
            }
            else if (e.Key == Key.Escape)
            {
                CloseWindow();
            }
        }

        private void OpenBatmanager_Click(object sender, RoutedEventArgs e)
        {
            mainFormLogic.OpenBatmanager();
        }

        #endregion UI Handlers - Buttons, Keys

        #region DragAndDrop Handlers

        private void HandlePreviewDragOver(object sender, DragEventArgs e)
        {
            mainFormLogic.ClearDestination(this);
            e.Handled = true;
        }

        private void HandleDragEnter(object sender, DragEventArgs e)
        {
            DragDrop.DragAndEnter(sender, e);
        }

        private void HandleDragDrop(object sender, DragEventArgs e)
        {
            e.Handled = true;

            var file = DragDrop.GetDroppedFiles(sender, e).FirstOrDefault();
            if (file == null)
            {
                MessageBox.Show("Sorry, not sure what to do with this type of file.", "Oops");
            }
            else
            {
                mainFormLogic.HandleNewDestination(this, file);
            }
        }

        private void Main_DragEnter(object sender, DragEventArgs e)
        {
            HandleDragEnter(sender, e);
        }

        private void Main_Drop(object sender, DragEventArgs e)
        {
            HandleDragDrop(sender, e);
        }

        private void GridMainFull_DragEnter(object sender, DragEventArgs e)
        {
            HandleDragEnter(sender, e);
        }

        private void GridMainFull_Drop(object sender, DragEventArgs e)
        {
            HandleDragDrop(sender, e);
        }

        private void GridMainSettingsIcon_DragEnter(object sender, DragEventArgs e)
        {
            HandleDragEnter(sender, e);
        }

        private void GridMainSettingsIcon_Drop(object sender, DragEventArgs e)
        {
            HandleDragDrop(sender, e);
        }

        private void GridMainMargin_DragEnter(object sender, DragEventArgs e)
        {
            HandleDragEnter(sender, e);
        }

        private void GridMainMargin_Drop(object sender, DragEventArgs e)
        {
            HandleDragDrop(sender, e);
        }

        private void TextboxDestination_PreviewDragOver(object sender, DragEventArgs e)
        {
            HandlePreviewDragOver(sender, e);
        }

        private void TextboxDestination_DragEnter(object sender, DragEventArgs e)
        {
            HandleDragEnter(sender, e);
        }

        private void TextboxDestination_Drop(object sender, DragEventArgs e)
        {
            HandleDragDrop(sender, e);
        }

        #endregion DragAndDrop Handlers

        #region OnPropertyChanged Handler

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion OnPropertyChanged Handler
    }
}
