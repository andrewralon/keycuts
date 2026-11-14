using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using keycuts.Common;
using keycuts.GUI.Controls;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI.Views
{
    /// <summary>
    /// The Settings screen (#218), the main window's third, over a <see cref="SettingsViewModel"/>. Everything is stored as it
    /// changes; this view only commits the output folder (Enter, or leaving the box), checks the saved theme without storing it
    /// again, moves the focus for the icon picker and announces what changes on the page.
    /// </summary>
    public partial class SettingsView : UserControl
    {
        private SettingsViewModel? viewModel;

        private bool initializing;

        public SettingsView()
        {
            InitializeComponent();
            DataContextChanged += SettingsView_DataContextChanged;
        }

        /// <summary>The folder picker behind the choose-a-folder button, seeded with the folder in the box. Null when cancelled.</summary>
        public static string? PickFolder(Window? owner, string current)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose the output folder" };

            if (!string.IsNullOrWhiteSpace(current) && System.IO.Directory.Exists(current))
            {
                dialog.InitialDirectory = current;
            }

            var picked = owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
            return picked == true ? dialog.FolderName : null;
        }

        /// <summary>Opens the output folder in Explorer. A folder that was deleted, or an Explorer that refused, does nothing.</summary>
        public static void OpenFolder(string folder)
        {
            try
            {
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch (Exception e) when (e is Win32Exception || e is InvalidOperationException)
            {
            }
        }

        private void SettingsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (viewModel != null)
            {
                viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            viewModel = e.NewValue as SettingsViewModel;
            if (viewModel == null)
            {
                return;
            }

            // Checking the stored choice raises Checked; that is not a change, so nothing is stored or applied again
            initializing = true;
            ThemeChoices.Children.OfType<RadioButton>().First(choice => (ThemePreference)choice.Tag == viewModel.Theme).IsChecked = true;
            initializing = false;

            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            // Arrowing through the tabs keeps the focus there (#218)
            if (RailTabControl.HoldsKeyboardFocus())
            {
                return;
            }

            TextboxOutputFolder.Focus();
            Keyboard.Focus(TextboxOutputFolder);
            TextboxOutputFolder.SelectAll();
        }

        // The helper line announces itself (HelperText); the rest change in code, where a live setting alone raises nothing
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (viewModel == null)
            {
                return;
            }

            if (e.PropertyName == nameof(SettingsViewModel.SaveError) && viewModel.HasSaveError)
            {
                Announce(SaveErrorMessage);
            }
            else if (e.PropertyName == nameof(SettingsViewModel.UpdateStatusText) && viewModel.HasUpdateStatus)
            {
                Announce(UpdateStatusMessage);
            }
            else if (e.PropertyName == nameof(SettingsViewModel.IsIconPickerOpen))
            {
                MoveFocusForIconPicker();
            }
            else if (e.PropertyName == nameof(SettingsViewModel.IconError) && viewModel.HasIconError)
            {
                Announce(IconPickerNoteText);
            }
            else if (e.PropertyName == nameof(SettingsViewModel.Theme))
            {
                // Restore defaults can change the theme; the radio buttons follow without storing it again
                initializing = true;
                ThemeChoices.Children.OfType<RadioButton>().First(choice => (ThemePreference)choice.Tag == viewModel.Theme).IsChecked = true;
                initializing = false;
            }
        }

        // Also scrolls the message into view: on a short screen it can sit below the visible part of the page
        private void Announce(FrameworkElement element)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                element.BringIntoView();
                LiveRegion.Announce(element);
            }));
        }

        #region UI Handlers

        // Enter stores the folder; an invalid one keeps the focus in the box with its error. Escape drops an edit and puts the
        // stored folder back, storing nothing; with no edit to drop it goes on to the window, which returns to the creator.
        private void OutputFolder_KeyDown(object sender, KeyEventArgs e)
        {
            if (viewModel == null)
            {
                return;
            }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (viewModel.CommitOutputFolder())
                {
                    TextboxOutputFolder.SelectAll();
                }
            }
            else if (e.Key == Key.Escape && viewModel.RevertOutputFolder())
            {
                e.Handled = true;
                TextboxOutputFolder.SelectAll();
            }
        }

        // Leaving the box stores it too, whether to the folder button, another setting or another tab
        private void OutputFolder_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            viewModel?.CommitOutputFolder();
        }

        // A click on the dimmed page behind the picker closes it
        private void IconPickerScrim_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (viewModel != null)
            {
                viewModel.IsIconPickerOpen = false;
            }

            e.Handled = true;
        }

        // Opening moves the focus to the picked card, so the arrows move from there; closing gives it back to the icon button.
        // Input priority runs after the layout that creates the cards.
        private void MoveFocusForIconPicker()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (viewModel == null)
                {
                    return;
                }

                if (viewModel.IsIconPickerOpen)
                {
                    if (IconList.ItemContainerGenerator.ContainerFromItem(viewModel.SelectedIcon) is ListBoxItem card)
                    {
                        card.Focus();
                    }
                    else
                    {
                        IconList.Focus();
                    }
                }
                else
                {
                    BtnIcon.Focus();
                }
            }));
        }

        // A click applies and stores the theme at once
        private void Theme_Checked(object sender, RoutedEventArgs e)
        {
            if (!initializing && viewModel != null)
            {
                viewModel.Theme = (ThemePreference)((RadioButton)sender).Tag;
            }
        }

        #endregion UI Handlers
    }
}
