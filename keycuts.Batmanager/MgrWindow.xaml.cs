using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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
        public event PropertyChangedEventHandler PropertyChanged;

        public MgrWindow()
        {
            InitializeComponent();
            DataContext = this;

            PopulateDataGrid();
        }

        private void PopulateDataGrid()
        {
            BatFormLogic.PopulateDataGrid(DataGrid);
        }

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
            else if (e.Key == Key.Enter || 
                (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control))
            {
                BatFormLogic.Edit(DataGrid);
            }
            else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BatFormLogic.Run(DataGrid);
            }
            //else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            //{
            //    Copy(sender); // Not needed -- works already
            //}
            else if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BatFormLogic.OpenDestinationLocation(DataGrid);
            }
            else if (e.Key == Key.Delete)
            {
                var mod = Keyboard.Modifiers;
                BatFormLogic.Delete(DataGrid);
            }
        }

        private void RightClickMenu_Edit(object sender, RoutedEventArgs e)
        {
            BatFormLogic.Edit(DataGrid);
        }

        private void RightClickMenu_Run(object sender, RoutedEventArgs e)
        {
            BatFormLogic.Run(DataGrid);
        }

        private void RightClickMenu_Copy(object sender, RoutedEventArgs e)
        {
            //Copy(DataGrid); // Not needed -- works already
        }

        private void RightClickMenu_OpenDestinationLocation(object sender, RoutedEventArgs e)
        {
            BatFormLogic.OpenDestinationLocation(DataGrid);
        }

        private void RightClickMenu_Delete(object sender, RoutedEventArgs e)
        {
            BatFormLogic.Delete(DataGrid);
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
