using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI
{
    /// <summary>
    /// The modal Edit form (#180; WPF's EditWindow): a keycut's target, name and description. Save writes the file and closes with a
    /// true result; it stays open, with the problem beside its field, when the save fails. Cancel and Escape close without saving.
    /// </summary>
    public partial class EditWindow : Window
    {
        private readonly EditKeycutViewModel? viewModel;

        /// <summary>For the XAML previewer only.</summary>
        public EditWindow()
        {
            InitializeComponent();
        }

        public EditWindow(EditKeycutViewModel viewModel)
            : this()
        {
            this.viewModel = viewModel;
            DataContext = viewModel;

            viewModel.CloseRequested += ViewModel_CloseRequested;
            viewModel.FocusRequested += ViewModel_FocusRequested;
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            Opened += (sender, e) =>
            {
                Modal.FitToWorkArea(this);
                // The target is the usual reason to open the form
                TextboxTarget.Focus();
                TextboxTarget.SelectAll();
            };
            Closing += Edit_Closing;
            Closed += (sender, e) =>
            {
                viewModel.CloseRequested -= ViewModel_CloseRequested;
                viewModel.FocusRequested -= ViewModel_FocusRequested;
                viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            };
        }

        /// <summary>Shows the form over <paramref name="owner"/> and returns true if it was closed by a save (or with nothing to save).</summary>
        public static bool Show(Window? owner, EditKeycutViewModel viewModel)
        {
            return Modal.Show<bool>(new EditWindow(viewModel), owner);
        }

        private void ViewModel_CloseRequested(object? sender, bool saved)
        {
            Close(saved);
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }

        // The fields are off while saving; wait for the layout to turn them back on before focusing one
        private void ViewModel_FocusRequested(object? sender, EditFocusTarget target)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var field = target == EditFocusTarget.Target ? TextboxTarget : TextboxName;
                field.Focus();
                field.SelectAll();
            }, DispatcherPriority.Input);
        }

        // A message can sit below the visible part of a height-capped window: bring it into view (its text change is announced)
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Control? line = e.PropertyName switch
            {
                nameof(EditKeycutViewModel.TargetError) when viewModel!.HasTargetError => TargetErrorText,
                nameof(EditKeycutViewModel.NameError) when viewModel!.HasNameError => NameErrorText,
                nameof(EditKeycutViewModel.SaveError) when viewModel!.HasSaveError => SaveErrorText,
                _ => null
            };

            if (line != null)
            {
                Dispatcher.UIThread.Post(() => line.BringIntoView(), DispatcherPriority.Background);
            }
        }

        // The close button and Alt+F4 would hand control back to the manager while the save is still running. A save that works
        // closes the window after IsSaving is back to false, so this never blocks that.
        private void Edit_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (viewModel?.IsSaving == true)
            {
                e.Cancel = true;
            }
        }
    }
}
