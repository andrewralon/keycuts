using System;
using Avalonia;

namespace keycuts.GUI
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>().UsePlatformDetect();
        }
    }
}
