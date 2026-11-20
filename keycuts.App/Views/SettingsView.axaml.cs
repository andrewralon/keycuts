using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using keycuts.Common;
using keycuts.GUI.Controls;
using keycuts.GUI.Themes;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI.Views
{
    /// <summary>
    /// The Settings screen (#218; WPF's Views/SettingsView), the main window's third, over a <see cref="SettingsViewModel"/>.
    /// Everything is stored as it changes; this view only commits the output folder (Enter, or leaving the box), checks the saved
    /// theme without storing it again, moves the focus for the icon picker and keeps the picker's two lists to one choice.
    /// </summary>
    public partial class SettingsView : UserControl
    {
        private SettingsViewModel? viewModel;

        private bool initializing;

        public SettingsView()
        {
            InitializeComponent();
            DataContextChanged += SettingsView_DataContextChanged;
            Loaded += (sender, e) => FocusFolderOnShow();

            TextboxOutputFolder.AddHandler(KeyDownEvent, OutputFolder_KeyDown, RoutingStrategies.Tunnel);
            TextboxOutputFolder.LostFocus += (sender, e) => viewModel?.CommitOutputFolder();
            foreach (var choice in ThemeChoices.Children.OfType<RadioButton>())
            {
                choice.IsCheckedChanged += Theme_Checked;
            }

            // Phase 6.2: the Run box shortcut is the next key pressed on its button while it records
            ShortcutRecorder.AddHandler(KeyDownEvent, ShortcutRecorder_KeyDown, RoutingStrategies.Tunnel);

            IconPickerScrim.PointerPressed += IconPickerScrim_PointerPressed;
            // Each group's list: its cards, and the choice, which goes through the view model so the other list lets go of its own
            IconGroups.AddHandler(ListBox.SelectionChangedEvent, IconList_SelectionChanged, RoutingStrategies.Bubble);
        }

        /// <summary>The folder picker behind the choose-a-folder button, seeded with the folder in the box. Null when cancelled.</summary>
        public static string? PickFolder(Window? owner, string current)
        {
            if (owner?.StorageProvider is not { } storage)
            {
                return null;
            }

            var options = new FolderPickerOpenOptions { Title = "Choose the output folder", AllowMultiple = false };
            var picked = Modal.Wait(async () =>
            {
                if (!string.IsNullOrWhiteSpace(current) && System.IO.Directory.Exists(current))
                {
                    options.SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(current);
                }

                return (await storage.OpenFolderPickerAsync(options)).FirstOrDefault()?.TryGetLocalPath();
            });
            return picked;
        }

        /// <summary>Opens the output folder in the file manager. A folder that was deleted, or one that refused, does nothing.</summary>
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

        private void SettingsView_DataContextChanged(object? sender, EventArgs e)
        {
            if (viewModel != null)
            {
                viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            viewModel = DataContext as SettingsViewModel;
            if (viewModel == null)
            {
                return;
            }

            CheckTheme();
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        // Checking the stored choice is not a change, so nothing is stored or applied again
        private void CheckTheme()
        {
            initializing = true;
            ThemeChoices.Children.OfType<RadioButton>().First(choice => (ThemePreference)choice.Tag! == viewModel!.Theme).IsChecked = true;
            initializing = false;
        }

        private void FocusFolderOnShow()
        {
            // Arrowing through the tabs keeps the focus there (#218)
            if (RailTabControl.HoldsKeyboardFocus(TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement()))
            {
                return;
            }

            TextboxOutputFolder.Focus();
            TextboxOutputFolder.SelectAll();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (viewModel == null)
            {
                return;
            }

            if (e.PropertyName == nameof(SettingsViewModel.SaveError) && viewModel.HasSaveError)
            {
                BringIntoView(SaveErrorMessage);
            }
            else if (e.PropertyName == nameof(SettingsViewModel.UpdateStatusText) && viewModel.HasUpdateStatus)
            {
                BringIntoView(UpdateStatusMessage);
            }
            else if (e.PropertyName == nameof(SettingsViewModel.IsIconPickerOpen))
            {
                MoveFocusForIconPicker();
            }
            else if (e.PropertyName == nameof(SettingsViewModel.SelectedIcon))
            {
                SelectIconCard();
            }
            else if (e.PropertyName == nameof(SettingsViewModel.Theme))
            {
                // Restore defaults can change the theme; the radio buttons follow without storing it again
                CheckTheme();
            }
        }

        // On a short screen a message can sit below the visible part of the page (its text change is announced)
        private void BringIntoView(Control element)
        {
            Dispatcher.UIThread.Post(() => element.BringIntoView(), DispatcherPriority.Background);
        }

        #region UI Handlers

        // Enter stores the folder; an invalid one keeps the focus in the box with its error. Escape drops an edit and puts the
        // stored folder back; with no edit to drop it goes on to the window, which returns to the creator.
        /// <summary>
        /// While the shortcut records: Esc cancels, Delete (or ⌫) alone clears it, a key with ⌘ or ⌃ becomes it, and one without
        /// is shown as refused. Modifier keys alone wait for the key. Every key is taken, so none reaches the window.
        /// </summary>
        private void ShortcutRecorder_KeyDown(object? sender, KeyEventArgs e)
        {
            if (viewModel is not { IsRecordingShortcut: true })
            {
                return;
            }

            e.Handled = true;
            var modifiers = e.KeyModifiers;
            if (e.Key == Key.Escape && modifiers == KeyModifiers.None)
            {
                viewModel.IsRecordingShortcut = false;
            }
            else if (e.Key is Key.Back or Key.Delete && modifiers == KeyModifiers.None)
            {
                viewModel.ClearShortcut();
            }
            else if (ShortcutKeyName(e) is string key)
            {
                viewModel.RecordShortcut(new RunBoxHotkey(key, modifiers.HasFlag(KeyModifiers.Control), modifiers.HasFlag(KeyModifiers.Alt),
                    modifiers.HasFlag(KeyModifiers.Shift), modifiers.HasFlag(KeyModifiers.Meta)));
            }
        }

        /// <summary>The key as <see cref="RunBoxHotkey"/> names it, by its place on the keyboard; null for a modifier or a key it doesn't take.</summary>
        public static string? ShortcutKeyName(KeyEventArgs e)
        {
            var physical = e.PhysicalKey.ToString();
            string? name = physical switch
            {
                _ when physical.Length == 1 && char.IsLetter(physical[0]) => physical,
                _ when physical.StartsWith("Digit", StringComparison.Ordinal) => physical.Substring(5),
                "Space" => "Space",
                "Comma" => ",",
                "Period" => ".",
                "Slash" => "/",
                "Semicolon" => ";",
                "Quote" => "'",
                "BracketLeft" => "[",
                "BracketRight" => "]",
                "Backslash" => "\\",
                "Minus" => "-",
                "Equal" => "=",
                "Backquote" => "`",
                _ when physical.Length <= 3 && physical.StartsWith('F') => physical,
                _ => null
            };

            // Without a physical key (some tests, some input methods), the key itself for letters, digits and Space
            if (name == null && e.PhysicalKey == PhysicalKey.None)
            {
                name = e.Key switch
                {
                    >= Key.A and <= Key.Z => e.Key.ToString(),
                    >= Key.D0 and <= Key.D9 => ((int)(e.Key - Key.D0)).ToString(),
                    Key.Space => "Space",
                    >= Key.F1 and <= Key.F12 => e.Key.ToString(),
                    _ => null
                };
            }

            return name != null && RunBoxHotkey.KeyCodes.ContainsKey(name) ? name : null;
        }

        private void OutputFolder_KeyDown(object? sender, KeyEventArgs e)
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

        // A click on the dimmed page behind the picker closes it
        private void IconPickerScrim_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (viewModel != null)
            {
                viewModel.IsIconPickerOpen = false;
            }

            e.Handled = true;
        }

        private ListBox[] IconLists
        {
            get { return IconGroups.GetVisualDescendants().OfType<ListBox>().ToArray(); }
        }

        private void IconList_ContainerPrepared(object? sender, ContainerPreparedEventArgs e)
        {
            if (e.Container is ListBoxItem card && card.DataContext is AppIconOption option)
            {
                card.Classes.Set("wide", option.IsWide);
                Avalonia.Automation.AutomationProperties.SetName(card, option.Name);
                Avalonia.Automation.AutomationProperties.SetHelpText(card, option.Description);
                if (ReferenceEquals(option, viewModel?.SelectedIcon))
                {
                    Dispatcher.UIThread.Post(SelectIconCard);
                }
            }
        }

        private void IconList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (initializing || viewModel == null || e.AddedItems.OfType<AppIconOption>().FirstOrDefault() is not { } picked)
            {
                return;
            }

            viewModel.SelectedIcon = picked;
        }

        // Each list shows the choice if it holds it, and none otherwise
        private void SelectIconCard()
        {
            initializing = true;
            try
            {
                foreach (var list in IconLists)
                {
                    var items = list.ItemsSource?.Cast<object>() ?? Enumerable.Empty<object>();
                    list.SelectedItem = items.Contains(viewModel?.SelectedIcon) ? viewModel!.SelectedIcon : null;
                }
            }
            finally
            {
                initializing = false;
            }
        }

        // Opening moves the focus to the picked card, so the arrows move from there; closing gives it back to the icon button
        private void MoveFocusForIconPicker()
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (viewModel == null)
                {
                    return;
                }

                if (viewModel.IsIconPickerOpen)
                {
                    SelectIconCard();
                    var card = IconLists.Select(l => l.ContainerFromItem(viewModel.SelectedIcon!)).OfType<ListBoxItem>().FirstOrDefault();
                    if (card != null)
                    {
                        card.Focus();
                    }
                    else
                    {
                        IconLists.FirstOrDefault()?.Focus();
                    }
                }
                else
                {
                    BtnIcon.Focus();
                }
            }, DispatcherPriority.Input);
        }

        // A click applies and stores the theme at once
        private void Theme_Checked(object? sender, RoutedEventArgs e)
        {
            if (!initializing && viewModel != null && sender is RadioButton { IsChecked: true } choice)
            {
                viewModel.Theme = (ThemePreference)choice.Tag!;
            }
        }

        #endregion UI Handlers
    }
}
