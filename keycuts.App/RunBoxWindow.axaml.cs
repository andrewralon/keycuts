using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using keycuts.GUI.Themes;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI
{
    /// <summary>
    /// The Run box's window (Phase 6.4): its keys, all taken in the text box. Return runs, Tab completes, Up and Down choose, Esc
    /// closes, and so does a click elsewhere (the window losing the focus). A click on a row runs it.
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
            RunRows.AddHandler(PointerReleasedEvent, (sender, e) => ViewModel.Accept(), RoutingStrategies.Bubble, handledEventsToo: true);
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
                    ViewModel.Move(e.Key == Key.Down ? 1 : -1);
                    break;
                case Key.Escape:
                    e.Handled = true;
                    ViewModel.Close();
                    break;
            }
        }
    }
}
