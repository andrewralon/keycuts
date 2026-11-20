using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using keycuts.GUI.Controls;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI.Views
{
    /// <summary>The Manager (WPF's Views/ManagerView): the table, the search box, the row keys, in-place edits and the row menu.</summary>
    public partial class ManagerView : UserControl
    {
        /// <summary>The table width below which the Modified column is hidden.</summary>
        internal const double ModifiedColumnMinTableWidth = 1040;

        /// <summary>The table width below which Last run is hidden too (#187), after Modified.</summary>
        internal const double LastRunColumnMinTableWidth = 780;

        /// <summary>The table width below which Runs is hidden, last (window 768 shows it, 828 Last run, 1088 Modified).</summary>
        internal const double RunsColumnMinTableWidth = 720;

        private const double RowSlack = 1;

        private ManagerViewModel? viewModel;

        private ContextMenu? activeMenu;

        private long menuClosedAtTick;

        // A row as drawn: RowHeight plus its 1px divider. Measured from the first row that loads; until then, that sum.
        private double rowHeight;

        public ManagerView()
        {
            InitializeComponent();
            DataContextChanged += ManagerView_DataContextChanged;
            AttachedToVisualTree += (sender, e) => FocusSearchOnShow();
            AddHandler(KeyDownEvent, ManagerView_PreviewKeyDown, RoutingStrategies.Tunnel);
            AddHandler(PointerPressedEvent, ManagerView_PreviewPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            AddHandler(ContextRequestedEvent, ManagerView_ContextRequested, RoutingStrategies.Bubble);
            AddHandler(KeyDownEvent, EditBox_PreviewKeyDown, RoutingStrategies.Tunnel);
            AddHandler(LostFocusEvent, EditBox_LostFocus, RoutingStrategies.Bubble);
            AddHandler(MenuItem.ClickEvent, MenuItem_Click, RoutingStrategies.Bubble);

            SearchBox.PropertyChanged += SearchBox_PropertyChanged;
            TableHost.SizeChanged += (sender, e) => TableHost_SizeChanged();
            TableArea.SizeChanged += (sender, e) => FitTableToRows();
            Table.LoadingRow += Table_LoadingRow;
        }

        private ManagerViewModel? ViewModel
        {
            get { return viewModel; }
        }

        private TopLevel? Top
        {
            get { return TopLevel.GetTopLevel(this); }
        }

        private IInputElement? Focused
        {
            get { return Top?.FocusManager?.GetFocusedElement(); }
        }

        private void FocusSearchOnShow()
        {
            // Arrowing through the tabs (#218) keeps the focus there
            if (!RailTabControl.HoldsKeyboardFocus(Focused))
            {
                SearchBox.Focus();
            }
        }

        // Search is disabled while loading; focus it once it becomes available (unless the user went elsewhere, the tabs included)
        private void SearchBox_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == IsEnabledProperty && SearchBox.IsEnabled && !IsKeyboardFocusWithin && !RailTabControl.HoldsKeyboardFocus(Focused))
            {
                SearchBox.Focus();
            }
        }

        #region Columns

        // Columns aren't controls, so they have no generated fields: by position, as declared
        private DataGridColumn NameColumn => Table.Columns[0];

        private DataGridColumn DescriptionColumn => Table.Columns[2];

        private DataGridColumn RunsColumn => Table.Columns[4];

        private DataGridColumn LastRunColumn => Table.Columns[5];

        private DataGridColumn ModifiedColumn => Table.Columns[6];

        // Round the table's corners, and drop the fixed columns when the window is narrow, one at a time: Modified first, then
        // Last run, then Runs
        private void TableHost_SizeChanged()
        {
            var width = TableHost.Bounds.Width;
            ModifiedColumn.IsVisible = width >= ModifiedColumnMinTableWidth;
            LastRunColumn.IsVisible = width >= LastRunColumnMinTableWidth;
            RunsColumn.IsVisible = width >= RunsColumnMinTableWidth;
        }

        #endregion Columns

        #region Keys

        // Up, Down and Enter work from anywhere in the view, so typing in the search box can go straight on to picking a row
        private void ManagerView_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            // While a name or description is edited, its box owns the keys
            if (ViewModel == null || ViewModel.IsMenuOpen || ViewModel.IsEditing)
            {
                return;
            }

            // The create row's keys are its own: Enter creates there, and Delete, the arrows, Home and End edit its text. Only
            // Ctrl+F still goes to the search.
            if (CreateRowHost.IsKeyboardFocusWithin && !(e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control))
            {
                return;
            }

            // Ctrl+Z undoes a delete while the status bar offers it, even from the search box
            if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control && ViewModel.CanUndoDelete)
            {
                e.Handled = true;
                _ = ViewModel.UndoDeleteAsync();
                return;
            }

            switch (e.KeyModifiers)
            {
                case KeyModifiers.Control:
                    HandleControlKey(e);
                    break;
                case KeyModifiers.Control | KeyModifiers.Shift:
                    HandleControlShiftKey(e);
                    break;
                case KeyModifiers.Shift:
                    // Shift+F2 edits the description, as F2 edits the name
                    if (e.Key == Key.F2 && ActionRow() is { } describing)
                    {
                        e.Handled = true;
                        Table.ScrollIntoView(describing, null);
                        ViewModel.BeginEditDescription(describing);
                    }

                    break;
                case KeyModifiers.None:
                    HandlePlainKey(e);
                    break;
            }
        }

        // The search box has the focus almost all the time, so a key that means something in a text box goes to the box when
        // the box has something to act on: Delete and Home/End when it holds text, Ctrl+C when text is selected
        private static bool SearchBoxHasText(KeyEventArgs e)
        {
            return e.Source is TextBox { Text.Length: > 0 };
        }

        private static bool SearchBoxHasSelection(KeyEventArgs e)
        {
            return e.Source is TextBox box && box.SelectionStart != box.SelectionEnd;
        }

        /// <summary>The row a key acts on: the selected one. Null with nothing selected, and the key does nothing.</summary>
        private KeycutRowViewModel? ActionRow()
        {
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
                case Key.F2 when ActionRow() is { } renaming:
                    e.Handled = true;
                    Table.ScrollIntoView(renaming, null);
                    ViewModel!.BeginRename(renaming);
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
                // A button (the footer's Download) acts on Enter itself; the key must not open the selected row instead
                case Key.Enter when e.Source is not Button:
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
            return Math.Max(1, (int)((Table.Bounds.Height - Table.ColumnHeaderHeight) / Table.RowHeight) - 1);
        }

        private void MoveSelection(KeyEventArgs e, Func<ManagerViewModel, bool> move)
        {
            if (!move(ViewModel!))
            {
                return;
            }

            e.Handled = true;
            Table.ScrollIntoView(ViewModel!.SelectedRow, null);
        }

        #endregion Keys

        #region Row menu

        /// <summary>
        /// Shift+F10 or the Menu key: the selected row's actions menu, under the row where its name starts, as a right-click opens it
        /// at the pointer. Like Delete, the keys stay with the search box while it has text, and with the create row.
        /// </summary>
        private void ManagerView_ContextRequested(object? sender, ContextRequestedEventArgs e)
        {
            var fromKeyboard = !e.TryGetPosition(null, out _);
            if (!fromKeyboard || ViewModel == null || ViewModel.IsEditing || CreateRowHost.IsKeyboardFocusWithin ||
                e.Source is TextBox { Text.Length: > 0 })
            {
                return;
            }

            if (OpenSelectedRowMenu())
            {
                e.Handled = true;
            }
        }

        private bool OpenSelectedRowMenu()
        {
            if (ActionRow() is not { } row)
            {
                return false;
            }

            Table.ScrollIntoView(row, null);
            Table.UpdateLayout();

            var container = Table.GetVisualDescendants().OfType<DataGridRow>().FirstOrDefault(r => ReferenceEquals(r.DataContext, row));
            if (container?.ContextMenu is not { } menu)
            {
                return false;
            }

            // Where the name cell's text starts
            menu.PlacementTarget = container;
            MenuPlacement.UnderLeftEdge(menu, 12, 0);
            menu.Open(container);
            return true;
        }

        private void Table_LoadingRow(object? sender, DataGridRowEventArgs e)
        {
            if (rowHeight <= 0)
            {
                e.Row.LayoutUpdated += Row_LayoutUpdated;
            }

            // Each row its own menu, so its bindings follow that row; a recycled row brings its menu along to its new keycut
            if (e.Row.ContextMenu == null && Resources["RowMenu"] is Avalonia.Controls.Templates.IDataTemplate template
                && template.Build(null) is ContextMenu menu)
            {
                e.Row.ContextMenu = menu;
                menu.Tag = e.Row;
                menu.Opened += RowMenu_Opened;
                menu.Closed += RowMenu_Closed;
                menu.AddHandler(KeyDownEvent, RowMenu_PreviewKeyDown, RoutingStrategies.Tunnel);
            }
        }

        private void RowMenu_Opened(object? sender, RoutedEventArgs e)
        {
            var menu = (ContextMenu)sender!;
            activeMenu = menu;
            (menu.Tag as DataGridRow)?.Classes.Set("menuopen", true);

            if (menu.DataContext is KeycutRowViewModel row)
            {
                row.IsMenuOpen = true;

                // A right-click selects the row it opened on, as in Explorer, so the keys act on it too
                if (ViewModel != null)
                {
                    ViewModel.SelectedRow = row;
                }
            }

            if (ViewModel != null)
            {
                ViewModel.IsMenuOpen = true;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (menu.IsOpen && menu.Items.OfType<MenuItem>().FirstOrDefault() is { } first)
                {
                    first.Focus();
                }
            }, DispatcherPriority.Input);
        }

        private void RowMenu_Closed(object? sender, RoutedEventArgs e)
        {
            var menu = (ContextMenu)sender!;
            menuClosedAtTick = Environment.TickCount64;
            (menu.Tag as DataGridRow)?.Classes.Set("menuopen", false);

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

            // Opened from the keyboard, it was placed under its row; a right-click opens it at the pointer again
            menu.Placement = PlacementMode.Pointer;
            menu.HorizontalOffset = 0;
            menu.VerticalOffset = 0;
        }

        private void RowMenu_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                ((ContextMenu)sender!).Close();
                FocusSearchBox();
            }
        }

        // Rows take no focus, so after a non-destructive action the focus goes back to the search box, where the row keys work
        private void MenuItem_Click(object? sender, RoutedEventArgs e)
        {
            if (e.Source is MenuItem item && item.Classes.Contains("Safe"))
            {
                Dispatcher.UIThread.Post(FocusSearchBox, DispatcherPriority.Input);
            }
        }

        #endregion Row menu

        #region Rename and description

        // The box appears when an edit starts; focus it once the row menu (if any) has finished closing
        private void FocusEditBox(string boxClass)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var box = Table.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.Classes.Contains(boxClass) && t.IsEffectivelyVisible);
                if (box != null)
                {
                    box.Focus();
                    box.SelectAll();
                }
            }, DispatcherPriority.Background);
        }

        private void EditBox_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Source is not TextBox box || ViewModel == null)
            {
                return;
            }

            var renaming = box.Classes.Contains("RenameBox");
            if (!renaming && !box.Classes.Contains("DescriptionBox"))
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Enter:
                    e.Handled = true;
                    _ = renaming ? ViewModel.CommitRenameAsync() : ViewModel.CommitDescriptionAsync();
                    break;
                case Key.Escape:
                    // Handled here, so the window doesn't also treat it as "back to the creator"
                    e.Handled = true;
                    if (renaming)
                    {
                        ViewModel.CancelRename();
                    }
                    else
                    {
                        ViewModel.CancelEditDescription();
                    }

                    break;
                case Key.Up:
                case Key.Down:
                case Key.PageUp:
                case Key.PageDown:
                    // The table would move the selection off the row being edited
                    e.Handled = true;
                    break;
            }
        }

        // Clicking or tabbing away saves, as in Explorer. Switching windows (no new focus in this one) keeps editing. Focus moving
        // into the box's own context menu is still part of the edit.
        private void EditBox_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (e.Source is not TextBox box || ViewModel == null || e is not FocusChangedEventArgs focus || focus.NewFocusedElement == null)
            {
                return;
            }

            if (focus.NewFocusedElement is Visual next && next.FindAncestorOfType<ContextMenu>(includeSelf: true) != null)
            {
                return;
            }

            if (box.Classes.Contains("RenameBox") && box.DataContext is KeycutRowViewModel { IsRenaming: true })
            {
                _ = ViewModel.CommitRenameAsync();
            }
            else if (box.Classes.Contains("DescriptionBox") && box.DataContext is KeycutRowViewModel { IsEditingDescription: true })
            {
                // Tabbed or clicked away: the focus is already where the user put it
                _ = ViewModel.CommitDescriptionAsync(restoreFocus: false);
            }
        }

        // Rows, cells and the background can't take focus, so a click there leaves the focus in the box being edited. Save on any
        // click in the view outside the box. With nothing being edited, a click on the name or description of the selected row
        // starts editing it.
        private void ManagerView_PreviewPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (ViewModel == null || e.Source is not Visual source)
            {
                return;
            }

            if (ViewModel.IsEditing)
            {
                if (source.FindAncestorOfType<TextBox>(includeSelf: true) is { } box &&
                    box.DataContext is KeycutRowViewModel { IsRenaming: true } or KeycutRowViewModel { IsEditingDescription: true })
                {
                    return;
                }

                // The click only saves; it doesn't also start another edit
                _ = ViewModel.IsRenaming ? ViewModel.CommitRenameAsync() : ViewModel.CommitDescriptionAsync(restoreFocus: false);
                return;
            }

            BeginEditFromClick(e, source);
        }

        // #202: as in Explorer, a click on the name or description of the row that is already selected edits it, and a click on
        // any other row only selects it
        private void BeginEditFromClick(PointerPressedEventArgs e, Visual source)
        {
            // A plain left click, and not the one that dismissed a menu
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || ViewModel!.IsMenuOpen || Environment.TickCount64 - menuClosedAtTick < 250)
            {
                return;
            }

            var cell = source.FindAncestorOfType<DataGridCell>(includeSelf: true);
            if (cell?.DataContext is not KeycutRowViewModel row || !ReferenceEquals(row, ViewModel.SelectedRow))
            {
                return;
            }

            var column = DataGridColumn.GetColumnContainingElement(cell);
            if (column == NameColumn)
            {
                e.Handled = true;
                ViewModel.BeginRename(row);
            }
            else if (column == DescriptionColumn)
            {
                e.Handled = true;
                ViewModel.BeginEditDescription(row);
            }
        }

        #endregion Rename and description

        #region View model

        private void ManagerView_DataContextChanged(object? sender, EventArgs e)
        {
            if (viewModel != null)
            {
                viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                viewModel.FocusRowRequested -= ViewModel_FocusRowRequested;
                viewModel.ConfirmRename = null;
                viewModel.ShowEditForm = null;
            }

            viewModel = DataContext as ManagerViewModel;

            if (viewModel != null)
            {
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
                viewModel.FocusRowRequested += ViewModel_FocusRowRequested;
                viewModel.ConfirmRename = check => ConfirmWindow.Ask(Top as Window, ConfirmViewModel.ForRename(check));
                // The Edit form (#180) is a dialog over the window; the form saves itself
                viewModel.ShowEditForm = form => EditWindow.Show(Top as Window, form);
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ManagerViewModel.IsRenaming) && ViewModel?.IsRenaming == true)
            {
                FocusEditBox("RenameBox");
            }
            else if (e.PropertyName == nameof(ManagerViewModel.IsEditingDescription) && ViewModel?.IsEditingDescription == true)
            {
                FocusEditBox("DescriptionBox");
            }
        }

        // After a delete, a restore or an edit in place: the row comes into view, and the focus goes to the search box (or the
        // create button while there are no keycuts), where the row keys act on the selected row
        private void ViewModel_FocusRowRequested(object? sender, KeycutRowViewModel? row)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (row != null)
                {
                    Table.ScrollIntoView(row, null);
                }

                if (SearchBox.IsEnabled)
                {
                    FocusSearchBox();
                }
                else
                {
                    EmptyCreateButton.Focus();
                }
            }, DispatcherPriority.Loaded);
        }

        private void FocusSearchBox()
        {
            if (SearchBox.IsEnabled)
            {
                SearchBox.Focus();
            }
        }

        #endregion View model

        #region Whole rows

        /// <summary>
        /// The table's height for <paramref name="available"/> room: its <paramref name="chrome"/> (the header and the border) and as
        /// many whole rows as fit, so the last row at the bottom is never shown in part. With less room than one row, all of it.
        /// </summary>
        public static double FitToRows(double available, double chrome, double rowHeight)
        {
            if (rowHeight <= 0 || available < chrome + rowHeight)
            {
                return available;
            }

            // A pixel of slack: layout rounding at some scalings leaves the area a fraction short of a whole row
            return chrome + Math.Floor((available - chrome + RowSlack) / rowHeight) * rowHeight;
        }

        private void FitTableToRows()
        {
            var row = rowHeight > 0 ? rowHeight : Table.RowHeight + 1;
            var chrome = TableHost.BorderThickness.Top + TableHost.BorderThickness.Bottom + Table.ColumnHeaderHeight;
            TableHost.Height = FitToRows(TableArea.Bounds.Height, chrome, row);
        }

        private void Row_LayoutUpdated(object? sender, EventArgs e)
        {
            if (sender is DataGridRow row && row.Bounds.Height > 0)
            {
                row.LayoutUpdated -= Row_LayoutUpdated;
                if (rowHeight <= 0)
                {
                    rowHeight = row.Bounds.Height;
                    FitTableToRows();
                }
            }
        }

        #endregion Whole rows
    }
}
