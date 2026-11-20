using Avalonia.Controls;
using Avalonia.Interactivity;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI
{
    /// <summary>
    /// The modal confirmation before a keycut replaces another or takes a name another command uses (#153; WPF's ConfirmWindow).
    /// The primary button (and Enter) goes ahead with a true result; Cancel and Escape close with false.
    /// </summary>
    public partial class ConfirmWindow : Window
    {
        /// <summary>For the XAML previewer only.</summary>
        public ConfirmWindow()
        {
            InitializeComponent();
        }

        public ConfirmWindow(ConfirmViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
            Opened += (sender, e) =>
            {
                Modal.FitToWorkArea(this);
                // Enter goes ahead (the issue's "second Enter"), so the primary button starts with the focus
                BtnConfirm.Focus();
            };
        }

        /// <summary>Shows the dialog over <paramref name="owner"/> and returns true if the user chose to go ahead.</summary>
        public static bool Ask(Window? owner, ConfirmViewModel viewModel)
        {
            return Modal.Show<bool>(new ConfirmWindow(viewModel), owner);
        }

        private void Confirm_Click(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
