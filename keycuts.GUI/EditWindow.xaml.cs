using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Threading;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI
{
    /// <summary>
    /// The modal Edit form (#180): a keycut's target, name and description. Save writes the file and closes with a true
    /// result; it stays open, with the problem beside its field, when the save fails. Cancel and Escape close without saving.
    /// It looks like the Settings window and the confirmation: the same surface, heading, footer strip and buttons.
    /// </summary>
    public partial class EditWindow : Window
    {
        private readonly EditKeycutViewModel viewModel;

        public EditWindow(EditKeycutViewModel viewModel)
        {
            InitializeComponent();
            this.viewModel = viewModel;
            DataContext = viewModel;

            viewModel.CloseRequested += ViewModel_CloseRequested;
            viewModel.FocusRequested += ViewModel_FocusRequested;
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        /// <summary>Shows the form over <paramref name="owner"/> and returns true if it was closed by a save (or with nothing to save).</summary>
        public static bool Show(Window? owner, EditKeycutViewModel viewModel)
        {
            var window = new EditWindow(viewModel);

            if (owner != null && owner.IsLoaded)
            {
                window.Owner = owner;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            return window.ShowDialog() == true;
        }

        private void ViewModel_CloseRequested(object? sender, bool saved)
        {
            try
            {
                DialogResult = saved;
            }
            catch (InvalidOperationException)
            {
                // Not shown as a dialog (a test hosts it with Show)
                Close();
            }
        }

        // The fields are off while saving; wait for the layout to turn them back on before focusing one
        private void ViewModel_FocusRequested(object? sender, EditFocusTarget target)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                var field = target == EditFocusTarget.Target ? TextboxTarget : TextboxName;
                field.Focus();
                Keyboard.Focus(field);
                field.SelectAll();
            }));
        }

        // The LiveSetting on a text block alone doesn't raise the event; the peer has to, after the bindings update
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EditKeycutViewModel.TargetError) && viewModel.HasTargetError)
            {
                AnnounceLiveRegion(TargetErrorText);
            }
            else if (e.PropertyName == nameof(EditKeycutViewModel.NameError) && viewModel.HasNameError)
            {
                AnnounceLiveRegion(NameErrorText);
            }
            else if (e.PropertyName == nameof(EditKeycutViewModel.SaveError) && viewModel.HasSaveError)
            {
                AnnounceLiveRegion(SaveErrorText);
            }
        }

        // Also scrolls the message into view: when the window is height-capped, a message can sit below the visible part
        private void AnnounceLiveRegion(FrameworkElement element)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                element.BringIntoView();

                if (UIElementAutomationPeer.CreatePeerForElement(element) is AutomationPeer peer)
                {
                    peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
                }
            }));
        }

        // As Settings and the confirmation do: cap the height to the screen so the footer stays reachable, and keep the window on it
        private void FitToWorkArea()
        {
            var workArea = WindowLayout.GetWorkAreaInDips(Owner ?? this);
            MaxHeight = workArea.Height;

            if (WindowStartupLocation == WindowStartupLocation.CenterOwner)
            {
                UpdateLayout();
                var bounds = WindowLayout.ClampToWorkArea(new Rect(Left, Top, ActualWidth, ActualHeight), workArea);
                Left = bounds.Left;
                Top = bounds.Top;
            }
        }

        private void Edit_Loaded(object sender, RoutedEventArgs e)
        {
            FitToWorkArea();

            // The target is the usual reason to open the form
            TextboxTarget.Focus();
            TextboxTarget.SelectAll();
        }

        private void Edit_Closed(object? sender, EventArgs e)
        {
            viewModel.CloseRequested -= ViewModel_CloseRequested;
            viewModel.FocusRequested -= ViewModel_FocusRequested;
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
    }
}
