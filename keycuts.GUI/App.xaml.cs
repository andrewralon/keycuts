using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace keycuts.Common
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        void App_Startup(object sender, StartupEventArgs e)
        {
            var mainWindow = new MainWindow();

            if (e.Args.Any())
            {
                mainWindow.Destination = e.Args[0];
            }

            mainWindow.Show();
        }
    }
}
