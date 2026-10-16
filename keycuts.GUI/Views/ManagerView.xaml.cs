using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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

        // Search is disabled while loading; focus it once it becomes available (unless the user went elsewhere).
        private void SearchBox_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (SearchBox.IsEnabled && !IsKeyboardFocusWithin)
            {
                SearchBox.Focus();
            }
        }

        // Round the table's corners so the pinned header doesn't poke out of the border.
        private void TableHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var radius = TableHost.CornerRadius.TopLeft;
            TableHost.Clip = new RectangleGeometry(new Rect(0, 0, TableHost.ActualWidth, TableHost.ActualHeight), radius, radius);
        }

        // Rows have no click or Enter behavior; stop the grid from moving focus on Enter.
        private void Table_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
            }
        }
    }
}
