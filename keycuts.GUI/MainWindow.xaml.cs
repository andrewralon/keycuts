using keycuts.CLI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
    public partial class MainWindow : Window
    {
        #region Fields

        private string defaultFolder;

        private string destination;

        private string shortcutName;

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Fields

        #region Properties

        public string Step1 { get { return Steps[0]; } }

        public string Step2 { get { return Steps[1]; } }

        public string Step3 { get { return Steps[2]; } }

        public string Instructions { get { return string.Join("\n", Steps[3], Steps[4], Steps[5]); } }

        public string Destination { get { return destination; }
            set
            {
                destination = value;
                NotifyPropertyChanged("Destination");
            }
        }

        public string ShortcutName { get { return shortcutName; }
            set
            {
                shortcutName = value;
                NotifyPropertyChanged("ShortcutName");
            }
        }

        #endregion Properties

        public string[] Steps = new string[]
        {
            "1. Drag or paste a file, folder, or URL",
            "2. Name the shortcut",
            "3. Create the shortcut",
            "* Type Windows + R",
            "* Type the shortcut name",
            "* Press enter.... Enjoy!"
        };

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            defaultFolder = RegistryKey.GetDefaultShortcutsFolder(Program.DefaultFolder);
        }

        #region Event Handlers - Buttons, etc

        private void CreateShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (Destination != "" && ShortcutName != "")
            {
                defaultFolder = RegistryKey.GetDefaultShortcutsFolder(Program.DefaultFolder);

                var args = new string[]
                {
                    // Surround with quotes?
                    $"-d {Destination}", 
                    $"-s {ShortcutName}"
                };

                Program.Main(args);
            }
        }

        private void OpenShortcutsFolder_Click(object sender, RoutedEventArgs e)
        {
            defaultFolder = RegistryKey.GetDefaultShortcutsFolder(Program.DefaultFolder);

            Process.Start(defaultFolder);
        }

        #endregion Event Handlers - Buttons, etc

        #region DragAndDrop Handlers

        private void HandlePreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = false;
        }

        private void HandleDragEnter(object sender, DragEventArgs e)
        {
            DragDrop.DragAndEnter(sender, e);
        }

        private void HandleDragDrop(object sender, DragEventArgs e)
        {
            var file = DragDrop.GetDroppedFiles(sender, e).FirstOrDefault();

            // Follow the link (if it exists) and set the path textbox
            Destination = Shortcut.GetWindowsLinkTargetPath(file);

            // Focus on the shortcut name textbox
            TextboxShortcut.Focus();

            // Activate this window (normally keeps focus on whatever was previously active)
            Admin.ActivateThisWindow();
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
