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
            Themes.ThemeManager.Initialize(this);

            var shell = new ViewModels.ShellViewModel(() => Common.KeycutRepository.CreateDefault().CountAsync());
            var mainWindow = new MainWindow(shell);

            if (e.Args.Any())
            {
                // Explorer "keycut this!": pre-fill the destination; the creator view focuses the name field on load
                MainFormLogic mainFormLogic = new MainFormLogic();
                mainFormLogic.HandleNewDestination(shell.Creator, e.Args[0]);
            }

            mainWindow.Show();
        }
    }
}
