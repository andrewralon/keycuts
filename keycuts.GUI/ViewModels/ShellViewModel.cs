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
        Manager
    }

    public enum EscapeResult
    {
        CloseWindow,
        ClosedMenu,
        ClearedSearch,
        ReturnedToCreator
    }

    /// <summary>
    /// Owns the single window's current view (creator or manager) and the navigation between them.
    /// </summary>
    public class ShellViewModel : ObservableObject
    {
        public const string CreatorTitle = "keycuts";

        public const string ManagerTitle = "keycuts";

        private readonly Func<Task<int>> countProvider;

        private ShellMode mode = ShellMode.Creator;

        private object currentView;

        public ShellViewModel(
            Func<Task<int>> countProvider,
            Func<CancellationToken, Task<IReadOnlyList<Keycut>>>? keycutLoader = null,
            Func<IReadOnlyList<Keycut>, CancellationToken, Task>? targetChecker = null,
            IKeycutActions? actions = null,
            WelcomeViewModel? welcome = null,
            UpdateNoticeViewModel? updateNotice = null,
            IDescriptionVisibilityStore? descriptionStore = null)
        {
            this.countProvider = countProvider ?? throw new ArgumentNullException(nameof(countProvider));

            ShowManagerCommand = new RelayCommand(ShowManager);
            ShowCreatorCommand = new RelayCommand(ShowCreator);
            OpenSettingsCommand = new RelayCommand(() => SettingsRequested?.Invoke(this, EventArgs.Empty));

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

            if (welcome != null)
            {
                welcome.KeycutCreated += (sender, e) => _ = ReloadAsync();
            }
            Manager.KeycutDeleted += (sender, e) => Creator.KeycutCount = Manager.KeycutCount;
            Manager.KeycutRestored += (sender, e) => Creator.KeycutCount = Manager.KeycutCount;
        }

        /// <summary>Raised when a settings button is pressed. The window opens the dialog and then calls <see cref="ReloadAsync"/>.</summary>
        public event EventHandler? SettingsRequested;

        public CreatorViewModel Creator { get; }

        public ManagerViewModel Manager { get; }

        public RelayCommand ShowManagerCommand { get; }

        public RelayCommand ShowCreatorCommand { get; }

        public RelayCommand OpenSettingsCommand { get; }

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
            _ = ReloadAsync();
        }

        /// <summary>
        /// Creator: close the window. Manager: close an open menu, else clear a non-empty search, else return to the creator.
        /// </summary>
        public EscapeResult HandleEscape()
        {
            if (mode == ShellMode.Creator)
            {
                return EscapeResult.CloseWindow;
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

        private void SetMode(ShellMode newMode, object view)
        {
            mode = newMode;
            CurrentView = view;
            OnPropertyChanged(nameof(Mode));
            OnPropertyChanged(nameof(Title));
        }
    }
}
