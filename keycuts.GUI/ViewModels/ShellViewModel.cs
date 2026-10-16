using System;
using System.Threading.Tasks;
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

        public const string ManagerTitle = "keycuts \u2014 Your keycuts";

        private readonly Func<Task<int>> countProvider;

        private ShellMode mode = ShellMode.Creator;

        private object currentView;

        public ShellViewModel(Func<Task<int>> countProvider)
        {
            this.countProvider = countProvider ?? throw new ArgumentNullException(nameof(countProvider));

            ShowManagerCommand = new RelayCommand(ShowManager);
            ShowCreatorCommand = new RelayCommand(ShowCreator);
            OpenSettingsCommand = new RelayCommand(() => SettingsRequested?.Invoke(this, EventArgs.Empty));

            Creator = new CreatorViewModel(ShowManagerCommand, OpenSettingsCommand);
            Manager = new ManagerViewModel(ShowCreatorCommand, OpenSettingsCommand);
            currentView = Creator;

            Creator.KeycutCreated += (sender, e) => _ = ReloadAsync();
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
            Manager.IsMenuOpen = false;
            SetMode(ShellMode.Manager, Manager);
            _ = ReloadAsync();
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

        /// <summary>Reloads the keycut count (and later the manager's data). A missing or unreadable folder counts as zero.</summary>
        public async Task ReloadAsync()
        {
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
