using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using keycuts.Common;
using keycuts.GUI.ViewModels;
using Microsoft.Win32;

namespace keycuts.GUI.Views
{
    /// <summary>
    /// Creator view: drop, paste, type and Browse feed the destination; Enter in the name field or the Create button submits.
    /// </summary>
    public partial class CreatorView : UserControl
    {
        private static readonly TimeSpan TypingDelay = TimeSpan.FromMilliseconds(300);

        private static readonly TimeSpan NameCheckDelay = TimeSpan.FromMilliseconds(250);

        private readonly MainFormLogic mainFormLogic = new MainFormLogic();

        private readonly DispatcherTimer typingTimer;

        private readonly DispatcherTimer nameTimer;

        private int resolveVersion;

        private bool suppressTextChanged;

        private long menuClosedAtTick;

        public CreatorView()
        {
            InitializeComponent();
            typingTimer = new DispatcherTimer { Interval = TypingDelay };
            typingTimer.Tick += TypingTimer_Tick;
            nameTimer = new DispatcherTimer { Interval = NameCheckDelay };
            nameTimer.Tick += NameTimer_Tick;
            DataObject.AddPastingHandler(TextboxDestination, TextboxDestination_Pasting);
            DataContextChanged += CreatorView_DataContextChanged;
        }

        private CreatorViewModel? ViewModel
        {
            get { return DataContext as CreatorViewModel; }
        }

        private void CreatorView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is CreatorViewModel oldViewModel)
            {
                oldViewModel.BrowseFileRequested -= ViewModel_BrowseFileRequested;
                oldViewModel.BrowseFolderRequested -= ViewModel_BrowseFolderRequested;
                oldViewModel.FocusRequested -= ViewModel_FocusRequested;
                oldViewModel.KeycutCreated -= ViewModel_KeycutCreated;
            }

            if (e.NewValue is CreatorViewModel newViewModel)
            {
                newViewModel.BrowseFileRequested += ViewModel_BrowseFileRequested;
                newViewModel.BrowseFolderRequested += ViewModel_BrowseFolderRequested;
                newViewModel.FocusRequested += ViewModel_FocusRequested;
                newViewModel.KeycutCreated += ViewModel_KeycutCreated;
            }
        }

        private void ViewModel_KeycutCreated(object? sender, EventArgs e)
        {
            System.Media.SystemSounds.Beep.Play();
        }

        private void ViewModel_FocusRequested(object? sender, CreatorFocusTarget target)
        {
            // The name and destination fields are disabled while saving; wait for the layout to re-enable them
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                var field = target == CreatorFocusTarget.Destination ? TextboxDestination : TextboxShortcut;
                field.Focus();
                Keyboard.Focus(field);
                if (target == CreatorFocusTarget.NameSelectAll)
                {
                    field.SelectAll();
                }
                else
                {
                    field.CaretIndex = field.Text.Length;
                }
            }));
        }

        private void TextboxShortcut_TextChanged(object sender, TextChangedEventArgs e)
        {
            nameTimer.Stop();
            if (!string.IsNullOrWhiteSpace(TextboxShortcut.Text))
            {
                nameTimer.Start();
            }
        }

        private void NameTimer_Tick(object? sender, EventArgs e)
        {
            nameTimer.Stop();
            if (ViewModel != null)
            {
                _ = ViewModel.CheckDuplicateNameAsync();
            }
        }

        private void CreatorView_Loaded(object sender, RoutedEventArgs e)
        {
            var nameFirst = ViewModel?.FocusNameFieldOnShow == true;
            var target = nameFirst ? TextboxShortcut : TextboxDestination;
            target.Focus();
            Keyboard.Focus(target);
        }

        /// <summary>Ctrl+V anywhere except the two text fields pastes into the destination.</summary>
        private void CreatorView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control || ViewModel?.IsInputEnabled != true)
            {
                return;
            }

            if (Keyboard.FocusedElement is TextBox)
            {
                return;
            }

            IDataObject? data = null;
            try
            {
                data = Clipboard.GetDataObject();
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // The clipboard is busy; treat it as empty
            }

            if (DragDrop.TryGetDestination(data, out var text))
            {
                e.Handled = true;
                ApplyInput(text);
            }
        }

        #region Browse

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            // Clicking the button while the menu is open first closes the menu (click outside); don't reopen it right away
            if (BrowseMenu.IsOpen || (Environment.TickCount64 - menuClosedAtTick < 250 && BtnBrowse.IsMouseOver))
            {
                BrowseMenu.IsOpen = false;
                return;
            }

            OpenBrowseMenu();
        }

        private void Browse_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var altDown = (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.Alt) ||
                          (e.Key == Key.System && e.SystemKey == Key.Down);
            if (altDown)
            {
                e.Handled = true;
                OpenBrowseMenu();
            }
        }

        private void OpenBrowseMenu()
        {
            // Right-align the menu under the button
            BrowseMenu.PlacementTarget = BtnBrowse;
            BrowseMenu.Placement = PlacementMode.Bottom;
            BrowseMenu.HorizontalOffset = BtnBrowse.ActualWidth - BrowseMenu.Width;
            BrowseMenu.VerticalOffset = 6;
            BrowseMenu.IsOpen = true;
        }

        private void BrowseMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.IsBrowseMenuOpen = true;
            }

            BtnBrowse.RaiseMenuStateChanged(false);
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (BrowseMenu.IsOpen && BrowseMenu.ItemContainerGenerator.ContainerFromIndex(0) is MenuItem first)
                {
                    first.Focus();
                }
            }));
        }

        private void BrowseMenu_Closed(object sender, RoutedEventArgs e)
        {
            menuClosedAtTick = Environment.TickCount64;
            if (ViewModel != null)
            {
                ViewModel.IsBrowseMenuOpen = false;
            }

            BtnBrowse.RaiseMenuStateChanged(true);
        }

        private void BrowseMenu_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                BrowseMenu.IsOpen = false;
                BtnBrowse.Focus();
            }
        }

        private void ViewModel_BrowseFileRequested(object? sender, EventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Choose a file",
                CheckFileExists = true,
                DereferenceLinks = false
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            {
                ApplyInput(dialog.FileName);
            }
        }

        private void ViewModel_BrowseFolderRequested(object? sender, EventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Choose a folder" };

            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            {
                ApplyInput(dialog.FolderName);
            }
        }

        #endregion Browse

        #region Destination input

        private void TextboxDestination_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Enter here resolves and moves on to the name; it never creates
                e.Handled = true;
                _ = ResolveCurrentAsync(showError: true, normalize: true, focusName: true);
            }
        }

        private void TextboxDestination_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (suppressTextChanged)
            {
                return;
            }

            typingTimer.Stop();
            if (!string.IsNullOrWhiteSpace(TextboxDestination.Text))
            {
                typingTimer.Start();
            }
        }

        private void TypingTimer_Tick(object? sender, EventArgs e)
        {
            typingTimer.Stop();
            _ = ResolveCurrentAsync(showError: false, normalize: false, focusName: false);
        }

        private void TextboxDestination_LostFocus(object sender, RoutedEventArgs e)
        {
            typingTimer.Stop();
            var viewModel = ViewModel;
            if (viewModel != null && viewModel.HasDestination && viewModel.DestinationKind == null)
            {
                _ = ResolveCurrentAsync(showError: true, normalize: true, focusName: false);
            }
        }

        private void TextboxDestination_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            // A plain text box can't paste files, so a copied file replaces the whole field with its path.
            // Text replacing the whole field resolves right away; a partial paste is left to the box and the typing delay.
            var wholeField = TextboxDestination.Text.Length == 0 || TextboxDestination.SelectionLength == TextboxDestination.Text.Length;
            string? raw = null;
            if (DragDrop.TryGetFirstFile(e.DataObject, out var path))
            {
                raw = path;
            }
            else if (wholeField && DragDrop.TryGetDestination(e.DataObject, out var text))
            {
                raw = text;
            }

            if (raw != null)
            {
                e.CancelCommand();
                ApplyInput(raw);
            }
        }

        /// <summary>Replaces the destination with the value, resolves it, and on success moves focus to the name field.</summary>
        private void ApplyInput(string raw)
        {
            var viewModel = ViewModel;
            if (viewModel == null || !viewModel.IsInputEnabled)
            {
                return;
            }

            typingTimer.Stop();
            SetDestinationText(viewModel, raw);
            _ = ResolveCurrentAsync(showError: true, normalize: true, focusName: true);
        }

        private void SetDestinationText(CreatorViewModel viewModel, string text)
        {
            suppressTextChanged = true;
            try
            {
                viewModel.Destination = text;
            }
            finally
            {
                suppressTextChanged = false;
            }
        }

        private async Task ResolveCurrentAsync(bool showError, bool normalize, bool focusName)
        {
            var viewModel = ViewModel;
            if (viewModel == null || !viewModel.HasDestination)
            {
                return;
            }

            var text = viewModel.Destination;
            var version = ++resolveVersion;
            var result = await DestinationResolver.ResolveAsync(text);

            // The text changed or a newer resolve started while this one ran
            if (version != resolveVersion || viewModel.Destination != text || viewModel.IsSaving)
            {
                return;
            }

            suppressTextChanged = true;
            try
            {
                if (result.IsValid && !normalize)
                {
                    viewModel.DestinationKind = result.Kind;
                }
                else
                {
                    viewModel.ApplyResolved(result, showError);
                }
            }
            finally
            {
                suppressTextChanged = false;
            }

            if (result.IsValid && normalize)
            {
                TextboxDestination.CaretIndex = TextboxDestination.Text.Length;
                TextboxDestination.ScrollToEnd();
            }

            if (result.IsValid && focusName)
            {
                TextboxShortcut.Focus();
                Keyboard.Focus(TextboxShortcut);
                TextboxShortcut.CaretIndex = TextboxShortcut.Text.Length;
            }
        }

        #endregion Destination input

        #region DragAndDrop Handlers

        private void Creator_DragEnter(object sender, DragEventArgs e)
        {
            HandleDrag(e);
        }

        private void Creator_DragOver(object sender, DragEventArgs e)
        {
            HandleDrag(e);
        }

        private void Creator_DragLeave(object sender, DragEventArgs e)
        {
            e.Handled = true;
            var position = e.GetPosition(this);
            if (position.X < 0 || position.Y < 0 || position.X >= ActualWidth || position.Y >= ActualHeight)
            {
                SetDragHover(false);
            }
        }

        // The preview events are handled here so a text box under the cursor never inserts the dropped text at the caret
        private void HandleDrag(DragEventArgs e)
        {
            e.Handled = true;
            var accepted = ViewModel?.IsInputEnabled == true && DragDrop.HasSupportedData(e.Data);
            e.Effects = accepted ? GetDropEffect(e.AllowedEffects) : DragDropEffects.None;
            SetDragHover(accepted);
        }

        private static DragDropEffects GetDropEffect(DragDropEffects allowed)
        {
            if (allowed.HasFlag(DragDropEffects.Link))
            {
                return DragDropEffects.Link;
            }

            return allowed.HasFlag(DragDropEffects.Copy) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void SetDragHover(bool value)
        {
            if (ViewModel != null)
            {
                ViewModel.IsDragHover = value;
            }
        }

        // Several dropped files use the first one only
        private void Creator_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            SetDragHover(false);

            if (ViewModel?.IsInputEnabled != true || !DragDrop.TryGetDestination(e.Data, out var text))
            {
                return;
            }

            mainFormLogic.ActivateThisWindow();
            ApplyInput(text);
        }

        #endregion DragAndDrop Handlers
    }
}
