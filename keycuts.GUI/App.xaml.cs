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
            var welcome = new ViewModels.WelcomeViewModel(
                appPath,
                Common.RegistryStuff.GetWelcomeDismissed,
                Common.RegistryStuff.SetWelcomeDismissed,
                (destination, name, ct) => Common.KeycutCreator.CreateAsync(destination, name, null, ct));

            var shell = new ViewModels.ShellViewModel(
                () => Common.KeycutRepository.CreateDefault().CountAsync(),
                ct => Common.KeycutRepository.CreateDefault().LoadAllAsync(ct),
                (keycuts, ct) => Common.KeycutRepository.CreateDefault().CheckDestinationsAsync(keycuts, ct),
                new ViewModels.SystemKeycutActions(),
                welcome);
            var mainWindow = new MainWindow(shell);

            if (args.Any())
            {
                // Explorer "keycut this!": pre-fill the destination; the creator view focuses the name field on load
                MainFormLogic mainFormLogic = new MainFormLogic();
                mainFormLogic.HandleNewDestination(shell.Creator, args[0]);
            }

            mainWindow.Show();
        }
    }
}
