using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
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

            // GUI creates never change PATH, so a portable copy needs the welcome to check it. The Debug folder override
            // must never write PATH, so it skips the check (null means "on PATH").
            var usesDebugFolder = Common.DebugOverrides.GetOutputFolder() != null;
            // Off the UI thread, since the folder can be a network share that's offline. The count and the loads below
            // wait for it, so the manager and the welcome already see the new names.
            var moveAside = Task.Run(MoveAsideCommandNames);
            var welcome = new ViewModels.WelcomeViewModel(
                Common.RegistryStuff.GetWelcomeDismissed,
                Common.RegistryStuff.SetWelcomeDismissed,
                usesDebugFolder ? null : () => Common.UserPathUpdater.IsOnPath(new Common.Runner().GetOutputFolder()),
                usesDebugFolder ? null : () => new Common.UserPathUpdater().AddToOrReplace(null, new Common.Runner().GetOutputFolder()));

            // #171: the GUI only; the CLI never checks
            var updateNotice = new ViewModels.UpdateNoticeViewModel(Common.UpdateChecker.CreateDefault());

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
                new ViewModels.RegistryDescriptionVisibilityStore());
            // A rename to a name other commands use asks first (#153); a taken keycut name is refused by the rename itself
            shell.Manager.CheckRename = name => new Common.NameCheck(name, null,
                Common.NameConflicts.Find(name, new Common.Runner().GetOutputFolder(), Common.NameConflictEnvironment.GetDefault()));

            // Environment variables in paths (#31): the setting now, and this machine's variables once the window is up
            shell.Creator.UseEnvironmentVariablesSetting = Common.RegistryStuff.GetUseEnvironmentVariables();

            var mainWindow = new MainWindow(shell);

            if (args.Any())
            {
                // Explorer "keycut this!": pre-fill the target; the creator view focuses the name field on load
                MainFormLogic mainFormLogic = new MainFormLogic();
                mainFormLogic.HandleNewTarget(shell.Creator, args[0]);
            }

            mainWindow.Show();
            _ = LoadPathVariablesAsync(shell.Creator);
            _ = ShowCommandNameNoticeAsync(mainWindow, moveAside);
            _ = CheckForUpdatesAsync(updateNotice);
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
        /// Reads this machine's variables off the UI thread: a custom one is checked with <c>Directory.Exists</c>, which can
        /// take a long time for a network share that's offline. The target shows its %VAR% form once they arrive.
        /// </summary>
        private static async Task LoadPathVariablesAsync(ViewModels.CreatorViewModel creator)
        {
            try
            {
                creator.PathVariables = await Task.Run(Common.PathVariables.GetDefault);
            }
            catch (Exception ex)
            {
                // No preview; a create reads them itself
                System.Diagnostics.Debug.WriteLine("keycuts: couldn't read environment variables: " + ex.Message);
            }
        }
    }
}
