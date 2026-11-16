using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using keycuts.Common;
using keycuts.GUI.Controls;
using keycuts.GUI.ViewModels;
using Microsoft.Win32;

namespace keycuts.GUI.Views
{
    /// <summary>
    /// What makes a view a creator form over a <see cref="CreatorViewModel"/>: drop, paste, type and Browse feed the target, the
    /// target is resolved as it is typed, the name is checked as it is typed, and the view model's requests (browse, focus,
    /// confirm a name) are answered. The Creator screen and the Manager's create row each attach one to their own controls.
    /// </summary>
    public sealed class CreatorInput
    {
        private static readonly TimeSpan TypingDelay = TimeSpan.FromMilliseconds(300);

        private static readonly TimeSpan NameCheckDelay = TimeSpan.FromMilliseconds(250);

        private readonly MainFormLogic mainFormLogic = new MainFormLogic();

        private readonly FrameworkElement view;

        private readonly TextBox targetBox;

        private readonly TextBox nameBox;

        private readonly TextBox descriptionBox;

        private readonly MenuButton browseButton;

        private readonly ContextMenu browseMenu;

        private readonly DispatcherTimer typingTimer;

        private readonly DispatcherTimer nameTimer;

        private CreatorViewModel? viewModel;

        private int resolveVersion;

        private bool suppressTextChanged;

        private long menuClosedAtTick;

        /// <param name="view">The view: the whole of it takes drops and Ctrl+V, and tracks Shift.</param>
        /// <param name="targetBox">The target's text box.</param>
        /// <param name="nameBox">The name's text box.</param>
        /// <param name="descriptionBox">The description's text box.</param>
        /// <param name="browseButton">The Browse button, which opens <paramref name="browseMenu"/>.</param>
        /// <param name="browseMenu">Browse's menu: File… and Folder….</param>
        public CreatorInput(FrameworkElement view, TextBox targetBox, TextBox nameBox, TextBox descriptionBox, MenuButton browseButton, ContextMenu browseMenu)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.targetBox = targetBox ?? throw new ArgumentNullException(nameof(targetBox));
            this.nameBox = nameBox ?? throw new ArgumentNullException(nameof(nameBox));
            this.descriptionBox = descriptionBox ?? throw new ArgumentNullException(nameof(descriptionBox));
            this.browseButton = browseButton ?? throw new ArgumentNullException(nameof(browseButton));
            this.browseMenu = browseMenu ?? throw new ArgumentNullException(nameof(browseMenu));

            typingTimer = new DispatcherTimer { Interval = TypingDelay };
            typingTimer.Tick += TypingTimer_Tick;
            nameTimer = new DispatcherTimer { Interval = NameCheckDelay };
            nameTimer.Tick += NameTimer_Tick;

            view.DataContextChanged += View_DataContextChanged;
            view.PreviewKeyDown += View_PreviewKeyDown;
            view.PreviewKeyUp += View_PreviewKeyUp;
            view.IsKeyboardFocusWithinChanged += View_IsKeyboardFocusWithinChanged;
            view.PreviewDragEnter += View_DragEnter;
            view.PreviewDragOver += View_DragOver;
            view.PreviewDragLeave += View_DragLeave;
            view.PreviewDrop += View_Drop;

            targetBox.PreviewKeyDown += TargetBox_PreviewKeyDown;
            targetBox.TextChanged += TargetBox_TextChanged;
            targetBox.LostFocus += TargetBox_LostFocus;
            targetBox.IsKeyboardFocusWithinChanged += TargetBox_IsKeyboardFocusWithinChanged;
            DataObject.AddPastingHandler(targetBox, TargetBox_Pasting);

            nameBox.TextChanged += NameBox_TextChanged;

            browseButton.Click += Browse_Click;
            browseButton.PreviewKeyDown += Browse_PreviewKeyDown;
            browseButton.ContextMenuOpening += Browse_ContextMenuOpening;
            browseMenu.Opened += BrowseMenu_Opened;
            browseMenu.Closed += BrowseMenu_Closed;
            browseMenu.PreviewKeyDown += BrowseMenu_PreviewKeyDown;

            // A view that left the screen (the Manager's row while the Creator shows) must not answer the view model's requests: a
            // Browse would open two dialogs
            view.Loaded += (sender, e) => Attach(view.DataContext as CreatorViewModel);
            view.Unloaded += (sender, e) => Attach(null);

            Attach(view.DataContext as CreatorViewModel);
        }

        /// <summary>
        /// Enter in the target resolves it and then creates the keycut when the name is filled, or else moves on to the name (the
        /// Manager's create row). On the Creator, Enter there never creates: it only moves on to the name.
        /// </summary>
        public bool TargetEnterCreates { get; init; }

        private CreatorViewModel? ViewModel
        {
            get { return viewModel; }
        }

        private void View_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Attach(e.NewValue as CreatorViewModel);
        }

        private void Attach(CreatorViewModel? newViewModel)
        {
            if (ReferenceEquals(viewModel, newViewModel))
            {
                return;
            }

            if (viewModel != null)
            {
                viewModel.BrowseFileRequested -= ViewModel_BrowseFileRequested;
                viewModel.BrowseFolderRequested -= ViewModel_BrowseFolderRequested;
                viewModel.FocusRequested -= ViewModel_FocusRequested;
                viewModel.KeycutCreated -= ViewModel_KeycutCreated;
                viewModel.ConfirmName = null;
            }

            viewModel = newViewModel;

            if (viewModel != null)
            {
                viewModel.BrowseFileRequested += ViewModel_BrowseFileRequested;
                viewModel.BrowseFolderRequested += ViewModel_BrowseFolderRequested;
                viewModel.FocusRequested += ViewModel_FocusRequested;
                viewModel.KeycutCreated += ViewModel_KeycutCreated;
                viewModel.ConfirmName = ConfirmName;
            }
        }

        // A name that would replace a keycut or clash with another command asks first, in the app's own dialog (#153)
        private bool ConfirmName(NameCheck check, string target)
        {
            return ConfirmWindow.Ask(Window.GetWindow(view), ConfirmViewModel.ForCreate(check, target));
        }

        private void ViewModel_KeycutCreated(object? sender, EventArgs e)
        {
            System.Media.SystemSounds.Beep.Play();
        }

        private void ViewModel_FocusRequested(object? sender, CreatorFocusTarget target)
        {
            // The name and target fields are disabled while saving; wait for the layout to re-enable them
            view.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                var field = target switch
                {
                    CreatorFocusTarget.Target => targetBox,
                    CreatorFocusTarget.Description => descriptionBox,
                    _ => nameBox
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

        private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            nameTimer.Stop();
            if (!string.IsNullOrWhiteSpace(nameBox.Text))
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

        // Holding Shift flips the environment-variables setting for this keycut (#31); the Target shows the path that way
        private void View_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.LeftShift || e.Key == Key.RightShift) && ViewModel != null)
            {
                ViewModel.IsShiftHeld = IsShiftDown();
            }
        }

        // Shift pressed or released in another window never reaches us, so read it again whenever focus comes or goes:
        // holding Shift elsewhere and then clicking Create must still flip the setting
        private void View_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.IsShiftHeld = view.IsKeyboardFocusWithin && IsShiftDown();
            }
        }

        private static bool IsShiftDown()
        {
            return Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
        }

        /// <summary>Ctrl+V anywhere in the view except its text fields pastes into the target.</summary>
        private void View_PreviewKeyDown(object sender, KeyEventArgs e)
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
            if (browseMenu.IsOpen || (Environment.TickCount64 - menuClosedAtTick < 250 && browseButton.IsMouseOver))
            {
                browseMenu.IsOpen = false;
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
            // Under the button, right edges lined up, whichever side Windows drops menus from
            browseMenu.PlacementTarget = browseButton;
            browseMenu.Placement = PlacementMode.Custom;
            browseMenu.CustomPopupPlacementCallback = MenuPlacement.UnderRightEdge;
            browseMenu.HorizontalOffset = 0;
            browseMenu.VerticalOffset = 6;
        }

        private void OpenBrowseMenu()
        {
            PlaceBrowseMenu();
            browseMenu.IsOpen = true;
        }

        private void BrowseMenu_Opened(object sender, RoutedEventArgs e)
        {
            browseButton.RaiseMenuStateChanged(false);
            view.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (browseMenu.IsOpen && browseMenu.ItemContainerGenerator.ContainerFromIndex(0) is MenuItem first)
                {
                    first.Focus();
                }
            }));
        }

        private void BrowseMenu_Closed(object sender, RoutedEventArgs e)
        {
            menuClosedAtTick = Environment.TickCount64;

            browseButton.RaiseMenuStateChanged(true);
        }

        private void BrowseMenu_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                browseMenu.IsOpen = false;
                browseButton.Focus();
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

            if (dialog.ShowDialog(Window.GetWindow(view)) == true)
            {
                ApplyInput(dialog.FileName);
            }
        }

        private void ViewModel_BrowseFolderRequested(object? sender, EventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Choose a folder" };

            if (dialog.ShowDialog(Window.GetWindow(view)) == true)
            {
                ApplyInput(dialog.FolderName);
            }
        }

        #endregion Browse

        #region Target input

        private void TargetBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Enter here resolves and moves on to the name; it creates only where TargetEnterCreates says so
                e.Handled = true;
                _ = ResolveCurrentAsync(showError: true, normalize: true, focusName: true, create: TargetEnterCreates);
            }
        }

        private void TargetBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (suppressTextChanged)
            {
                return;
            }

            typingTimer.Stop();
            if (!string.IsNullOrWhiteSpace(targetBox.Text))
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
        private void TargetBox_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var current = ViewModel;
            if (current == null)
            {
                return;
            }

            var wasSuppressed = suppressTextChanged;
            suppressTextChanged = true;
            try
            {
                current.IsTargetEditing = targetBox.IsKeyboardFocusWithin;
            }
            finally
            {
                suppressTextChanged = wasSuppressed;
            }

            if (targetBox.IsKeyboardFocusWithin)
            {
                targetBox.CaretIndex = targetBox.Text.Length;
            }
        }

        private void TargetBox_LostFocus(object sender, RoutedEventArgs e)
        {
            typingTimer.Stop();
            var current = ViewModel;
            if (current != null && current.HasTarget && current.TargetKind == null)
            {
                _ = ResolveCurrentAsync(showError: true, normalize: true, focusName: false);
            }
        }

        private void TargetBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            // A plain text box can't paste files, so a copied file replaces the whole field with its path.
            // Text replacing the whole field resolves right away; a partial paste is left to the box and the typing delay.
            var wholeField = targetBox.Text.Length == 0 || targetBox.SelectionLength == targetBox.Text.Length;
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
            var current = ViewModel;
            if (current == null || !current.IsInputEnabled)
            {
                return;
            }

            typingTimer.Stop();
            SetTargetText(current, raw);
            _ = ResolveCurrentAsync(showError: true, normalize: true, focusName: true);
        }

        private void SetTargetText(CreatorViewModel current, string text)
        {
            suppressTextChanged = true;
            try
            {
                current.Target = text;
            }
            finally
            {
                suppressTextChanged = false;
            }
        }

        private async Task ResolveCurrentAsync(bool showError, bool normalize, bool focusName, bool create = false)
        {
            var current = ViewModel;
            if (current == null || !current.HasTarget)
            {
                return;
            }

            var text = current.Target;
            var version = ++resolveVersion;
            var result = await TargetResolver.ResolveAsync(text);

            // The text changed or a newer resolve started while this one ran
            if (version != resolveVersion || current.Target != text || current.IsSaving)
            {
                return;
            }

            suppressTextChanged = true;
            try
            {
                if (result.IsValid && !normalize)
                {
                    // Keep what was typed, but preview what will be saved (a shortcut's target, a normalized path)
                    current.TargetKind = result.Kind;
                    current.ResolvedTarget = result.Target;
                }
                else
                {
                    current.ApplyResolved(result, showError);
                }
            }
            finally
            {
                suppressTextChanged = false;
            }

            if (result.IsValid && normalize)
            {
                targetBox.CaretIndex = targetBox.Text.Length;
                targetBox.ScrollToEnd();
            }

            // The Manager's row: a name already typed means the form is done, so Enter makes the keycut (Shift as read then)
            if (result.IsValid && create && current.CreateCommand.CanExecute(null))
            {
                await current.CreateCommand.ExecuteAsync();
                return;
            }

            if (result.IsValid && focusName)
            {
                nameBox.Focus();
                Keyboard.Focus(nameBox);
                nameBox.CaretIndex = nameBox.Text.Length;
            }
        }

        #endregion Target input

        #region DragAndDrop Handlers

        private void View_DragEnter(object sender, DragEventArgs e)
        {
            HandleDrag(e);
        }

        private void View_DragOver(object sender, DragEventArgs e)
        {
            HandleDrag(e);
        }

        private void View_DragLeave(object sender, DragEventArgs e)
        {
            e.Handled = true;
            var position = e.GetPosition(view);
            if (position.X < 0 || position.Y < 0 || position.X >= view.ActualWidth || position.Y >= view.ActualHeight)
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
        private void View_Drop(object sender, DragEventArgs e)
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
