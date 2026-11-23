using System;
using System.Runtime.Versioning;
using keycuts.GUI.ViewModels;
using Microsoft.Win32;

namespace keycuts.GUI
{
    /// <summary>Where Windows keeps the login items (issue #307): the Run key, and the Startup apps switch beside it.</summary>
    public interface IStartupKeys
    {
        /// <summary>The command the Run key has for keycuts; null when there is none.</summary>
        string? GetCommand();

        void SetCommand(string? command);

        /// <summary>The first byte of Startup apps' entry (<c>StartupApproved\Run</c>); null when it has none. An odd one means switched off.</summary>
        byte? GetApproval();

        void ClearApproval();
    }

    /// <summary>"Open at login" on Windows (issue #307): keycuts starts in the tray with no window (<see cref="StartupArgument"/>).</summary>
    public sealed class WindowsLoginItem : ILoginItem
    {
        /// <summary>The argument a start at login carries, so it opens in the tray with no window. Windows can pass one, so nothing is guessed from timing.</summary>
        public const string StartupArgument = "--startup";

        private readonly IStartupKeys keys;

        private readonly string exePath;

        public WindowsLoginItem(IStartupKeys keys, string exePath)
        {
            this.keys = keys;
            this.exePath = exePath;
        }

        public bool IsOn
        {
            get { return keys.GetCommand() != null; }
        }

        /// <summary>Task Manager's Startup apps (Settings → Apps → Startup) has it switched off.</summary>
        public bool NeedsApproval
        {
            get { return IsOn && keys.GetApproval() is byte first && (first & 1) == 1; }
        }

        public string? Set(bool on)
        {
            try
            {
                if (on)
                {
                    keys.SetCommand(Command(exePath));
                    // Turning it on in keycuts is the user's choice again, whatever Startup apps said before
                    keys.ClearApproval();
                }
                else
                {
                    keys.SetCommand(null);
                    keys.ClearApproval();
                }

                return null;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is System.Security.SecurityException || ex is System.IO.IOException)
            {
                return "Windows refused: " + ex.Message;
            }
        }

        /// <summary>The Run key's value: the exe in quotes, then the argument.</summary>
        public static string Command(string exePath)
        {
            return "\"" + exePath + "\" " + StartupArgument;
        }
    }

    /// <summary>The real keys, in HKCU.</summary>
    [SupportedOSPlatform("windows")]
    public sealed class RegistryStartupKeys : IStartupKeys
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

        /// <summary>The value's name in both keys, as in Task Manager's list.</summary>
        public const string ValueName = "keycuts";

        public string? GetCommand()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) as string;
        }

        public void SetCommand(string? command)
        {
            if (command == null)
            {
                using var existing = Registry.CurrentUser.OpenSubKey(RunKey, true);
                existing?.DeleteValue(ValueName, false);
                return;
            }

            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            key.SetValue(ValueName, command, RegistryValueKind.String);
        }

        public byte? GetApproval()
        {
            using var key = Registry.CurrentUser.OpenSubKey(ApprovedKey);
            return key?.GetValue(ValueName) is byte[] { Length: > 0 } bytes ? bytes[0] : null;
        }

        public void ClearApproval()
        {
            using var key = Registry.CurrentUser.OpenSubKey(ApprovedKey, true);
            key?.DeleteValue(ValueName, false);
        }
    }
}
