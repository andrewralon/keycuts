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
        public event PropertyChangedEventHandler PropertyChanged;

        //public ContextMenu RightClickMenu = new ContextMenu();

        public MgrWindow()
        {
            InitializeComponent();
            DataContext = this;

            PopulateRightClickContextMenu();
            PopulateDataGrid();
        }

        private void PopulateRightClickContextMenu()
        {
            //RightClickMenu.Items.Add(PopulateMenuItem("Open", ApplicationCommands.Open));
            //RightClickMenu.Items.Add(PopulateMenuItem("Copy", ApplicationCommands.Copy));
            //RightClickMenu.Items.Add(PopulateMenuItem("Edit", ApplicationCommands.NotACommand));
            //RightClickMenu.Items.Add(PopulateMenuItem("Open Destination Location", ApplicationCommands.NotACommand));
            //RightClickMenu.Items.Add(PopulateMenuItem("Delete", ApplicationCommands.Delete));
        }

        private MenuItem PopulateMenuItem(string name, object command)
        {
            return new MenuItem()
            {
                Header = name,
                Command = command as ICommand
            };
        }

        private void PopulateDataGrid()
        {
            BatFormLogic.PopulateDataGrid(DataGrid);
        }

        private void ShowRightClickContextMenu(DataGrid sender)
        {
            var cm = FindResource("DataGridRightClickMenu") as ContextMenu;
            cm.PlacementTarget = sender;
            cm.IsOpen = true;

            //RightClickMenu.PlacementTarget = sender;
            //RightClickMenu.IsOpen = true;
        }

        private void Copy(object sender)
        {

        }

        private void Run(object sender)
        {

        }

        private void Edit(object sender)
        {

        }

        private void OpenDestinationLocation(object sender)
        {

        }

        private void Delete(object sender)
        {

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
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Copy(sender);
            }
            else if (e.Key == Key.Enter || 
                (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control))
            {
                Edit(sender);
            }
            else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Run(sender);
            }
            else if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
            {
                OpenDestinationLocation(sender);
            }
            else if (e.Key == Key.Delete)
            {
                Delete(sender);
            }
        }

        private void DataGrid_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dep = (DependencyObject)e.OriginalSource;
            while ((dep != null) && !(dep is DataGridCell))
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            if (dep != null)
            {
                if (dep is DataGridCell)
                {
                    var cell = dep as DataGridCell;
                    cell.Focus();

                    while ((dep != null) && !(dep is DataGridRow))
                    {
                        dep = VisualTreeHelper.GetParent(dep);
                    }

                    var row = dep as DataGridRow;
                    DataGrid.SelectedItem = row.DataContext;

                    ShowRightClickContextMenu(sender as DataGrid);
                }
            }
        }

        private void RightClickMenu_Copy(object sender, RoutedEventArgs e)
        {

        }

        private void RightClickMenu_Run(object sender, RoutedEventArgs e)
        {

        }

        private void RightClickMenu_Edit(object sender, RoutedEventArgs e)
        {

        }

        private void RightClickMenu_OpenDestinationLocation(object sender, RoutedEventArgs e)
        {

        }

        private void RightClickMenu_Delete(object sender, RoutedEventArgs e)
        {

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
