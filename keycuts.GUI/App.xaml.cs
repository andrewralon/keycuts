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

            // The self-keycut opens this exe; ProcessPath is null only in unusual hosts
            var appPath = Environment.ProcessPath ?? System.IO.Path.Combine(AppContext.BaseDirectory, "keycuts.GUI.exe");
            // GUI creates never change PATH, so a portable copy needs the welcome to check it. The Debug folder override
            // must never write PATH, so it skips the check (null means "on PATH").
            var usesDebugFolder = Common.DebugOverrides.GetOutputFolder() != null;
            // Off the UI thread, since the folder can be a network share that's offline. The count and the loads below
            // wait for it, so the manager and the welcome already see the new names.
            var moveAside = Task.Run(MoveAsideCommandNames);
            var welcome = new ViewModels.WelcomeViewModel(
                appPath,
                Common.RegistryStuff.GetWelcomeDismissed,
                Common.RegistryStuff.SetWelcomeDismissed,
                (destination, name, ct) => Common.KeycutCreator.CreateAsync(destination, name, null, ct),
                usesDebugFolder ? null : folder => Common.UserPathUpdater.IsOnPath(folder ?? new Common.Runner().GetOutputFolder()),
                usesDebugFolder ? null : folder => new Common.UserPathUpdater().AddToOrReplace(null, folder ?? new Common.Runner().GetOutputFolder()));

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
                (keycuts, ct) => Common.KeycutRepository.CreateDefault().CheckDestinationsAsync(keycuts, ct),
                new ViewModels.SystemKeycutActions(),
                welcome);
            // Environment variables in paths (#31): the setting now, and this machine's variables once the window is up
            shell.Creator.UseEnvironmentVariablesSetting = Common.RegistryStuff.GetUseEnvironmentVariables();

            var mainWindow = new MainWindow(shell);

            if (args.Any())
            {
                // Explorer "keycut this!": pre-fill the destination; the creator view focuses the name field on load
                MainFormLogic mainFormLogic = new MainFormLogic();
                mainFormLogic.HandleNewDestination(shell.Creator, args[0]);
            }

            mainWindow.Show();
            _ = LoadPathVariablesAsync(shell.Creator);
            _ = ShowCommandNameNoticeAsync(mainWindow, moveAside);
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
        /// take a long time for a network share that's offline. The "Saved as" line appears when they arrive.
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
