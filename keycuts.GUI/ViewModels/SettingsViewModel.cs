using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Security;
using System.Threading.Tasks;
using System.Windows.Data;
using keycuts.Common;
using keycuts.GUI.Mvvm;
using keycuts.GUI.Themes;

namespace keycuts.GUI.ViewModels
{
    /// <summary>Where the Settings window reads and writes its values. The real one goes through <see cref="Runner"/> and the registry.</summary>
    public interface ISettingsStore
    {
        Settings Load();

        /// <summary>Returns a message for the user when something couldn't be saved, otherwise null.</summary>
        string? Save(Settings settings);
    }

    public class RunnerSettingsStore : ISettingsStore
    {
        public Settings Load()
        {
            var settings = new Settings();
            settings.LoadSettings();
            return settings;
        }

        public string? Save(Settings settings)
        {
            return settings.SaveSettings();
        }
    }

    /// <summary>
    /// State and rules of the Settings window. Nothing is stored until <see cref="Save"/> passes validation;
    /// closing the window any other way (Escape, the close button) leaves the store untouched. The icon is the exception:
    /// a pick applies and is saved at once (<see cref="SelectedIcon"/>).
    /// </summary>
    public class SettingsViewModel : ObservableObject
    {
        public const string DefaultHelperText = "New keycuts files are saved here.";

        public const string EmptyFolderMessage = "Choose where keycuts are saved.";

        public const string UnwritableFolderMessage = "Choose a folder you have permission to write to.";

        public const string VersionFormat = "Version {0}";

        public const string CheckingForUpdatesMessage = "Checking for updates\u2026";

        public const string UpToDateFormat = "You\u2019re up to date ({0}).";

        public const string UpdateCheckFailedMessage = "Couldn\u2019t check for updates.";

        private readonly ISettingsStore store;

        private readonly Func<string, string?> pickFolder;

        private readonly Action<string> openFolder;

        private readonly Func<string, string?> checkFolder;

        private readonly Func<string, bool> directoryExists;

        private readonly UpdateChecker? updates;

        private readonly Action<string> openUrl;

        private UpdateCheckResult? updateResult;

        private string updateStatus = string.Empty;

        private bool storeChanged;

        private bool showVersionRow = true;

        private string outputFolder = string.Empty;

        private string? outputFolderError;

        private string? saveError;

        private bool askBeforeReplacing;

        private bool rightClickContextMenus;

        private bool useEnvironmentVariables;

        private ThemePreference theme;

        private bool checkForUpdates;

        private readonly IAppIconService appIcon;

        private AppIconOption selectedIcon;

        private bool isIconPickerOpen;

        private string? iconError;

        /// <param name="pickFolder">Shows a folder picker seeded with the given folder; null when cancelled.</param>
        /// <param name="openFolder">Opens the folder in Explorer.</param>
        /// <param name="checkFolder">Creates the folder if needed and proves it is writable; returns an error message or null. Tests use temp folders.</param>
        /// <param name="directoryExists">Decides whether Launch is enabled.</param>
        /// <param name="updates">The update check behind Check now (#171), shared with the creator's notice. Null hides Check now.</param>
        /// <param name="openUrl">Opens a release page in the browser; tests pass a fake.</param>
        /// <param name="appIcon">Reads and applies the app icon; a pick applies at once. Null keeps it in memory, as tests want.</param>
        public SettingsViewModel(
            ISettingsStore store,
            Func<string, string?>? pickFolder = null,
            Action<string>? openFolder = null,
            Func<string, string?>? checkFolder = null,
            Func<string, bool>? directoryExists = null,
            UpdateChecker? updates = null,
            Action<string>? openUrl = null,
            IAppIconService? appIcon = null)
        {
            this.appIcon = appIcon ?? new InMemoryAppIconService();
            selectedIcon = AppIcons.Get(this.appIcon.Choice);
            var view = new ListCollectionView(new List<AppIconOption>(AppIcons.Options));
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AppIconOption.Group)));
            IconOptionsView = view;
            OpenIconPickerCommand = new RelayCommand(() => IsIconPickerOpen = true);
            CloseIconPickerCommand = new RelayCommand(() => IsIconPickerOpen = false);

            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.pickFolder = pickFolder ?? (_ => null);
            this.openFolder = openFolder ?? (_ => { });
            this.checkFolder = checkFolder ?? CheckFolder;
            this.directoryExists = directoryExists ?? Directory.Exists;
            this.updates = updates;
            this.openUrl = openUrl ?? UpdateNoticeViewModel.OpenInBrowser;

            ChooseCommand = new RelayCommand(Choose);
            LaunchCommand = new RelayCommand(Launch, () => CanLaunch);
            CheckNowCommand = new AsyncRelayCommand(CheckNowAsync, () => updates != null);
            DownloadUpdateCommand = new RelayCommand(DownloadUpdate, () => IsUpdateAvailable);

            var loaded = store.Load();
            outputFolder = loaded.OutputFolder ?? string.Empty;
            askBeforeReplacing = loaded.AskBeforeReplacing;
            rightClickContextMenus = loaded.RightClickContextMenus;
            useEnvironmentVariables = loaded.UseEnvironmentVariables;
            theme = loaded.Theme;
            checkForUpdates = loaded.CheckForUpdates;

            // What an earlier check found, until Check now asks again
            var known = updates?.GetLatestKnown();
            if (known != null)
            {
                ShowUpdateResult(known);
            }
        }

        /// <summary>Raised after a successful save; the window closes with a true result.</summary>
        public event EventHandler? Saved;

        public RelayCommand ChooseCommand { get; }

        public RelayCommand LaunchCommand { get; }

        /// <summary>Asks GitHub now, whatever the toggle and the last check, and always shows a result.</summary>
        public AsyncRelayCommand CheckNowCommand { get; }

        public RelayCommand DownloadUpdateCommand { get; }

        /// <summary>
        /// Raised on the UI thread after Check now stored what it found, so the creator's line follows even when the window
        /// closed before the answer came.
        /// </summary>
        public event EventHandler? UpdateChecked;

        public string OutputFolder
        {
            get { return outputFolder; }
            set
            {
                if (SetProperty(ref outputFolder, value ?? string.Empty))
                {
                    // The old messages were about the old text
                    OutputFolderError = null;
                    SaveError = null;
                    OnPropertyChanged(nameof(CanLaunch));
                    LaunchCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string? OutputFolderError
        {
            get { return outputFolderError; }
            private set
            {
                if (SetProperty(ref outputFolderError, value))
                {
                    OnPropertyChanged(nameof(HasOutputFolderError));
                    OnPropertyChanged(nameof(OutputFolderMessage));
                }
            }
        }

        public bool HasOutputFolderError
        {
            get { return outputFolderError != null; }
        }

        /// <summary>The error when there is one, otherwise the helper text. Read by screen readers as the field's help.</summary>
        public string OutputFolderMessage
        {
            get { return outputFolderError ?? DefaultHelperText; }
        }

        /// <summary>A problem after validation, such as PATH not updating. The window stays open.</summary>
        public string? SaveError
        {
            get { return saveError; }
            private set
            {
                if (SetProperty(ref saveError, value))
                {
                    OnPropertyChanged(nameof(HasSaveError));
                }
            }
        }

        public bool HasSaveError
        {
            get { return saveError != null; }
        }

        /// <summary>Confirm before a new keycut replaces one with the same name (#153). Name conflicts always warn.</summary>
        public bool AskBeforeReplacing
        {
            get { return askBeforeReplacing; }
            set { SetProperty(ref askBeforeReplacing, value); }
        }

        public bool RightClickContextMenus
        {
            get { return rightClickContextMenus; }
            set { SetProperty(ref rightClickContextMenus, value); }
        }

        /// <summary>Save new file and folder keycuts as %USERPROFILE%\... and the like (#31). Shift flips it per keycut.</summary>
        public bool UseEnvironmentVariables
        {
            get { return useEnvironmentVariables; }
            set { SetProperty(ref useEnvironmentVariables, value); }
        }

        public ThemePreference Theme
        {
            get { return theme; }
            set { SetProperty(ref theme, value); }
        }

        /// <summary>Opens the icon picker: the current icon is a button.</summary>
        public RelayCommand OpenIconPickerCommand { get; }

        /// <summary>Closes the icon picker. A pick is already applied and saved, so closing never undoes one.</summary>
        public RelayCommand CloseIconPickerCommand { get; }

        /// <summary>
        /// The picker's list, every choice in order, grouped by <see cref="AppIconOption.Group"/>. A view of this window's own, because the default
        /// view of the shared list would collect a group description per window.
        /// </summary>
        public ICollectionView IconOptionsView { get; }

        /// <summary>
        /// The icon. Setting it applies and saves it at once, unlike the rest of Settings, which waits for Save; Escape and
        /// closing the window keep it.
        /// </summary>
        public AppIconOption SelectedIcon
        {
            get { return selectedIcon; }
            set
            {
                // A list that loses its selection sets null; that isn't a pick
                if (value == null || value == selectedIcon)
                {
                    return;
                }

                selectedIcon = value;
                IconError = appIcon.Apply(value.Choice);
                OnPropertyChanged();
                OnPropertyChanged(nameof(IconButtonName));
            }
        }

        /// <summary>The icon button's name for screen readers, which can't see the picture.</summary>
        public string IconButtonName
        {
            get { return $"Icon: {selectedIcon.Name}. Change"; }
        }

        public bool IsIconPickerOpen
        {
            get { return isIconPickerOpen; }
            set
            {
                if (SetProperty(ref isIconPickerOpen, value))
                {
                    OnPropertyChanged(nameof(IsBodyEnabled));
                }
            }
        }

        /// <summary>The rest of the window is disabled while the picker is open, so Tab stays in it and Enter can't Save.</summary>
        public bool IsBodyEnabled
        {
            get { return !isIconPickerOpen; }
        }

        /// <summary>Shown in the picker when a pick couldn't be saved; it still applies until the app closes.</summary>
        public string? IconError
        {
            get { return iconError; }
            private set
            {
                if (SetProperty(ref iconError, value))
                {
                    OnPropertyChanged(nameof(IconPickerNote));
                    OnPropertyChanged(nameof(HasIconError));
                }
            }
        }

        public bool HasIconError
        {
            get { return iconError != null; }
        }

        public string IconPickerNote
        {
            get { return iconError ?? "Applies as soon as you pick one."; }
        }

        /// <summary>Check GitHub for a newer release once a day when the app starts (#171).</summary>
        public bool CheckForUpdates
        {
            get { return checkForUpdates; }
            set { SetProperty(ref checkForUpdates, value); }
        }

        /// <summary>"Version 1.1.1": the running version in three parts.</summary>
        public string CurrentVersionText
        {
            get { return string.Format(VersionFormat, CurrentVersion.ToString(3)); }
        }

        /// <summary>
        /// Whether Settings shows the row with <see cref="CurrentVersionText"/>, the update status and Check now. On, except
        /// for the README's screenshots (<c>tools/New-Screenshots.ps1</c>), which leave the whole row out so they don't go
        /// stale at every release.
        /// </summary>
        public bool ShowVersionRow
        {
            get { return showVersionRow; }
            set { SetProperty(ref showVersionRow, value); }
        }

        public bool CanCheckForUpdates
        {
            get { return updates != null; }
        }

        /// <summary>The last check's result (stored or from Check now): up to date, available, checking, or couldn't check. Empty before any.</summary>
        public string UpdateStatusText
        {
            get { return updateStatus; }
            private set
            {
                if (SetProperty(ref updateStatus, value))
                {
                    OnPropertyChanged(nameof(HasUpdateStatus));
                }
            }
        }

        public bool HasUpdateStatus
        {
            get { return updateStatus.Length > 0; }
        }

        /// <summary>Shows Download beside the status.</summary>
        public bool IsUpdateAvailable
        {
            get { return updateResult?.Status == UpdateStatus.Available; }
        }

        private Version CurrentVersion
        {
            get { return updates?.CurrentVersion ?? UpdateChecker.Normalize(Version.Parse(Runner.Version)); }
        }

        /// <summary>
        /// Launch needs a folder that exists right now. This checks the disk on each edit; typing a network path can pause
        /// briefly while Windows looks up the server, which was judged acceptable for a rarely edited setting (PR #151).
        /// </summary>
        public bool CanLaunch
        {
            get
            {
                var folder = outputFolder.Trim();
                return folder.Length > 0 && SafeDirectoryExists(folder);
            }
        }

        /// <summary>
        /// True once a save reached the store, even when it then reported a problem (for example PATH not updating):
        /// the folder and other values may already be stored, so the main window must refresh however the dialog closes.
        /// </summary>
        public bool StoreChanged
        {
            get { return storeChanged; }
        }

        /// <summary>Validates, saves and raises <see cref="Saved"/>. Failures show inline and nothing closes.</summary>
        public bool Save()
        {
            var folder = outputFolder.Trim().Trim('"');

            var error = Validate(folder);
            if (error != null)
            {
                OutputFolderError = error;
                return false;
            }

            OutputFolderError = null;

            var settings = new Settings
            {
                OutputFolder = folder,
                AskBeforeReplacing = askBeforeReplacing,
                RightClickContextMenus = rightClickContextMenus,
                UseEnvironmentVariables = useEnvironmentVariables,
                Theme = theme,
                CheckForUpdates = checkForUpdates
            };

            storeChanged = true;
            var message = store.Save(settings);
            if (message != null)
            {
                SaveError = message;
                return false;
            }

            SaveError = null;
            Saved?.Invoke(this, EventArgs.Empty);
            return true;
        }

        private string? Validate(string folder)
        {
            if (folder.Length == 0)
            {
                return EmptyFolderMessage;
            }

            if (!Settings.IsValidOutputFolder(folder))
            {
                return Settings.InvalidOutputFolderMessage;
            }

            return checkFolder(folder);
        }

        private async Task CheckNowAsync()
        {
            if (updates == null)
            {
                return;
            }

            ShowUpdateResult(null);
            UpdateStatusText = CheckingForUpdatesMessage;
            ShowUpdateResult(await updates.CheckNowAsync());
            UpdateChecked?.Invoke(this, EventArgs.Empty);
        }

        private void ShowUpdateResult(UpdateCheckResult? result)
        {
            updateResult = result;
            OnPropertyChanged(nameof(IsUpdateAvailable));
            DownloadUpdateCommand.RaiseCanExecuteChanged();

            if (result == null)
            {
                return;
            }

            switch (result.Status)
            {
                case UpdateStatus.Available:
                    UpdateStatusText = UpdateNoticeViewModel.FormatAvailable(result.Latest);
                    break;
                case UpdateStatus.UpToDate:
                    UpdateStatusText = string.Format(UpToDateFormat, CurrentVersion.ToString(3));
                    break;
                default:
                    UpdateStatusText = UpdateCheckFailedMessage;
                    break;
            }
        }

        private void DownloadUpdate()
        {
            if (updateResult?.Status == UpdateStatus.Available)
            {
                openUrl(updateResult.ReleaseUrl);
            }
        }

        private void Choose()
        {
            var picked = pickFolder(outputFolder.Trim());
            if (!string.IsNullOrWhiteSpace(picked))
            {
                OutputFolder = picked;
            }
        }

        private void Launch()
        {
            if (CanLaunch)
            {
                openFolder(outputFolder.Trim());
            }
        }

        private bool SafeDirectoryExists(string folder)
        {
            try
            {
                return directoryExists(folder);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                return false;
            }
        }

        /// <summary>Creates the folder when missing, then writes and deletes a probe file to prove it is writable.</summary>
        public static string? CheckFolder(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);

                var probe = Path.Combine(folder, ".keycuts-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (File.Create(probe, 1, FileOptions.DeleteOnClose))
                {
                }

                return null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException ||
                e is SecurityException || e is ArgumentException)
            {
                return UnwritableFolderMessage;
            }
        }
    }
}
