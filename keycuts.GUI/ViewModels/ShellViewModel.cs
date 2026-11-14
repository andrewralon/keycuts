using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using keycuts.Common;
using keycuts.GUI.Mvvm;

namespace keycuts.GUI.ViewModels
{
    public enum ShellMode
    {
        Creator,
        Manager,
        Settings
    }

    public enum EscapeResult
    {
        CloseWindow,
        ClosedMenu,
        ClearedSearch,
        ReturnedToCreator,
        ClosedIconPicker
    }

    /// <summary>
    /// Owns the single window's current view (creator, manager or settings, #218) and the navigation between them.
    /// </summary>
    public class ShellViewModel : ObservableObject
    {
        public const string CreatorTitle = "keycuts";

        public const string ManagerTitle = "keycuts";

        private readonly Func<Task<int>> countProvider;

        private ShellMode mode = ShellMode.Creator;

        private object currentView;

        private readonly Func<SettingsViewModel>? settingsFactory;

        private SettingsViewModel? settings;

        public ShellViewModel(
            Func<Task<int>> countProvider,
            Func<CancellationToken, Task<IReadOnlyList<Keycut>>>? keycutLoader = null,
            Func<IReadOnlyList<Keycut>, CancellationToken, Task>? targetChecker = null,
            IKeycutActions? actions = null,
            WelcomeViewModel? welcome = null,
            UpdateNoticeViewModel? updateNotice = null,
            IDescriptionVisibilityStore? descriptionStore = null,
            Func<SettingsViewModel>? settingsFactory = null)
        {
            this.countProvider = countProvider ?? throw new ArgumentNullException(nameof(countProvider));
            this.settingsFactory = settingsFactory;

            ShowManagerCommand = new RelayCommand(ShowManager);
            ShowCreatorCommand = new RelayCommand(ShowCreator);
            OpenSettingsCommand = new RelayCommand(ShowSettings, () => settingsFactory != null);

            Creator = new CreatorViewModel(ShowManagerCommand, OpenSettingsCommand, descriptionStore: descriptionStore)
            {
                Welcome = welcome,
                UpdateNotice = updateNotice
            };
            // Both screens share the footer, so both show the same update line
            Manager = new ManagerViewModel(ShowCreatorCommand, OpenSettingsCommand, keycutLoader, targetChecker, actions)
            {
                UpdateNotice = updateNotice
            };
            currentView = Creator;

            Creator.KeycutCreated += (sender, e) => _ = ReloadAsync();
            Manager.KeycutDeleted += (sender, e) => Creator.KeycutCount = Manager.KeycutCount;
            Manager.KeycutRestored += (sender, e) => Creator.KeycutCount = Manager.KeycutCount;
        }

        public CreatorViewModel Creator { get; }

        public ManagerViewModel Manager { get; }

        public RelayCommand ShowManagerCommand { get; }

        public RelayCommand ShowCreatorCommand { get; }

        public RelayCommand OpenSettingsCommand { get; }

        /// <summary>
        /// The Settings screen's view model while it is showing (#218), made fresh each time the screen opens, so it shows what is
        /// stored now. Null on the other screens.
        /// </summary>
        public SettingsViewModel? Settings
        {
            get { return settings; }
            private set
            {
                if (ReferenceEquals(settings, value))
                {
                    return;
                }

                if (settings != null)
                {
                    settings.PropertyChanged -= Settings_PropertyChanged;
                    settings.Stored -= Settings_Stored;
                    settings.OutputFolderChanged -= Settings_OutputFolderChanged;
                    settings.ShowWelcomeRequested -= Settings_ShowWelcomeRequested;
                    settings.UpdateChecked -= Settings_UpdateChecked;
                }

                settings = value;

                if (settings != null)
                {
                    settings.PropertyChanged += Settings_PropertyChanged;
                    settings.Stored += Settings_Stored;
                    settings.OutputFolderChanged += Settings_OutputFolderChanged;
                    settings.ShowWelcomeRequested += Settings_ShowWelcomeRequested;
                    settings.UpdateChecked += Settings_UpdateChecked;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(AreTabsEnabled));
            }
        }

        /// <summary>The tabs are off while Settings' icon picker is open, as the page behind it is; the caption buttons never are.</summary>
        public bool AreTabsEnabled
        {
            get { return settings?.IsIconPickerOpen != true; }
        }

        public ShellMode Mode
        {
            get { return mode; }
        }

        public object CurrentView
        {
            get { return currentView; }
            private set { SetProperty(ref currentView, value); }
        }

        public string Title
        {
            get { return mode == ShellMode.Manager ? ManagerTitle : CreatorTitle; }
        }

        public void ShowManager()
        {
            if (mode == ShellMode.Manager)
            {
                return;
            }

            Manager.SearchText = string.Empty;
            Manager.SelectedRow = null;
            Manager.IsMenuOpen = false;
            SetMode(ShellMode.Manager, Manager);
            Settings = null;
            _ = ReloadAsync();
        }

        /// <summary>Settings → Show welcome: back to the creator with the first-run panel showing.</summary>
        public void ShowWelcome()
        {
            ShowCreator();
            Creator.Welcome?.Show();
        }

        public void ShowCreator()
        {
            if (mode == ShellMode.Creator)
            {
                return;
            }

            Manager.IsMenuOpen = false;
            SetMode(ShellMode.Creator, Creator);
            Settings = null;
            _ = ReloadAsync();
        }

        /// <summary>The Settings screen (#218), with what is stored now. Does nothing without a way to make its view model.</summary>
        public void ShowSettings()
        {
            if (mode == ShellMode.Settings || settingsFactory == null)
            {
                return;
            }

            Manager.IsMenuOpen = false;
            var screen = settingsFactory();
            screen.Footer = Creator;
            Settings = screen;
            SetMode(ShellMode.Settings, screen);
            _ = ReloadAsync();
        }

        /// <summary>
        /// Creator: close the window. Manager: close an open menu, else clear a non-empty search, else return to the creator.
        /// Settings: close the icon picker if it is open, else return to the creator. Nothing on Settings is unsaved (#218).
        /// </summary>
        public EscapeResult HandleEscape()
        {
            if (mode == ShellMode.Creator)
            {
                return EscapeResult.CloseWindow;
            }

            if (mode == ShellMode.Settings)
            {
                if (settings?.IsIconPickerOpen == true)
                {
                    settings.IsIconPickerOpen = false;
                    return EscapeResult.ClosedIconPicker;
                }

                ShowCreator();
                return EscapeResult.ReturnedToCreator;
            }

            if (Manager.IsMenuOpen)
            {
                Manager.IsMenuOpen = false;
                return EscapeResult.ClosedMenu;
            }

            if (Manager.SearchText.Length > 0)
            {
                Manager.SearchText = string.Empty;
                return EscapeResult.ClearedSearch;
            }

            ShowCreator();
            return EscapeResult.ReturnedToCreator;
        }

        /// <summary>
        /// Reloads the keycuts. While the manager is showing it loads the rows (and the count follows from them);
        /// otherwise only the count is read. A missing or unreadable folder counts as zero.
        /// </summary>
        public async Task ReloadAsync()
        {
            if (mode == ShellMode.Manager && Manager.CanLoad)
            {
                await Manager.LoadAsync();
                Creator.KeycutCount = Manager.KeycutCount;
                Creator.Welcome?.OnKeycutCountLoaded(Manager.KeycutCount);
                return;
            }

            int count;
            try
            {
                count = await countProvider();
            }
            catch (Exception)
            {
                count = 0;
            }

            Creator.KeycutCount = count;
            Manager.KeycutCount = count;
            Creator.Welcome?.OnKeycutCountLoaded(count);
        }

        private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsViewModel.IsIconPickerOpen))
            {
                OnPropertyChanged(nameof(AreTabsEnabled));
            }
        }

        // What the old dialog's close refreshed now follows each change: the environment-variables preview and the update line
        private void Settings_Stored(object? sender, EventArgs e)
        {
            if (sender is SettingsViewModel stored)
            {
                Creator.UseEnvironmentVariablesSetting = stored.UseEnvironmentVariables;
            }

            Creator.UpdateNotice?.Refresh();
        }

        // The count is the new folder's
        private void Settings_OutputFolderChanged(object? sender, EventArgs e)
        {
            _ = ReloadAsync();
        }

        private void Settings_ShowWelcomeRequested(object? sender, EventArgs e)
        {
            ShowWelcome();
        }

        // Check now can answer after the screen was left; the footer's line follows whenever it does
        private void Settings_UpdateChecked(object? sender, EventArgs e)
        {
            Creator.UpdateNotice?.Refresh();
        }

        private void SetMode(ShellMode newMode, object view)
        {
            mode = newMode;
            CurrentView = view;
            OnPropertyChanged(nameof(Mode));
            OnPropertyChanged(nameof(Title));
        }
    }
}
