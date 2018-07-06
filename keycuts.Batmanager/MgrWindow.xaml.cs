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

namespace keycuts.Batmanager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MgrWindow : Window, INotifyPropertyChanged
    {
        #region Fields

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Fields

        #region Properties



        #endregion Properties
        
        #region Constructors

        public MgrWindow()
        {
            InitializeComponent();
            DataContext = this;

            PopulateDataGrid();
        }

        #endregion Constructors

        #region Public Methods

        public void PopulateDataGrid()
        {
            BatFormLogic.PopulateDataGrid(DataGrid);
        }

        #endregion Public Methods

        #region Handlers

        private void DataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            PopulateDataGrid();
        }

        private void Mgr_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                PopulateDataGrid();
            }
        }

        #endregion Handlers

        #region OnPropertyChanged Handler

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion OnPropertyChanged Handler
    }
}
