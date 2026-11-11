using System.Windows;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI
{
    /// <summary>
    /// The modal confirmation before a keycut replaces another or takes a name another command uses (#153). It looks
    /// like the Settings window: the same surface, heading, footer strip and buttons, in every theme. The primary button
    /// (and Enter) goes ahead with a true result; Cancel and Escape close with false.
    /// </summary>
    public partial class ConfirmWindow : Window
    {
        public ConfirmWindow(ConfirmViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        /// <summary>Shows the dialog over <paramref name="owner"/> and returns true if the user chose to go ahead.</summary>
        public static bool Ask(Window? owner, ConfirmViewModel viewModel)
        {
            var window = new ConfirmWindow(viewModel);

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

        // Same as Settings: cap the height to the screen so the footer stays reachable, and keep the window on it
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

        private void Confirm_Loaded(object sender, RoutedEventArgs e)
        {
            FitToWorkArea();

            // Enter goes ahead (the issue's "second Enter"), so the primary button starts with the focus
            BtnConfirm.Focus();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
