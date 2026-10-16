using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using keycuts.Common;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI.Views
{
    /// <summary>
    /// Creator view. It still carries the legacy create and drag-and-drop behavior until KC-10 and KC-11 rework them.
    /// </summary>
    public partial class CreatorView : UserControl
    {
        private readonly MainFormLogic mainFormLogic = new MainFormLogic();

        private readonly CommonLogic commonLogic = new CommonLogic();

        public CreatorView()
        {
            InitializeComponent();
        }

        private CreatorViewModel? ViewModel
        {
            get { return DataContext as CreatorViewModel; }
        }

        private void CreatorView_Loaded(object sender, RoutedEventArgs e)
        {
            var nameFirst = ViewModel?.FocusNameFieldOnShow == true;
            var target = nameFirst ? TextboxShortcut : TextboxDestination;
            target.Focus();
            Keyboard.Focus(target);
        }

        private void CreatorView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (ViewModel?.CanCreate == true)
                {
                    CreateShortcut();
                }
            }
        }

        private void CreateShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.CanCreate == true)
            {
                CreateShortcut();
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            // Right-align the menu under the button; KC-10 adds the file and folder pickers
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
        }

        private void BrowseMenu_Closed(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.IsBrowseMenuOpen = false;
            }
        }

        private void CreateShortcut()
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var owner = Window.GetWindow(this);
            var destination = viewModel.Destination;
            var shortcutName = viewModel.ShortcutName;

            viewModel.IsSaving = true;
            UpdateLayout();

            var result = commonLogic.CreateShortcut(destination, shortcutName);

            if (result == ExitCode.FileAlreadyExists)
            {
                var dialogResult = MessageBox.Show(owner,
                    "Shortcut file already exists. Overwrite it?",
                    "File already exists", MessageBoxButton.YesNo);

                if (dialogResult == MessageBoxResult.Yes)
                {
                    result = commonLogic.CreateShortcut(destination, shortcutName, true);
                }
            }

            System.Threading.Thread.Sleep(400);
            System.Media.SystemSounds.Beep.Play();
            viewModel.IsSaving = false;

            if (result == ExitCode.Success)
            {
                viewModel.Clear();
                viewModel.NotifyKeycutCreated();
                TextboxDestination.Focus();
            }
            else if (result == ExitCode.FileAlreadyExists)
            {
                // User chose not to overwrite; nothing went wrong
                TextboxShortcut.Focus();
            }
            else
            {
                TextboxShortcut.Focus();
                MessageBox.Show(owner, $"Error: {result}\nDestination: {destination}\nShortcut: {shortcutName}");
            }
        }

        #region DragAndDrop Handlers

        private void TextboxDestination_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.Destination = string.Empty;
            }

            e.Handled = true;
        }

        private void Creator_DragEnter(object sender, DragEventArgs e)
        {
            DragDrop.DragAndEnter(sender, e);
            SetDragHover(true);
        }

        private void Creator_DragLeave(object sender, DragEventArgs e)
        {
            SetDragHover(false);
        }

        private void SetDragHover(bool value)
        {
            if (ViewModel != null)
            {
                ViewModel.IsDragHover = value;
            }
        }

        private void Creator_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            SetDragHover(false);

            var file = DragDrop.GetDroppedFiles(sender, e).FirstOrDefault();
            if (file == null || ViewModel == null)
            {
                MessageBox.Show(Window.GetWindow(this), "Sorry, not sure what to do with this type of file.", "Oops");
            }
            else
            {
                mainFormLogic.HandleNewDestination(ViewModel, file);
                TextboxShortcut.Focus();
            }
        }

        #endregion DragAndDrop Handlers
    }
}
