using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using keycuts.GUI.Controls;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI.Views
{
    public partial class ManagerView : UserControl
    {
        private ContextMenu? activeMenu;

        private long menuClosedAtTick;

        public ManagerView()
        {
            InitializeComponent();
            DataContextChanged += ManagerView_DataContextChanged;
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

        #region Row actions menu

        private ManagerViewModel? ViewModel
        {
            get { return DataContext as ManagerViewModel; }
        }

        private void ManagerView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ManagerViewModel oldViewModel)
            {
                oldViewModel.FocusRowRequested -= ViewModel_FocusRowRequested;
            }

            if (e.NewValue is ManagerViewModel newViewModel)
            {
                newViewModel.FocusRowRequested += ViewModel_FocusRowRequested;
            }
        }

        private void RowMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuButton button || button.ContextMenu == null)
            {
                return;
            }

            var menu = button.ContextMenu;

            // A click on the button while its menu is open dismisses the menu first, so don't reopen it.
            if (menu.IsOpen || (Environment.TickCount64 - menuClosedAtTick < 250 && button.IsMouseOver))
            {
                menu.IsOpen = false;
                return;
            }

            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }

        private void RowMenu_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var altDown = (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.Alt) ||
                          (e.Key == Key.System && e.SystemKey == Key.Down);

            if (altDown && sender is MenuButton button && button.ContextMenu != null)
            {
                e.Handled = true;
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void RowMenu_Opened(object sender, RoutedEventArgs e)
        {
            var menu = (ContextMenu)sender;
            activeMenu = menu;

            if (menu.DataContext is KeycutRowViewModel row)
            {
                row.IsMenuOpen = true;
            }

            if (ViewModel != null)
            {
                ViewModel.IsMenuOpen = true;
            }

            (menu.PlacementTarget as MenuButton)?.RaiseMenuStateChanged(false);
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (menu.IsOpen && menu.ItemContainerGenerator.ContainerFromIndex(0) is MenuItem first)
                {
                    first.Focus();
                }
            }));
        }

        private void RowMenu_Closed(object sender, RoutedEventArgs e)
        {
            var menu = (ContextMenu)sender;
            menuClosedAtTick = Environment.TickCount64;

            if (menu.DataContext is KeycutRowViewModel row)
            {
                row.IsMenuOpen = false;
            }

            if (ReferenceEquals(activeMenu, menu))
            {
                activeMenu = null;

                if (ViewModel != null)
                {
                    ViewModel.IsMenuOpen = false;
                }
            }

            (menu.PlacementTarget as MenuButton)?.RaiseMenuStateChanged(true);
        }

        private void RowMenuPopup_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                var menu = (ContextMenu)sender;
                menu.IsOpen = false;
                menu.PlacementTarget?.Focus();
            }
        }

        // After a non-destructive action the focus goes back to the row's menu button.
        private void SafeItem_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsControl.ItemsControlFromItemContainer((MenuItem)sender) is ContextMenu menu &&
                menu.PlacementTarget is UIElement button)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => button.Focus()));
            }
        }

        // After a delete the focus moves to the next row's menu button, or to the search box (or the create button) if no rows are left.
        private void ViewModel_FocusRowRequested(object? sender, KeycutRowViewModel? row)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (row != null)
                {
                    Table.ScrollIntoView(row);
                    Table.UpdateLayout();

                    if (Table.ItemContainerGenerator.ContainerFromItem(row) is DataGridRow container &&
                        FindDescendant<MenuButton>(container) is MenuButton button)
                    {
                        button.Focus();
                        Keyboard.Focus(button);
                        return;
                    }
                }

                if (SearchBox.IsEnabled)
                {
                    SearchBox.Focus();
                }
                else
                {
                    EmptyCreateButton.Focus();
                }
            }));
        }

        private static T? FindDescendant<T>(DependencyObject parent)
            where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T match)
                {
                    return match;
                }

                var found = FindDescendant<T>(child);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        #endregion Row actions menu
    }
}
