using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace keycuts.GUI
{
    /// <summary>Last line of defense: an unexpected error is logged and shown instead of killing the app silently.</summary>
    public static class CrashReporter
    {
        public static readonly string DefaultLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "keycuts", "crash.log");

        public static void Install(Application application)
        {
            application.DispatcherUnhandledException += (sender, e) =>
            {
                Report(e.Exception);
                e.Handled = true;
            };

            // Fire-and-forget reloads land here; nothing is waiting on them, so logging is enough
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Log(DefaultLogPath, e.Exception);
                e.SetObserved();
            };

            // Non-UI threads can't be rescued, but the log survives the exit
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception exception)
                {
                    Log(DefaultLogPath, exception);
                }
            };
        }

        public static void Report(Exception exception)
        {
            var logged = Log(DefaultLogPath, exception);
            var details = logged ? $"Details were saved to {DefaultLogPath}" : exception.Message;

            MessageBox.Show($"Something went wrong in keycuts.\n\n{details}", "keycuts",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>Appends to the log. Returns false instead of throwing, since this runs while handling a failure.</summary>
        public static bool Log(string logPath, Exception exception)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, $"{DateTime.Now:O}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");

                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
