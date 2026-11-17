using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace keycuts.GUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        void App_Startup(object sender, StartupEventArgs e)
        {
            CrashReporter.Install(this);

            try
            {
                ShowMainWindow(e.Args);
            }
            catch (Exception exception)
            {
                // Nothing is on screen yet, so staying alive would leave an invisible process behind
                CrashReporter.Report(exception);
                Shutdown(1);
            }
        }

        private void ShowMainWindow(string[] args)
        {
            // Before the first window, so a saved theme never flashes the wrong palette
            Themes.ThemeManager.Initialize(this, Common.RegistryStuff.GetThemePreference());
            // After the theme, which the icon options that follow it read
            Themes.AppIconManager.Initialize(this, Common.RegistryStuff.GetAppIcon(), Common.RegistryStuff.GetAppIconTransparent(), UpdateShortcutIcons);

            // GUI creates never change PATH, so a portable copy needs the welcome to check it. The Debug folder override
            // must never write PATH, so it skips the check (null means "on PATH").
            var usesDebugFolder = Common.DebugOverrides.GetOutputFolder() != null;
            // Off the UI thread, since the folder can be a network share that's offline. The count and the loads below
            // wait for it, so the manager and the welcome already see the new names.
            var moveAside = Task.Run(() =>
            {
                var renamed = MoveAsideCommandNames();
                AddRunCounting();
                return renamed;
            });
            var welcome = new ViewModels.WelcomeViewModel(
                Common.RegistryStuff.GetWelcomeDismissed,
                Common.RegistryStuff.SetWelcomeDismissed,
                usesDebugFolder ? null : folder => Common.UserPathUpdater.IsOnPath(folder ?? new Common.Runner().GetOutputFolder()),
                usesDebugFolder ? null : folder => new Common.UserPathUpdater().AddToOrReplace(null, folder ?? new Common.Runner().GetOutputFolder()));

            // #171: the GUI only; the CLI never checks
            var updateNotice = new ViewModels.UpdateNoticeViewModel(Common.UpdateChecker.CreateDefault());

            // Settings is the window's third screen (#218), made fresh each time it opens. Its pickers and the confirmation of
            // restore defaults are owned by the main window, created below.
            MainWindow? mainWindow = null;
            Func<ViewModels.SettingsViewModel> settingsFactory = () => new ViewModels.SettingsViewModel(
                new ViewModels.RunnerSettingsStore(),
                current => Views.SettingsView.PickFolder(mainWindow, current),
                Views.SettingsView.OpenFolder,
                updates: updateNotice.Checker,
                appIcon: new ViewModels.AppIconService(),
                applyTheme: Themes.ThemeManager.SetPreference,
                confirmRestoreDefaults: () => ConfirmWindow.Ask(mainWindow, ViewModels.ConfirmViewModel.ForRestoreDefaults()));

            var shell = new ViewModels.ShellViewModel(
                async () =>
                {
                    await moveAside;
                    return await Common.KeycutRepository.CreateDefault().CountAsync();
                },
                async ct =>
                {
                    await moveAside.WaitAsync(ct);
                    return await Common.KeycutRepository.CreateDefault().LoadAllAsync(ct);
                },
                (keycuts, ct) => Common.KeycutRepository.CreateDefault().CheckTargetsAsync(keycuts, ct),
                new ViewModels.SystemKeycutActions(),
                welcome,
                updateNotice,
                settingsFactory);
            // A rename to a name other commands use asks first (#153); a taken keycut name is refused by the rename itself
            // Through NameCheck.Run, like the Creator, so a conflict that is a keycut is described as one; askBeforeReplacing is
            // off because the rename refuses a taken keycut name itself
            shell.Manager.CheckRename = name => Common.NameCheck.Run(name, new Common.Runner().GetOutputFolder(), false,
                Common.NameConflictEnvironment.GetDefault());
            // #235: the Runs and Last run headers say when counting started on this machine
            shell.Manager.CountingSince = Common.UsageLog.CreateDefault().CountingSince;
            // #238: a folder chosen in Settings counts its keycuts' runs at once, not from the next start
            shell.PrepareOutputFolder = () => Task.Run(AddRunCounting);

            // Environment variables in paths (#31): the setting now, and this machine's variables once the window is up
            var useEnvironmentVariables = Common.RegistryStuff.GetUseEnvironmentVariables();
            foreach (var form in shell.Forms)
            {
                form.UseEnvironmentVariablesSetting = useEnvironmentVariables;
            }

            mainWindow = new MainWindow(shell);

            if (args.Any())
            {
                // Explorer "keycut this!": pre-fill the target; the creator view focuses the name field on load
                MainFormLogic mainFormLogic = new MainFormLogic();
                mainFormLogic.HandleNewTarget(shell.Creator, args[0]);
            }

            // Before Show, so the title bar and the taskbar never show the exe's own icon first
            Themes.AppIconManager.ApplyTo(mainWindow);
            mainWindow.Show();
            _ = LoadPathVariablesAsync(shell);
            _ = ShowCommandNameNoticeAsync(mainWindow, moveAside);
            _ = CheckForUpdatesAsync(updateNotice);
        }

        private static readonly SemaphoreSlim shortcutLock = new(1, 1);

        private static string? pendingShortcutIcon;

        /// <summary>
        /// Points the app's shortcuts (Start menu, desktop, pinned taskbar) at the icon now shown, off the UI thread. Picks can
        /// come faster than the shortcuts are written, so each run writes only the latest one. Only shortcuts to this exe change,
        /// so a Debug run leaves the installed app's alone.
        /// </summary>
        private static void UpdateShortcutIcons(Themes.AppIconArt art)
        {
            var exe = Environment.ProcessPath;
            var icon = System.IO.Path.Combine(AppContext.BaseDirectory, Themes.AppIcons.FolderName, Themes.AppIcons.GetFileName(art));
            if (exe == null || !System.IO.File.Exists(icon))
            {
                return;
            }

            Interlocked.Exchange(ref pendingShortcutIcon, icon);
            _ = Task.Run(async () =>
            {
                await shortcutLock.WaitAsync();
                try
                {
                    var latest = Interlocked.Exchange(ref pendingShortcutIcon, null);
                    if (latest != null)
                    {
                        Common.ShortcutIcons.PointAt(Common.ShortcutIcons.GetDefaultFolders(), exe, latest);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("keycuts: couldn't update the shortcut icons: " + ex.Message);
                }
                finally
                {
                    shortcutLock.Release();
                }
            });
        }

        /// <summary>After the window is up, so startup never waits on the network. Any failure just leaves the footer line hidden.</summary>
        private static async Task CheckForUpdatesAsync(ViewModels.UpdateNoticeViewModel updateNotice)
        {
            try
            {
                await updateNotice.CheckOnStartupAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("keycuts: couldn't check for updates: " + ex.Message);
            }
        }

        /// <summary>Says what <see cref="MoveAsideCommandNames"/> renamed, once it's done, unless the window is already closed.</summary>
        private static async Task ShowCommandNameNoticeAsync(Window owner, Task<IReadOnlyList<Common.RenamedKeycut>> moveAside)
        {
            // Resumes on the UI thread, which the message box needs
            var renamed = await moveAside;

            if (renamed.Count > 0 && owner.IsLoaded)
            {
                MessageBox.Show(owner, ViewModels.CommandNameNotice.Format(renamed), "keycuts", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Renames keycuts named keycuts or keypaste, which clash with those commands on PATH (bin\keycuts.cmd and
        /// bin\keypaste.cmd). Runs on a worker thread. Never fails: if the folder can't be read, nothing is renamed.
        /// </summary>
        private static IReadOnlyList<Common.RenamedKeycut> MoveAsideCommandNames()
        {
            try
            {
                return Common.KeycutRepository.CreateDefault().MoveAsideCommandNames();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("keycuts: couldn't check for keycuts named after commands: " + ex.Message);
                return Array.Empty<Common.RenamedKeycut>();
            }
        }

        /// <summary>
        /// Gives the keycuts in the folder the lines that count their runs (#187), once per folder (so lines taken out by hand
        /// stay out), and makes the usage folder so counting starts now: at startup, and when Settings changes the folder (#238).
        /// Runs on a worker thread. Never fails. The Debug folder override never writes the registry, so its folder gets the
        /// lines on every start (they're only added once to a file).
        /// </summary>
        private static void AddRunCounting()
        {
            try
            {
                var usesDebugFolder = Common.DebugOverrides.GetOutputFolder() != null;
                Common.KeycutRepository.CreateDefault().AddRunCountingOnce(
                    usesDebugFolder ? null : Common.RegistryStuff.IsRunCountingDone,
                    usesDebugFolder ? null : Common.RegistryStuff.MarkRunCountingDone);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("keycuts: couldn't add run counting: " + ex.Message);
            }
        }

        /// <summary>
        /// Reads this machine's variables off the UI thread: a custom one is checked with <c>Directory.Exists</c>, which can
        /// take a long time for a network share that's offline. The target shows its %VAR% form once they arrive.
        /// </summary>
        private static async Task LoadPathVariablesAsync(ViewModels.ShellViewModel shell)
        {
            try
            {
                var variables = await Task.Run(Common.PathVariables.GetDefault);
                foreach (var form in shell.Forms)
                {
                    form.PathVariables = variables;
                }
            }
            catch (Exception ex)
            {
                // No preview; a create reads them itself
                System.Diagnostics.Debug.WriteLine("keycuts: couldn't read environment variables: " + ex.Message);
            }
        }
    }
}
