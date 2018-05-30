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
    public partial class MainWindow : Window
    {
        #region Fields

        public string _step1 = "1. Drag in or paste a file, folder, or URL";
        public string _step2 = "2. Choose a name for the shortcut";
        public string _step3 = "3. Create the shortcut";

        #endregion Fields

        #region Properties

        public string Step1
        {
            get { return _step1; }
            set
            {
                _step1 = value;
                OnPropertyChanged();
            }
        }

        public string Step2
        {
            get { return _step2; }
            set
            {
                _step2 = value;
                OnPropertyChanged();
            }
        }

        public string Step3
        {
            get { return _step3; }
            set
            {
                _step3 = value;
                OnPropertyChanged();
            }
        }

        #endregion Properties

        public string[] Steps = new string[]
        {
            "1. Drag in or paste a file, folder, or URL",
            "2. Choose a name for the shortcut",
            "3. Create the shortcut",
            "Instructions:",
            "* Press Windows + R",
            "* Type the shortcut name,",
            "* Press enter. Enjoy!"
        };

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName]string propertyName = null)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }
    }
}
