using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using keycuts.GUI.Themes;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI
{
    /// <summary>
    /// The Run box's window (Phase 6.4): its keys, all taken in the text box. Return runs, Tab completes, Up and Down (and Page Up and
    /// Page Down) choose, Esc closes, and so does a click elsewhere (the window losing the focus). A click on a row runs it.
    /// </summary>
    public partial class RunBoxWindow : Window
    {
        /// <summary>For the XAML previewer only.</summary>
        public RunBoxWindow()
            : this(new RunBoxViewModel(_ => { }, _ => { }, () => { }))
        {
        }

        public RunBoxWindow(RunBoxViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;
            Keycap.Source = AppIcons.GetPreview(AppIconManager.TabBar);
            RunText.AddHandler(KeyDownEvent, RunText_KeyDown, RoutingStrategies.Tunnel);
            RunRows.AddHandler(PointerReleasedEvent, RunRows_PointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);

            // Typing, and opening the box again (Reset), go back to the top; a list loaded late leaves the scrolling alone (#337)
            ViewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(RunBoxViewModel.Text) && RunRows.Scroll is { } scroll)
                {
                    scroll.Offset = default;
                }
            };
            Deactivated += (sender, e) =>
            {
                if (IsVisible)
                {
                    ViewModel.Close();
                }
            };
        }

        public RunBoxViewModel ViewModel { get; }

        /// <summary>The text box gets the focus each time the box opens, with what was typed cleared by the view model.</summary>
        public void FocusText()
        {
            RunText.Focus();
            RunText.CaretIndex = RunText.Text?.Length ?? 0;
        }

        /// <summary>A click on a row runs it: the left button, pressed and released on that row. The scroll bar isn't a row (#337).</summary>
        private void RunRows_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton == MouseButton.Left &&
                (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { } row &&
                new Rect(row.Bounds.Size).Contains(e.GetPosition(row)))
            {
                ViewModel.SelectedIndex = RunRows.IndexFromContainer(row);
                ViewModel.Accept();
            }
        }

        private void Choose(int by)
        {
            ViewModel.Move(by);
            if (ViewModel.SelectedIndex >= 0)
            {
                RunRows.ScrollIntoView(ViewModel.SelectedIndex);
            }
        }

        private void RunText_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyModifiers != KeyModifiers.None && e.KeyModifiers != KeyModifiers.Shift)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Enter:
                    e.Handled = true;
                    ViewModel.Accept();
                    break;
                case Key.Tab:
                    e.Handled = true;
                    if (ViewModel.Complete())
                    {
                        RunText.CaretIndex = ViewModel.Text.Length;
                    }

                    break;
                case Key.Up:
                case Key.Down:
                    e.Handled = true;
                    Choose(e.Key == Key.Down ? 1 : -1);
                    break;
                case Key.PageUp:
                case Key.PageDown:
                    e.Handled = true;
                    Choose(e.Key == Key.PageDown ? RunBoxViewModel.VisibleRows : -RunBoxViewModel.VisibleRows);
                    break;
                case Key.Escape:
                    e.Handled = true;
                    ViewModel.Close();
                    break;
            }
        }
    }
}
