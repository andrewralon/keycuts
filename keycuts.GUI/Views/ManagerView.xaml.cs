using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace keycuts.GUI.Views
{
    public partial class ManagerView : UserControl
    {
        public ManagerView()
        {
            InitializeComponent();
        }

        private void ManagerView_Loaded(object sender, RoutedEventArgs e)
        {
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
        }
    }
}
