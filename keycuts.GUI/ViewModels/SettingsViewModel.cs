using System;
using System.IO;
using System.Security;
using keycuts.Common;
using keycuts.GUI.Mvvm;

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
    /// closing the window any other way (Cancel, Escape) leaves the store untouched.
    /// </summary>
    public class SettingsViewModel : ObservableObject
    {
        public const string DefaultHelperText = "New .bat files will be saved here.";

        public const string EmptyFolderMessage = "Choose where shortcuts are saved.";

        public const string UnwritableFolderMessage = "Choose a folder you have permission to write to.";

        private readonly ISettingsStore store;

        private readonly Func<string, string?> pickFolder;

        private readonly Action<string> openFolder;

        private readonly Func<string, string?> checkFolder;

        private readonly Func<string, bool> directoryExists;

        private bool storeChanged;

        private string outputFolder = string.Empty;

        private string? outputFolderError;

        private string? saveError;

        private bool forceOverwrite;

        private bool rightClickContextMenus;

        private bool useEnvironmentVariables;

        private ThemePreference theme;

        /// <param name="pickFolder">Shows a folder picker seeded with the given folder; null when cancelled.</param>
        /// <param name="openFolder">Opens the folder in Explorer.</param>
        /// <param name="checkFolder">Creates the folder if needed and proves it is writable; returns an error message or null. Tests use temp folders.</param>
        /// <param name="directoryExists">Decides whether Launch is enabled.</param>
        public SettingsViewModel(
            ISettingsStore store,
            Func<string, string?>? pickFolder = null,
            Action<string>? openFolder = null,
            Func<string, string?>? checkFolder = null,
            Func<string, bool>? directoryExists = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.pickFolder = pickFolder ?? (_ => null);
            this.openFolder = openFolder ?? (_ => { });
            this.checkFolder = checkFolder ?? CheckFolder;
            this.directoryExists = directoryExists ?? Directory.Exists;

            ChooseCommand = new RelayCommand(Choose);
            LaunchCommand = new RelayCommand(Launch, () => CanLaunch);

            var loaded = store.Load();
            outputFolder = loaded.OutputFolder ?? string.Empty;
            forceOverwrite = loaded.ForceOverwrite;
            rightClickContextMenus = loaded.RightClickContextMenus;
            useEnvironmentVariables = loaded.UseEnvironmentVariables;
            theme = loaded.Theme;
        }

        /// <summary>Raised after a successful save; the window closes with a true result.</summary>
        public event EventHandler? Saved;

        public RelayCommand ChooseCommand { get; }

        public RelayCommand LaunchCommand { get; }

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

        public bool ForceOverwrite
        {
            get { return forceOverwrite; }
            set { SetProperty(ref forceOverwrite, value); }
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
                ForceOverwrite = forceOverwrite,
                RightClickContextMenus = rightClickContextMenus,
                UseEnvironmentVariables = useEnvironmentVariables,
                Theme = theme
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
