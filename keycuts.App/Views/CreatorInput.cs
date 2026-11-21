using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using keycuts.Common;
using keycuts.GUI.Controls;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI.Views
{
    /// <summary>
    /// What makes a view a creator form over a <see cref="CreatorViewModel"/> (WPF's CreatorInput): drop, paste, type and Browse
    /// feed the target, the target is resolved as it is typed, the name is checked as it is typed, and the view model's requests
    /// (browse, focus, confirm a name) are answered. The Creator screen and the Manager's create row each attach one.
    /// </summary>
    public sealed class CreatorInput
    {
        private static readonly TimeSpan TypingDelay = TimeSpan.FromMilliseconds(300);

        private static readonly TimeSpan NameCheckDelay = TimeSpan.FromMilliseconds(250);

        private readonly Control view;

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
        public CreatorInput(Control view, TextBox targetBox, TextBox nameBox, TextBox descriptionBox, MenuButton browseButton, ContextMenu browseMenu)
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

            view.DataContextChanged += (sender, e) => Attach(view.DataContext as CreatorViewModel);
            view.AddHandler(InputElement.KeyDownEvent, View_PreviewKeyDown, RoutingStrategies.Tunnel);
            view.AddHandler(InputElement.KeyUpEvent, View_PreviewKeyUp, RoutingStrategies.Tunnel);
            view.PropertyChanged += View_PropertyChanged;
            view.AddHandler(DragDrop.DragEnterEvent, View_Drag, handledEventsToo: true);
            view.AddHandler(DragDrop.DragOverEvent, View_Drag, handledEventsToo: true);
            view.AddHandler(DragDrop.DragLeaveEvent, View_DragLeave, handledEventsToo: true);
            view.AddHandler(DragDrop.DropEvent, View_Drop, handledEventsToo: true);

            targetBox.AddHandler(InputElement.KeyDownEvent, TargetBox_PreviewKeyDown, RoutingStrategies.Tunnel);
            targetBox.TextChanged += TargetBox_TextChanged;
            targetBox.LostFocus += TargetBox_LostFocus;
            targetBox.PropertyChanged += TargetBox_PropertyChanged;
            targetBox.AddHandler(TextBox.PastingFromClipboardEvent, TargetBox_Pasting, RoutingStrategies.Bubble);

            nameBox.TextChanged += NameBox_TextChanged;
            // Enter creates from the name and the description; Shift+Enter too, with the environment-variables setting flipped
            nameBox.AddHandler(InputElement.KeyDownEvent, Field_EnterCreates, RoutingStrategies.Tunnel);
            descriptionBox.AddHandler(InputElement.KeyDownEvent, Field_EnterCreates, RoutingStrategies.Tunnel);

            browseButton.Click += Browse_Click;
            browseButton.AddHandler(InputElement.KeyDownEvent, Browse_PreviewKeyDown, RoutingStrategies.Tunnel);
            browseMenu.Opening += (sender, e) => PlaceBrowseMenu();
            browseMenu.Opened += BrowseMenu_Opened;
            browseMenu.Closed += BrowseMenu_Closed;
            browseMenu.AddHandler(InputElement.KeyDownEvent, BrowseMenu_PreviewKeyDown, RoutingStrategies.Tunnel);

            // A view that left the screen (the Manager's row while the Creator shows) must not answer the view model's requests: a
            // Browse would open two dialogs
            view.AttachedToVisualTree += (sender, e) => Attach(view.DataContext as CreatorViewModel);
            view.DetachedFromVisualTree += (sender, e) => Attach(null);

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

        private Window? Window
        {
            get { return TopLevel.GetTopLevel(view) as Window; }
        }

        // A name that would replace a keycut or clash with another command asks first, in the app's own dialog (#153)
        private bool ConfirmName(NameCheck check, string target)
        {
            return ConfirmWindow.Ask(Window, ConfirmViewModel.ForCreate(check, target));
        }

        private void ViewModel_KeycutCreated(object? sender, EventArgs e)
        {
            Modal.Beep();
        }

        private void ViewModel_FocusRequested(object? sender, CreatorFocusTarget target)
        {
            // The name and target fields are disabled while saving; wait for the layout to re-enable them
            Dispatcher.UIThread.Post(() =>
            {
                var field = target switch
                {
                    CreatorFocusTarget.Target => targetBox,
                    CreatorFocusTarget.Description => descriptionBox,
                    _ => nameBox
                };
                field.Focus();
                if (target == CreatorFocusTarget.NameSelectAll)
                {
                    field.SelectAll();
                }
                else
                {
                    field.CaretIndex = field.Text?.Length ?? 0;
                }
            }, DispatcherPriority.Input);
        }

        private void Field_EnterCreates(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (e.KeyModifiers == KeyModifiers.None || e.KeyModifiers == KeyModifiers.Shift)
                && ViewModel?.CreateCommand.CanExecute(null) == true)
            {
                e.Handled = true;
                ViewModel.CreateCommand.Execute(null);
            }
        }

        private void NameBox_TextChanged(object? sender, TextChangedEventArgs e)
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
        private void View_PreviewKeyUp(object? sender, KeyEventArgs e)
        {
            if ((e.Key == Key.LeftShift || e.Key == Key.RightShift) && ViewModel != null)
            {
                ViewModel.IsShiftHeld = (e.KeyModifiers & KeyModifiers.Shift) != 0 && Modal.IsShiftDown();
            }
        }

        // Shift pressed or released in another window never reaches us, so read it again whenever focus comes or goes:
        // holding Shift elsewhere and then clicking Create must still flip the setting
        private void View_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == InputElement.IsKeyboardFocusWithinProperty && ViewModel != null)
            {
                ViewModel.IsShiftHeld = view.IsKeyboardFocusWithin && Modal.IsShiftDown();
            }
        }

        /// <summary>Ctrl+V anywhere in the view except its text fields pastes into the target.</summary>
        private void View_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if ((e.Key == Key.LeftShift || e.Key == Key.RightShift) && ViewModel != null)
            {
                ViewModel.IsShiftHeld = true;
                return;
            }

            if (e.Key != Key.V || e.KeyModifiers != PlatformKeys.Command || ViewModel?.IsInputEnabled != true)
            {
                return;
            }

            var topLevel = TopLevel.GetTopLevel(view);
            if (topLevel?.FocusManager?.GetFocusedElement() is TextBox || topLevel?.Clipboard is not { } clipboard)
            {
                return;
            }

            e.Handled = true;
            _ = PasteAsync(clipboard);
        }

        private async Task PasteAsync(Avalonia.Input.Platform.IClipboard clipboard)
        {
            var text = await TargetData.GetTargetAsync(await clipboard.TryGetDataAsync());
            if (text != null)
            {
                ApplyInput(text);
            }
        }

        #region Browse

        private void Browse_Click(object? sender, RoutedEventArgs e)
        {
            // Clicking the button while the menu is open first closes the menu (click outside); don't reopen it right away
            if (browseMenu.IsOpen || (Environment.TickCount64 - menuClosedAtTick < 250 && browseButton.IsPointerOver))
            {
                browseMenu.Close();
                return;
            }

            OpenBrowseMenu();
        }

        private void Browse_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && e.KeyModifiers == KeyModifiers.Alt)
            {
                e.Handled = true;
                OpenBrowseMenu();
            }
        }

        // Under the button, right edges lined up, whichever side the system drops menus from
        private void PlaceBrowseMenu()
        {
            browseMenu.PlacementTarget = browseButton;
            MenuPlacement.UnderRightEdge(browseMenu, 6);
        }

        private void OpenBrowseMenu()
        {
            PlaceBrowseMenu();
            browseMenu.Open(browseButton);
        }

        private void BrowseMenu_Opened(object? sender, RoutedEventArgs e)
        {
            browseButton.RaiseMenuStateChanged(false);
            Dispatcher.UIThread.Post(() =>
            {
                if (browseMenu.IsOpen && browseMenu.Items.OfType<MenuItem>().FirstOrDefault() is { } first)
                {
                    first.Focus();
                }
            }, DispatcherPriority.Input);
        }

        private void BrowseMenu_Closed(object? sender, RoutedEventArgs e)
        {
            menuClosedAtTick = Environment.TickCount64;
            browseButton.RaiseMenuStateChanged(true);
        }

        private void BrowseMenu_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                browseMenu.Close();
                browseButton.Focus();
            }
        }

        private async void ViewModel_BrowseFileRequested(object? sender, EventArgs e)
        {
            if (Window?.StorageProvider is not { } storage)
            {
                return;
            }

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Choose a file", AllowMultiple = false });
            if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
            {
                ApplyInput(path);
            }
        }

        private async void ViewModel_BrowseFolderRequested(object? sender, EventArgs e)
        {
            if (Window?.StorageProvider is not { } storage)
            {
                return;
            }

            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose a folder", AllowMultiple = false });
            if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path)
            {
                ApplyInput(path);
            }
        }

        #endregion Browse

        #region Target input

        private void TargetBox_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Enter here resolves and moves on to the name; it creates only where TargetEnterCreates says so
                e.Handled = true;
                _ = ResolveCurrentAsync(showError: true, normalize: true, focusName: true, create: TargetEnterCreates);
            }
        }

        private void TargetBox_TextChanged(object? sender, TextChangedEventArgs e)
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
        private void TargetBox_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            var current = ViewModel;
            if (e.Property != InputElement.IsKeyboardFocusWithinProperty || current == null)
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
                targetBox.CaretIndex = targetBox.Text?.Length ?? 0;
            }
        }

        private void TargetBox_LostFocus(object? sender, RoutedEventArgs e)
        {
            typingTimer.Stop();
            var current = ViewModel;
            if (current != null && current.HasTarget && current.TargetKind == null)
            {
                _ = ResolveCurrentAsync(showError: true, normalize: true, focusName: false);
            }
        }

        // A plain text box can't paste files, so a copied file replaces the whole field with its path. Text replacing the whole
        // field resolves right away; a partial paste is the box's own. The clipboard is read asynchronously, so the box's paste is
        // stopped first and done here when it is the box's.
        private void TargetBox_Pasting(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(targetBox)?.Clipboard is not { } clipboard)
            {
                return;
            }

            e.Handled = true;
            var length = targetBox.Text?.Length ?? 0;
            var wholeField = length == 0 || Math.Abs(targetBox.SelectionEnd - targetBox.SelectionStart) == length;
            _ = PasteIntoTargetAsync(clipboard, wholeField);
        }

        private async Task PasteIntoTargetAsync(Avalonia.Input.Platform.IClipboard clipboard, bool wholeField)
        {
            var data = await clipboard.TryGetDataAsync();
            if (await TargetData.GetFirstFileAsync(data) is { } path)
            {
                ApplyInput(path);
                return;
            }

            if (wholeField && await TargetData.GetTargetAsync(data) is { } target)
            {
                ApplyInput(target);
                return;
            }

            if (data != null && await data.TryGetTextAsync() is { } text)
            {
                targetBox.SelectedText = text;
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
                targetBox.CaretIndex = targetBox.Text?.Length ?? 0;
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
                nameBox.CaretIndex = nameBox.Text?.Length ?? 0;
            }
        }

        #endregion Target input

        #region DragAndDrop Handlers

        // Handled here so a text box under the cursor never inserts the dropped text at the caret
        private void View_Drag(object? sender, DragEventArgs e)
        {
            e.Handled = true;
            var accepted = ViewModel?.IsInputEnabled == true && TargetData.HasSupportedData(e.DataTransfer);
            e.DragEffects = accepted ? GetDropEffect(e.DragEffects) : DragDropEffects.None;
            SetDragHover(accepted);
        }

        private void View_DragLeave(object? sender, DragEventArgs e)
        {
            e.Handled = true;
            var position = e.GetPosition(view);
            if (position.X < 0 || position.Y < 0 || position.X >= view.Bounds.Width || position.Y >= view.Bounds.Height)
            {
                SetDragHover(false);
            }
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
        private void View_Drop(object? sender, DragEventArgs e)
        {
            e.Handled = true;
            SetDragHover(false);

            if (ViewModel?.IsInputEnabled != true || !TargetData.TryGetTarget(e.DataTransfer, out var text))
            {
                return;
            }

            Window?.Activate();
            ApplyInput(text);
        }

        #endregion DragAndDrop Handlers
    }
}
