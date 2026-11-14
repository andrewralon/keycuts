using System;
using System.ComponentModel;
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
    /// Creator view: drop, paste, type and Browse feed the target; Enter in the name field or the Create button submits.
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
            DataObject.AddPastingHandler(TextboxTarget, TextboxTarget_Pasting);
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
                oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                oldViewModel.ConfirmName = null;

                if (oldViewModel.Welcome != null)
                {
                    oldViewModel.Welcome.PropertyChanged -= Welcome_PropertyChanged;
                }
            }

            if (e.NewValue is CreatorViewModel newViewModel)
            {
                newViewModel.BrowseFileRequested += ViewModel_BrowseFileRequested;
                newViewModel.BrowseFolderRequested += ViewModel_BrowseFolderRequested;
                newViewModel.FocusRequested += ViewModel_FocusRequested;
                newViewModel.KeycutCreated += ViewModel_KeycutCreated;
                newViewModel.PropertyChanged += ViewModel_PropertyChanged;
                newViewModel.ConfirmName = ConfirmName;

                if (newViewModel.Welcome != null)
                {
                    newViewModel.Welcome.PropertyChanged += Welcome_PropertyChanged;
                }
            }
        }

        // Tell screen readers when the helper line (name warnings and errors) or the status by the Name label (Saving, Created) changes
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CreatorViewModel.HelperText) && ViewModel?.ShowHelperLine == true)
            {
                LiveRegion.Announce(HelperTextBlock);
            }
            else if (e.PropertyName == nameof(CreatorViewModel.NameStatusText) && ViewModel?.HasNameStatus == true)
            {
                LiveRegion.Announce(NameStatusTextBlock);
            }
        }

        // The welcome panel's helper line (name errors, Saving, how to use the new keycut, the PATH warning)
        private void Welcome_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WelcomeViewModel.HelperText))
            {
                LiveRegion.Announce(WelcomeHelperTextBlock);
            }
        }

        // A name that would replace a keycut or clash with another command asks first, in the app's own dialog (#153)
        private bool ConfirmName(NameCheck check, string target)
        {
            return ConfirmWindow.Ask(Window.GetWindow(this), ConfirmViewModel.ForCreate(check, target));
        }

        private void ViewModel_KeycutCreated(object? sender, EventArgs e)
        {
            System.Media.SystemSounds.Beep.Play();
        }

        private void ViewModel_FocusRequested(object? sender, CreatorFocusTarget target)
        {
            // The name and target fields are disabled while saving; wait for the layout to re-enable them
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                var field = target switch
                {
                    CreatorFocusTarget.Target => TextboxTarget,
                    CreatorFocusTarget.Description => TextboxDescription,
                    _ => TextboxShortcut
                };
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
                _ = ViewModel.CheckNameAsync();
            }
        }

        private void CreatorView_Loaded(object sender, RoutedEventArgs e)
        {
            // Arrowing through the tabs (#218) keeps the focus there
            if (Controls.RailTabControl.HoldsKeyboardFocus())
            {
                return;
            }

            var nameFirst = ViewModel?.FocusNameFieldOnShow == true;
            var target = nameFirst ? TextboxShortcut : TextboxTarget;
            target.Focus();
            Keyboard.Focus(target);
        }

        // Holding Shift flips the environment-variables setting for this keycut (#31); the Target shows the path that way
        private void CreatorView_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.LeftShift || e.Key == Key.RightShift) && ViewModel != null)
            {
                ViewModel.IsShiftHeld = IsShiftDown();
            }
        }

        // Shift pressed or released in another window never reaches us, so read it again whenever focus comes or goes:
        // holding Shift elsewhere and then clicking Create must still flip the setting
        private void CreatorView_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.IsShiftHeld = IsKeyboardFocusWithin && IsShiftDown();
            }
        }

        private static bool IsShiftDown()
        {
            return Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
        }

        /// <summary>Ctrl+V anywhere except the two text fields pastes into the target.</summary>
        private void CreatorView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.LeftShift || e.Key == Key.RightShift) && ViewModel != null)
            {
                ViewModel.IsShiftHeld = true;
                return;
            }

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

            if (DragDrop.TryGetTarget(data, out var text))
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

        // Shift+F10 and the Apps key open the menu through the context menu service; place it like the click path does
        private void Browse_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            PlaceBrowseMenu();
        }

        private void PlaceBrowseMenu()
        {
            // Right-align the menu under the button
            BrowseMenu.PlacementTarget = BtnBrowse;
            BrowseMenu.Placement = PlacementMode.Bottom;
            BrowseMenu.HorizontalOffset = BtnBrowse.ActualWidth - BrowseMenu.Width;
            BrowseMenu.VerticalOffset = 6;
        }

        private void OpenBrowseMenu()
        {
            PlaceBrowseMenu();
            BrowseMenu.IsOpen = true;
        }

        private void BrowseMenu_Opened(object sender, RoutedEventArgs e)
        {
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

        #region Target input

        private void TextboxTarget_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Enter here resolves and moves on to the name; it never creates
                e.Handled = true;
                _ = ResolveCurrentAsync(showError: true, normalize: true, focusName: true);
            }
        }

        private void TextboxTarget_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (suppressTextChanged)
            {
                return;
            }

            typingTimer.Stop();
            if (!string.IsNullOrWhiteSpace(TextboxTarget.Text))
            {
                typingTimer.Start();
            }
        }

        private void TypingTimer_Tick(object? sender, EventArgs e)
        {
            typingTimer.Stop();
            _ = ResolveCurrentAsync(showError: false, normalize: false, focusName: false);
        }

        // While the box is being edited it holds the real path; otherwise it may show the %VAR% form of it (CreatorViewModel.TargetText).
        // The text changes under the caret here, which isn't typing, so it must not start the resolve timer.
        private void TextboxTarget_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var wasSuppressed = suppressTextChanged;
            suppressTextChanged = true;
            try
            {
                viewModel.IsTargetEditing = TextboxTarget.IsKeyboardFocusWithin;
            }
            finally
            {
                suppressTextChanged = wasSuppressed;
            }

            if (TextboxTarget.IsKeyboardFocusWithin)
            {
                TextboxTarget.CaretIndex = TextboxTarget.Text.Length;
            }
        }

        private void TextboxTarget_LostFocus(object sender, RoutedEventArgs e)
        {
            typingTimer.Stop();
            var viewModel = ViewModel;
            if (viewModel != null && viewModel.HasTarget && viewModel.TargetKind == null)
            {
                _ = ResolveCurrentAsync(showError: true, normalize: true, focusName: false);
            }
        }

        private void TextboxTarget_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            // A plain text box can't paste files, so a copied file replaces the whole field with its path.
            // Text replacing the whole field resolves right away; a partial paste is left to the box and the typing delay.
            var wholeField = TextboxTarget.Text.Length == 0 || TextboxTarget.SelectionLength == TextboxTarget.Text.Length;
            string? raw = null;
            if (DragDrop.TryGetFirstFile(e.DataObject, out var path))
            {
                raw = path;
            }
            else if (wholeField && DragDrop.TryGetTarget(e.DataObject, out var text))
            {
                raw = text;
            }

            if (raw != null)
            {
                e.CancelCommand();
                ApplyInput(raw);
            }
        }

        /// <summary>Replaces the target with the value, resolves it, and on success moves focus to the name field.</summary>
        private void ApplyInput(string raw)
        {
            var viewModel = ViewModel;
            if (viewModel == null || !viewModel.IsInputEnabled)
            {
                return;
            }

            typingTimer.Stop();
            SetTargetText(viewModel, raw);
            _ = ResolveCurrentAsync(showError: true, normalize: true, focusName: true);
        }

        private void SetTargetText(CreatorViewModel viewModel, string text)
        {
            suppressTextChanged = true;
            try
            {
                viewModel.Target = text;
            }
            finally
            {
                suppressTextChanged = false;
            }
        }

        private async Task ResolveCurrentAsync(bool showError, bool normalize, bool focusName)
        {
            var viewModel = ViewModel;
            if (viewModel == null || !viewModel.HasTarget)
            {
                return;
            }

            var text = viewModel.Target;
            var version = ++resolveVersion;
            var result = await TargetResolver.ResolveAsync(text);

            // The text changed or a newer resolve started while this one ran
            if (version != resolveVersion || viewModel.Target != text || viewModel.IsSaving)
            {
                return;
            }

            suppressTextChanged = true;
            try
            {
                if (result.IsValid && !normalize)
                {
                    // Keep what was typed, but preview what will be saved (a shortcut's target, a normalized path)
                    viewModel.TargetKind = result.Kind;
                    viewModel.ResolvedTarget = result.Target;
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
                TextboxTarget.CaretIndex = TextboxTarget.Text.Length;
                TextboxTarget.ScrollToEnd();
            }

            if (result.IsValid && focusName)
            {
                TextboxShortcut.Focus();
                Keyboard.Focus(TextboxShortcut);
                TextboxShortcut.CaretIndex = TextboxShortcut.Text.Length;
            }
        }

        #endregion Target input

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

            if (ViewModel?.IsInputEnabled != true || !DragDrop.TryGetTarget(e.Data, out var text))
            {
                return;
            }

            mainFormLogic.ActivateThisWindow();
            ApplyInput(text);
        }

        #endregion DragAndDrop Handlers
    }
}
