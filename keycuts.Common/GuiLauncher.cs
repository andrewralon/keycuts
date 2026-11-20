using System;
using System.Diagnostics;
using System.IO;

namespace keycuts.Common
{
    /// <summary>
    /// What a bare <c>keycuts</c> does: open the app. The CLI sits beside <c>keycuts.GUI.exe</c> in the install folder, so
    /// typing the product's name in Win+R starts the window instead of printing help.
    /// </summary>
    public static class GuiLauncher
    {
        /// <summary>The GUI's executable beside the CLI: <c>keycuts.GUI.exe</c>, or <c>keycuts.GUI</c> in the bundle's <c>Contents/MacOS</c>.</summary>
        public static readonly string FileName = OperatingSystem.IsMacOS() ? "keycuts.GUI" : "keycuts.GUI.exe";

        /// <summary>The app's bundle identifier (its Info.plist, Phase 8).</summary>
        public const string BundleId = "com.teamralon.keycuts";

        /// <summary>
        /// How the GUI at <paramref name="path"/> is started: shell-executed on Windows; on macOS <c>open -b</c> with the bundle id,
        /// so Launch Services starts the app (or brings a running one forward) by identity, not any app named keycuts (correction 7).
        /// </summary>
        public static ProcessStartInfo StartInfo(string path)
        {
            if (!OperatingSystem.IsMacOS())
            {
                return new ProcessStartInfo(path) { UseShellExecute = true };
            }

            var start = new ProcessStartInfo("/usr/bin/open") { UseShellExecute = false };
            start.ArgumentList.Add("-b");
            start.ArgumentList.Add(BundleId);
            return start;
        }

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
