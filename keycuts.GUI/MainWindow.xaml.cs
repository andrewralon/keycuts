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

        #endregion Fields

        #region Properties

        public string Step1 { get { return Steps[0]; } }

        public string Step2 { get { return Steps[1]; } }

        public string Step3 { get { return Steps[2]; } }

        public string Instructions { get { return string.Join("\n", Steps[3], Steps[4], Steps[5]); } }

        #endregion Properties

        public string[] Steps = new string[]
        {
            "1. Drag in or paste a file, folder, or URL",
            "2. Choose a name for the shortcut",
            "3. Create the shortcut",
            "* Type Windows + R",
            "* Type the shortcut name",
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
