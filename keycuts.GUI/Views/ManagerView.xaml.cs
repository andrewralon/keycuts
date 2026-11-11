using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

        // Up, Down and Enter work from anywhere in the view, so typing in the search box can go straight on to picking a row.
        private void ManagerView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // While renaming, the name box owns the keys (this handler runs before it)
            if (ViewModel == null || ViewModel.IsMenuOpen || ViewModel.IsRenaming || Keyboard.Modifiers != ModifierKeys.None)
            {
                return;
            }

            if (e.Key == Key.F2 && ViewModel.SelectedRow != null)
            {
                e.Handled = true;
                Table.ScrollIntoView(ViewModel.SelectedRow);
                ViewModel.BeginRename(ViewModel.SelectedRow);
            }
            else if (e.Key == Key.Up || e.Key == Key.Down)
            {
                MoveSelection(e, e.Key == Key.Down ? 1 : -1);
            }
            else if (e.Key == Key.Enter && e.OriginalSource is not ButtonBase)
            {
                e.Handled = ViewModel.OpenSelected();
            }
        }

        private void MoveSelection(KeyEventArgs e, int direction)
        {
            // Tabbing to a row's menu button puts the user on that row, so move from there and keep the focus with the selection
            var focusedRow = (Keyboard.FocusedElement as MenuButton)?.DataContext as KeycutRowViewModel;

            if (focusedRow != null)
            {
                ViewModel!.SelectedRow = focusedRow;
            }

            if (!ViewModel!.MoveSelection(direction))
            {
                return;
            }

            e.Handled = true;

            var row = ViewModel.SelectedRow!;
            Table.ScrollIntoView(row);

            if (focusedRow != null)
            {
                FocusRowMenuButton(row);
            }
        }

        #region Rename

        // The box appears when a rename starts; focus it once the row menu (if any) has finished closing
        private void RenameBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is TextBox box && box.IsVisible)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
                {
                    if (box.IsVisible)
                    {
                        box.Focus();
                        Keyboard.Focus(box);
                        box.SelectAll();
                    }
                }));
            }
        }

        private void RenameBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    e.Handled = true;
                    _ = ViewModel?.CommitRenameAsync();
                    break;
                case Key.Escape:
                    // Handled here, so the window doesn't also treat it as "back to the creator"
                    e.Handled = true;
                    ViewModel?.CancelRename();
                    break;
                case Key.Up:
                case Key.Down:
                case Key.PageUp:
                case Key.PageDown:
                    // The table would move the selection off the row being renamed
                    e.Handled = true;
                    break;
            }
        }

        // Rows, cells and the background can't take focus, so a click there leaves the focus in the name box and
        // LostKeyboardFocus never fires. Save on any click in the view outside the box; the click itself goes on as usual.
        private void ManagerView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel?.IsRenaming != true || e.OriginalSource is not DependencyObject source)
            {
                return;
            }

            // A click on text can report a Run, which isn't a Visual, so walk its logical parent until a Visual is reached
            for (var node = source; node != null; node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            {
                if (node is TextBox { DataContext: KeycutRowViewModel { IsRenaming: true } })
                {
                    return;
                }
            }

            _ = ViewModel.CommitRenameAsync();
        }

        // Clicking or tabbing away saves, as in Explorer. Switching windows (no new focus in this one) keeps editing.
        // Focus moving into the box's own context menu (right-click, Shift+F10: Cut, Copy, Paste) is still part of the edit.
        private void RenameBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.NewFocus != null && sender is TextBox { DataContext: KeycutRowViewModel { IsRenaming: true } } box &&
                !IsInContextMenuOf(box, e.NewFocus as DependencyObject))
            {
                _ = ViewModel?.CommitRenameAsync();
            }
        }

        /// <summary>True if <paramref name="element"/> is in a context menu opened for <paramref name="owner"/>.</summary>
        internal static bool IsInContextMenuOf(UIElement owner, DependencyObject? element)
        {
            // Menu items' logical parent is their menu; the menu itself lives in a separate popup tree
            for (var node = element; node != null; node = LogicalTreeHelper.GetParent(node) ?? (node is Visual ? VisualTreeHelper.GetParent(node) : null))
            {
                if (node is ContextMenu menu)
                {
                    return ReferenceEquals(menu.PlacementTarget, owner);
                }
            }

            return false;
        }

        #endregion Rename

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
                oldViewModel.ConfirmRename = null;
            }

            if (e.NewValue is ManagerViewModel newViewModel)
            {
                newViewModel.FocusRowRequested += ViewModel_FocusRowRequested;
                newViewModel.ConfirmRename = check => ConfirmWindow.Ask(Window.GetWindow(this), ConfirmViewModel.ForRename(check));
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
                if (row != null && FocusRowMenuButton(row))
                {
                    return;
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

        private bool FocusRowMenuButton(KeycutRowViewModel row)
        {
            Table.ScrollIntoView(row);
            Table.UpdateLayout();

            if (Table.ItemContainerGenerator.ContainerFromItem(row) is DataGridRow container &&
                FindDescendant<MenuButton>(container) is MenuButton button)
            {
                button.Focus();
                Keyboard.Focus(button);
                return true;
            }

            return false;
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
