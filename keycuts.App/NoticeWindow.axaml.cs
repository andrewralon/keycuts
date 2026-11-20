using Avalonia.Controls;
using Avalonia.Interactivity;
using keycuts.GUI.Controls;

namespace keycuts.GUI
{
    /// <summary>A message with an OK button, for the startup notices and a crash: WPF's <c>MessageBox.Show</c>.</summary>
    public partial class NoticeWindow : Window
    {
        public NoticeWindow()
        {
            InitializeComponent();
            Opened += (sender, e) =>
            {
                Modal.FitToWorkArea(this);
                BtnOk.Focus();
            };
        }

        /// <summary>Shows <paramref name="message"/> over <paramref name="owner"/> until OK, Enter or Escape.</summary>
        public static void Show(Window? owner, string message, bool isError = false)
        {
            var window = new NoticeWindow();
            window.MessageText.Text = message;
            if (isError)
            {
                window.NoticeIcon.Kind = IconKind.Warning;
                window.NoticeIcon.Foreground = window.FindResource("DangerBrush") as Avalonia.Media.IBrush;
            }

            Modal.Show<object>(window, owner);
        }

        private void Ok_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
