using System;
using System.IO;

namespace keycuts.Common
{
    /// <summary>
    /// What a bare <c>keycuts</c> does: open the app. The CLI sits beside <c>keycuts.GUI.exe</c> in the install folder, so
    /// typing the product's name in Win+R starts the window instead of printing help.
    /// </summary>
    public static class GuiLauncher
    {
        public const string FileName = "keycuts.GUI.exe";

        /// <summary>
        /// Starts the GUI when there are no arguments. Returns null when <paramref name="args"/> isn't empty (the caller
        /// carries on as the command line), <see cref="ExitCode.Success"/> once it started, or
        /// <see cref="ExitCode.CannotFindFile"/> when the GUI isn't beside the CLI.
        /// </summary>
        public static ExitCode? TryLaunch(string[] args, string baseDirectory, Func<string, bool> fileExists, Action<string> start)
        {
            if (args != null && args.Length > 0)
            {
                return null;
            }

            var path = Path.Combine(baseDirectory, FileName);
            if (!fileExists(path))
            {
                return ExitCode.CannotFindFile;
            }

            start(path);
            return ExitCode.Success;
        }
    }
}
