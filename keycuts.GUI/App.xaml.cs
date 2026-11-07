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

            var shell = new ViewModels.ShellViewModel(
                () => Common.KeycutRepository.CreateDefault().CountAsync(),
                ct => Common.KeycutRepository.CreateDefault().LoadAllAsync(ct),
                (keycuts, ct) => Common.KeycutRepository.CreateDefault().CheckDestinationsAsync(keycuts, ct),
                new ViewModels.SystemKeycutActions());
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
