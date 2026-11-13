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

        /// <summary>The table width below which the Modified column is hidden: the other columns need their room more.</summary>
        internal const double ModifiedColumnMinTableWidth = 780;

        // Round the table's corners so the pinned header doesn't poke out of the border, and drop the Modified column
        // when the window is narrow (at its minimum width the other columns' minimum widths already fill the table).
        private void TableHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var radius = TableHost.CornerRadius.TopLeft;
            TableHost.Clip = new RectangleGeometry(new Rect(0, 0, TableHost.ActualWidth, TableHost.ActualHeight), radius, radius);

            ModifiedColumn.Visibility = TableHost.ActualWidth < ModifiedColumnMinTableWidth ? Visibility.Collapsed : Visibility.Visible;
        }

        // Up, Down and Enter work from anywhere in the view, so typing in the search box can go straight on to picking a row.
        private void ManagerView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // While renaming, the name box owns the keys (this handler runs before it). Modifiers come from the event's own device:
            // in the app that is the physical keyboard, as with Keyboard.Modifiers, but a test can give the event its own.
            if (ViewModel == null || ViewModel.IsMenuOpen || ViewModel.IsRenaming)
            {
                return;
            }

            // Ctrl+Z undoes a delete while the status bar offers it, even from the search box, where it would otherwise be
            // the box's own undo. Any other time the box keeps it.
            if (e.Key == Key.Z && e.KeyboardDevice.Modifiers == ModifierKeys.Control && ViewModel.CanUndoDelete)
            {
                e.Handled = true;
                _ = ViewModel.UndoDeleteAsync();
                return;
            }

            var modifiers = e.KeyboardDevice.Modifiers;

            if (modifiers == ModifierKeys.Control)
            {
                HandleControlKey(e);
            }
            else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                HandleControlShiftKey(e);
            }
            else if (modifiers == ModifierKeys.None)
            {
                HandlePlainKey(e);
            }
        }

        // The search box has the focus almost all the time, so a key that means something in a text box goes to the box
        // when the box has something to act on: Delete and Home/End when it holds text, Ctrl+C when text is selected. A
        // mistaken Delete costs little, since a delete has an Undo (#179).
        private static bool SearchBoxHasText(KeyEventArgs e)
        {
            return e.OriginalSource is TextBox { Text.Length: > 0 };
        }

        private static bool SearchBoxHasSelection(KeyEventArgs e)
        {
            return e.OriginalSource is TextBox { SelectionLength: > 0 };
        }

        /// <summary>
        /// The row a key acts on: the selected one. Tabbing to a row's menu button puts the user on that row, so a key
        /// pressed there acts on it, and the selection follows. Null with nothing selected, and the key does nothing.
        /// </summary>
        private KeycutRowViewModel? ActionRow()
        {
            if ((Keyboard.FocusedElement as MenuButton)?.DataContext is KeycutRowViewModel focused)
            {
                ViewModel!.SelectedRow = focused;
            }

            return ViewModel!.SelectedRow;
        }

        private void HandleControlKey(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F:
                    // Search from anywhere, with what was typed selected, so the next letter starts a new search
                    if (SearchBox.IsEnabled)
                    {
                        e.Handled = true;
                        SearchBox.Focus();
                        Keyboard.Focus(SearchBox);
                        SearchBox.SelectAll();
                    }

                    break;
                case Key.C when !SearchBoxHasSelection(e) && ActionRow() is { } copy:
                    e.Handled = true;
                    ViewModel!.CopyTarget(copy);
                    break;
                case Key.E when ActionRow() is { } edit:
                    e.Handled = true;
                    _ = ViewModel!.EditKeycutAsync(edit);
                    break;
            }
        }

        private void HandleControlShiftKey(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.E when ActionRow() is { } notepad:
                    e.Handled = true;
                    ViewModel!.EditInNotepad(notepad);
                    break;
                case Key.O when ActionRow() is { } location:
                    e.Handled = true;
                    ViewModel!.OpenFileLocation(location);
                    break;
            }
        }

        private void HandlePlainKey(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F2 when ViewModel!.SelectedRow != null:
                    e.Handled = true;
                    Table.ScrollIntoView(ViewModel.SelectedRow);
                    ViewModel.BeginRename(ViewModel.SelectedRow);
                    break;
                case Key.Up:
                case Key.Down:
                    MoveSelection(e, model => model.MoveSelection(e.Key == Key.Down ? 1 : -1));
                    break;
                case Key.PageUp:
                case Key.PageDown:
                    MoveSelection(e, model => model.MoveSelection(e.Key == Key.PageDown ? RowsPerPage() : -RowsPerPage()));
                    break;
                case Key.Home when !SearchBoxHasText(e):
                    MoveSelection(e, model => model.SelectFirst());
                    break;
                case Key.End when !SearchBoxHasText(e):
                    MoveSelection(e, model => model.SelectLast());
                    break;
                case Key.Enter when e.OriginalSource is not ButtonBase:
                    e.Handled = ViewModel!.OpenSelected();
                    break;
                case Key.Delete when !SearchBoxHasText(e) && ActionRow() is { } doomed:
                    e.Handled = true;
                    _ = ViewModel!.DeleteKeycutAsync(doomed);
                    break;
            }
        }

        /// <summary>How far Page Up and Page Down move: the rows the table shows, less one so the last one stays in view.</summary>
        private int RowsPerPage()
        {
            return Math.Max(1, (int)((Table.ActualHeight - Table.ColumnHeaderHeight) / Table.RowHeight) - 1);
        }

        private void MoveSelection(KeyEventArgs e, Func<ManagerViewModel, bool> move)
        {
            // Tabbing to a row's menu button puts the user on that row, so move from there and keep the focus with the selection
            var focusedRow = (Keyboard.FocusedElement as MenuButton)?.DataContext as KeycutRowViewModel;

            if (focusedRow != null)
            {
                ViewModel!.SelectedRow = focusedRow;
            }

            if (!move(ViewModel!))
            {
                return;
            }

            e.Handled = true;

            var row = ViewModel!.SelectedRow!;
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
                oldViewModel.ShowEditForm = null;
            }

            if (e.NewValue is ManagerViewModel newViewModel)
            {
                newViewModel.FocusRowRequested += ViewModel_FocusRowRequested;
                newViewModel.ConfirmRename = check => ConfirmWindow.Ask(Window.GetWindow(this), ConfirmViewModel.ForRename(check));
                // The Edit form (#180) is a dialog over the window; the form saves itself, and its name checks use the same dialog
                newViewModel.ShowEditForm = form => EditWindow.Show(Window.GetWindow(this), form);
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
            var altDown = (e.Key == Key.Down && e.KeyboardDevice.Modifiers == ModifierKeys.Alt) ||
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
