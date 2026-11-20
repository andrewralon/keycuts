using System;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace keycuts.Common
{
    /// <summary>
    /// What the desktop does with a keycut or a target: run it, open it, show it in the file manager, open it in a text
    /// editor. Windows uses the shell, Explorer and Notepad (<see cref="WindowsDesktop"/>); macOS gets <c>open</c> and
    /// <c>/bin/sh</c> (docs/macos-port.md, Phase 4).
    /// </summary>
    public interface IDesktop
    {
        /// <summary>Opens a URL, folder or file the way the desktop would on a double-click.</summary>
        void OpenTarget(string target);

        /// <summary>Runs a keycut file, as Win+R would, with <paramref name="arguments"/> passed on as typed.</summary>
        void RunKeycut(string path, string arguments = "");

        /// <summary>Shows the file in the file manager, selected.</summary>
        void RevealFile(string path);

        /// <summary>Opens the file in a plain text editor.</summary>
        void EditFile(string path);
    }

    /// <summary>The shell, Explorer and Notepad. Each start is built by a public method, so tests can pin it without starting anything.</summary>
    [SupportedOSPlatform("windows")]
    public sealed class WindowsDesktop : IDesktop
    {
        public void OpenTarget(string target)
        {
            Start(OpenTargetStart(target));
        }

        public void RunKeycut(string path, string arguments = "")
        {
            Start(RunKeycutStart(path, arguments));
        }

        public void RevealFile(string path)
        {
            Start(RevealFileStart(path));
        }

        public void EditFile(string path)
        {
            Start(EditFileStart(path));
        }

        public static ProcessStartInfo OpenTargetStart(string target)
        {
            return new ProcessStartInfo(target) { UseShellExecute = true };
        }

        public static ProcessStartInfo RunKeycutStart(string path, string arguments)
        {
            return new ProcessStartInfo(path, arguments ?? "") { UseShellExecute = true };
        }

        /// <summary><c>/select,</c> opens the containing folder with the file highlighted.</summary>
        public static ProcessStartInfo RevealFileStart(string path)
        {
            return new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true };
        }

        public static ProcessStartInfo EditFileStart(string path)
        {
            return new ProcessStartInfo("notepad.exe", "\"" + path + "\"") { UseShellExecute = true };
        }

        // The started process runs on its own; only our handle to it is released
        private static void Start(ProcessStartInfo start)
        {
            Process.Start(start)?.Dispose();
        }
    }

    /// <summary>The desktop until macOS gets its own (Phase 4): every action throws rather than do nothing.</summary>
    public sealed class UnavailableDesktop : IDesktop
    {
        public void OpenTarget(string target)
        {
            throw NotAvailable();
        }

        public void RunKeycut(string path, string arguments = "")
        {
            throw NotAvailable();
        }

        public void RevealFile(string path)
        {
            throw NotAvailable();
        }

        public void EditFile(string path)
        {
            throw NotAvailable();
        }

        private static PlatformNotSupportedException NotAvailable()
        {
            return new PlatformNotSupportedException("Opening files isn't supported on this platform yet.");
        }
    }

    /// <summary>The desktop keycuts are run, opened and shown on.</summary>
    public static class Desktops
    {
        public static readonly IDesktop Current = OperatingSystem.IsWindows()
            ? new WindowsDesktop()
            : new UnavailableDesktop();
    }
}
