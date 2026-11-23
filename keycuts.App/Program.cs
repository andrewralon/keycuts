using System;
using Avalonia;

namespace keycuts.GUI
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            // Issue #307: on Windows keycuts stays in the tray, so a second start hands its arguments to the first and ends
            if (OperatingSystem.IsWindows())
            {
                var name = SingleInstance.DefaultName();
                SingleInstance.Current = SingleInstance.TryOwn(name);
                if (SingleInstance.Current == null && SingleInstance.Send(name, args))
                {
                    return 0;
                }

                SingleInstance.Current?.Listen(SingleInstance.Deliver);
            }

            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>().UsePlatformDetect();
        }
    }
}
