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

        /// <summary>
        /// Stores every setting but the output folder, which another window or the CLI may have changed since this one loaded.
        /// Returns a message for the user when something couldn't be saved, otherwise null.
        /// </summary>
        string? SaveOptions(Settings settings);

        /// <summary>Stores the output folder and moves PATH to it; says whether the folder itself was stored, apart from any message.</summary>
        FolderSaveResult SaveOutputFolder(string folder);
    }

    public class RunnerSettingsStore : ISettingsStore
    {
        public Settings Load()
        {
            var settings = new Settings();
            settings.LoadSettings();
            return settings;
        }

        public string? SaveOptions(Settings settings)
        {
            return settings.SaveOptions();
        }

        public FolderSaveResult SaveOutputFolder(string folder)
        {
            return new Settings { OutputFolder = folder }.SaveOutputFolder();
        }
    }

    /// <summary>
    /// State and rules of the Settings screen (#218). Every change is stored the moment it is made: a toggle, the theme and the
    /// icon at once, the output folder when it is committed (Enter, leaving the box, or a pick from the folder button) and only
    /// after it passes validation. Nothing waits for a Save button, so leaving the screen never loses or undoes anything.
    /// </summary>
    public class SettingsViewModel : ObservableObject
    {
        public const string DefaultHelperText = "New keycuts are saved here.";

        public const string EmptyFolderMessage = "Choose where keycuts are saved.";

        public const string UnwritableFolderMessage = "Choose a folder you have permission to write to.";

        public const string VersionFormat = "Version {0}";

        public const string CheckingForUpdatesMessage = "Checking for updates\u2026";

        public const string UpToDateFormat = "You\u2019re up to date ({0}).";

        public const string UpdateCheckFailedMessage = "Couldn\u2019t check for updates.";

        /// <summary>The note near the bottom of the screen, since there is no Save button.</summary>
        public const string SavedAsYouGoNote = "Changes are saved as you make them.";

        private readonly ISettingsStore store;

        private readonly Func<string, string?> pickFolder;

        private readonly Action<string> openFolder;

        private readonly Func<string, string?> checkFolder;

        private readonly Func<string, bool> directoryExists;

        private readonly UpdateChecker? updates;

        private readonly Action<string> openUrl;

        private UpdateCheckResult? updateResult;

        private string updateStatus = string.Empty;

        private readonly Action<ThemePreference> applyTheme;

        private readonly Func<bool> confirmRestoreDefaults;

        /// <summary>The output folder as this screen last stored it: what Escape in the box goes back to, and what "unchanged" is measured against.</summary>
        private string committedFolder;

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
        /// <param name="applyTheme">Shows a theme on every window (the app's is <c>ThemeManager.SetPreference</c>). Null does nothing, as tests want.</param>
        /// <param name="confirmRestoreDefaults">Asks before restore defaults; true goes ahead. Null always goes ahead.</param>
        public SettingsViewModel(
            ISettingsStore store,
            Func<string, string?>? pickFolder = null,
            Action<string>? openFolder = null,
            Func<string, string?>? checkFolder = null,
            Func<string, bool>? directoryExists = null,
            UpdateChecker? updates = null,
            Action<string>? openUrl = null,
            IAppIconService? appIcon = null,
            Action<ThemePreference>? applyTheme = null,
            Func<bool>? confirmRestoreDefaults = null)
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
            this.applyTheme = applyTheme ?? (_ => { });
            this.confirmRestoreDefaults = confirmRestoreDefaults ?? (() => true);

            ChooseCommand = new RelayCommand(Choose);
            LaunchCommand = new RelayCommand(Launch, () => CanLaunch);
            CheckNowCommand = new AsyncRelayCommand(CheckNowAsync, () => updates != null);
            DownloadUpdateCommand = new RelayCommand(DownloadUpdate, () => IsUpdateAvailable);
            RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);
            ShowWelcomeCommand = new RelayCommand(() => ShowWelcomeRequested?.Invoke(this, EventArgs.Empty));

            var loaded = store.Load();
            outputFolder = loaded.OutputFolder ?? string.Empty;
            committedFolder = outputFolder;
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

        /// <summary>
        /// Raised after every store, a partial failure included (the values may be stored even when PATH or the menu wasn't
        /// updated), so the rest of the app can follow: the environment-variables preview and the update line.
        /// </summary>
        public event EventHandler? Stored;

        /// <summary>Raised after a new output folder was stored; the keycut count is the new folder's.</summary>
        public event EventHandler? OutputFolderChanged;

        /// <summary>Show welcome: the shell goes back to the creator with the first-run panel showing.</summary>
        public event EventHandler? ShowWelcomeRequested;

        /// <summary>Puts every setting on the page back as a new install has it, after asking. Never the output folder.</summary>
        public RelayCommand RestoreDefaultsCommand { get; }

        public RelayCommand ShowWelcomeCommand { get; }

        /// <summary>
        /// What the screen's footer shows (the count and the update line): the creator's view model, set by the shell, so all three
        /// screens show the same footer. Null leaves the footer empty.
        /// </summary>
        public object? Footer { get; set; }

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

        /// <summary>A problem storing a change, such as PATH not updating. Shown on the page until the next change.</summary>
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
            set
            {
                if (SetProperty(ref askBeforeReplacing, value))
                {
                    Store();
                }
            }
        }

        public bool RightClickContextMenus
        {
            get { return rightClickContextMenus; }
            set
            {
                if (SetProperty(ref rightClickContextMenus, value))
                {
                    Store();
                }
            }
        }

        /// <summary>Save new file and folder keycuts as %USERPROFILE%\... and the like (#31). Shift flips it per keycut.</summary>
        public bool UseEnvironmentVariables
        {
            get { return useEnvironmentVariables; }
            set
            {
                if (SetProperty(ref useEnvironmentVariables, value))
                {
                    Store();
                }
            }
        }

        public ThemePreference Theme
        {
            get { return theme; }
            set
            {
                if (SetProperty(ref theme, value))
                {
                    applyTheme(value);
                    Store();
                }
            }
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

        /// <summary>The icon. Setting it applies and saves it at once, as every setting is; closing the picker keeps it.</summary>
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

        /// <summary>The page (and the tabs, through the shell) is disabled while the picker is open, so Tab stays in it.</summary>
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
            set
            {
                if (SetProperty(ref checkForUpdates, value))
                {
                    Store();
                }
            }
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
        /// Stores the folder in the box, after the same validation as before: Enter, leaving the box, and a pick from the folder
        /// button call this. An invalid folder shows its error and stores nothing; the folder already stored is kept. An unchanged
        /// folder stores nothing. Returns false when the folder was refused.
        /// </summary>
        public bool CommitOutputFolder()
        {
            var folder = outputFolder.Trim().Trim('"');

            if (folder.Length > 0 && string.Equals(folder, committedFolder, StringComparison.OrdinalIgnoreCase))
            {
                OutputFolderError = null;
                return true;
            }

            var error = Validate(folder);
            if (error != null)
            {
                OutputFolderError = error;
                return false;
            }

            // The folder alone: the other settings were stored as they changed. The store says whether the folder was stored, apart
            // from any message: PATH can fail after it is, and then it still counts. When it wasn't, the stored folder is still the
            // old one, and the box keeps the new text as an edit that Enter tries again.
            OutputFolder = folder;
            var result = store.SaveOutputFolder(folder);
            SaveError = result.Message;

            if (!result.Stored)
            {
                return false;
            }

            committedFolder = folder;
            OnPropertyChanged(nameof(HasUncommittedFolder));
            Stored?.Invoke(this, EventArgs.Empty);
            OutputFolderChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>Whether the folder box holds something other than the stored folder: an edit not committed yet.</summary>
        public bool HasUncommittedFolder
        {
            get { return !string.Equals(outputFolder, committedFolder, StringComparison.Ordinal); }
        }

        /// <summary>
        /// Escape in the folder box: puts the stored folder back and clears its error, storing nothing. Returns false when there
        /// was no edit to drop, so Escape can go on to leave the screen.
        /// </summary>
        public bool RevertOutputFolder()
        {
            if (!HasUncommittedFolder && !HasOutputFolderError)
            {
                return false;
            }

            OutputFolder = committedFolder;
            OutputFolderError = null;
            return true;
        }

        /// <summary>
        /// Stores every value but the output folder, which is stored only when committed: a toggle never writes back a folder that
        /// another window or the CLI changed meanwhile. A partial failure shows on the page (<see cref="SaveError"/>).
        /// </summary>
        private void Store()
        {
            var settings = new Settings
            {
                AskBeforeReplacing = askBeforeReplacing,
                RightClickContextMenus = rightClickContextMenus,
                UseEnvironmentVariables = useEnvironmentVariables,
                Theme = theme,
                CheckForUpdates = checkForUpdates
            };

            SaveError = store.SaveOptions(settings);
            Stored?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Every setting on the page back to a new install's value, stored at once: the toggles, the theme and the icon. The output
        /// folder is never touched (so neither is PATH), and an unsaved edit in its box stays as it is.
        /// </summary>
        private void RestoreDefaults()
        {
            if (!confirmRestoreDefaults())
            {
                return;
            }

            var defaults = Settings.Defaults();
            askBeforeReplacing = defaults.AskBeforeReplacing;
            rightClickContextMenus = defaults.RightClickContextMenus;
            useEnvironmentVariables = defaults.UseEnvironmentVariables;
            checkForUpdates = defaults.CheckForUpdates;
            var themeChanged = theme != defaults.Theme;
            theme = defaults.Theme;

            OnPropertyChanged(nameof(AskBeforeReplacing));
            OnPropertyChanged(nameof(RightClickContextMenus));
            OnPropertyChanged(nameof(UseEnvironmentVariables));
            OnPropertyChanged(nameof(CheckForUpdates));
            OnPropertyChanged(nameof(Theme));

            if (themeChanged)
            {
                applyTheme(theme);
            }

            var iconChanged = selectedIcon.Choice != Settings.DefaultAppIcon;
            SelectedIcon = AppIcons.Get(Settings.DefaultAppIcon);
            Store();

            // An icon that couldn't be saved says so in the picker, which is closed now: the page says it too, after any other problem
            if (iconChanged && HasIconError)
            {
                SaveError = SaveError == null ? IconError : SaveError + Environment.NewLine + IconError;
            }
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
                CommitOutputFolder();
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
