using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
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
                FitToWorkArea();
                // Enter goes ahead (the issue's "second Enter"), so the primary button starts with the focus
                this.FindControl<Button>("BtnConfirm")!.Focus();
            };
        }

        /// <summary>Shows the dialog over <paramref name="owner"/> and returns true if the user chose to go ahead.</summary>
        public static bool Ask(Window? owner, ConfirmViewModel viewModel)
        {
            var window = new ConfirmWindow(viewModel);
            return Modal.Show<bool>(window, owner);
        }

        // Cap the height to the screen so the footer stays reachable, and keep the window on it
        private void FitToWorkArea()
        {
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen == null)
            {
                return;
            }

            var scale = screen.Scaling;
            var area = screen.WorkingArea;
            var workArea = new Rect(area.X / scale, area.Y / scale, area.Width / scale, area.Height / scale);
            MaxHeight = workArea.Height;
            var bounds = WindowLayout.ClampToWorkArea(new Rect(Position.X / scale, Position.Y / scale, Bounds.Width, Bounds.Height), workArea);
            Position = new PixelPoint((int)(bounds.Left * scale), (int)(bounds.Top * scale));
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
