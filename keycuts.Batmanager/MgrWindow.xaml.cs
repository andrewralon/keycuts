using keycuts.Common;
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
        private BatFormLogic batFormLogic;

        private string outputFolder;

        public event PropertyChangedEventHandler PropertyChanged;

        public MgrWindow()
        {
            InitializeComponent();
            DataContext = this;
            batFormLogic = new BatFormLogic();
        }

        private void DataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshList();
        }

        public void RefreshList()
        {
            outputFolder = RegistryStuff.GetOutputFolder(Runner.DefaultOutputFolder);
            TextboxOutputFolder.Text = outputFolder;
            batFormLogic.PopulateDataGrid(DataGrid, outputFolder);
        }

        #region Handlers

        private void ButtonRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshList();
        }

        private void Mgr_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                RefreshList();
            }
        }

        private void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter ||
                (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control))
            {
                batFormLogic.Edit(DataGrid);
            }
            else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
            {
                batFormLogic.Run(DataGrid);
            }
            else if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
            {
                batFormLogic.OpenDestinationLocation(DataGrid);
            }
            //else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            //{
            //    batFormLogic.Copy(DataGrid); // Not needed -- works already
            //}
            else if (e.Key == Key.Delete)
            {
                batFormLogic.Delete(DataGrid);
            }
        }

        private void RightClickMenu_Edit(object sender, RoutedEventArgs e)
        {
            batFormLogic.Edit(DataGrid);
        }

        private void RightClickMenu_Run(object sender, RoutedEventArgs e)
        {
            batFormLogic.Run(DataGrid);
        }

        private void RightClickMenu_OpenDestinationLocation(object sender, RoutedEventArgs e)
        {
            batFormLogic.OpenDestinationLocation(DataGrid);
        }

        private void RightClickMenu_Copy(object sender, RoutedEventArgs e)
        {
            //batFormLogic.Copy(DataGrid); // Not needed -- works already
        }

        private void RightClickMenu_Delete(object sender, RoutedEventArgs e)
        {
            batFormLogic.Delete(DataGrid);
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
