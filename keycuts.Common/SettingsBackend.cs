using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace keycuts.Common
{
    /// <summary>
    /// Where <see cref="RegistryStuff"/>'s settings live, as named values of three kinds: a string, a DWORD and a multi-string.
    /// Only the registry backend exists so far; macOS gets a JSON file with the same value names (docs/macos-port.md, Phase 4.2).
    /// </summary>
    public interface ISettingsBackend
    {
        /// <summary>A string, an int (DWORD) or a string[] (multi-string); null when it's missing or can't be read. Never writes.</summary>
        object Read(string name);

        void WriteString(string name, string value);

        void WriteDword(string name, int value);

        void WriteMultiString(string name, string[] value);

        /// <summary>Best effort: removes those of <paramref name="names"/> that exist, and never throws. A failure leaves them for the next call.</summary>
        void DeleteValues(IEnumerable<string> names);
    }

    /// <summary>The settings key, <c>HKCU\Software\TeamRalon\keycuts</c>. The installer writes and reads some of its values too.</summary>
    public class RegistrySettingsBackend : ISettingsBackend
    {
        private static readonly RegistryKey CurrentUserContext = Registry.CurrentUser;

        public object Read(string name)
        {
            try
            {
                using var appNameKey = CurrentUserContext.OpenSubKey(RegistryStuff.StartPath);

                return appNameKey?.GetValue(name);
            }
            catch (Exception e) when (ContextMenuRegistrar.IsRegistryFailure(e))
            {
                return null;
            }
        }

        public void WriteString(string name, string value)
        {
            Write(name, value, RegistryValueKind.String);
        }

        public void WriteDword(string name, int value)
        {
            Write(name, value, RegistryValueKind.DWord);
        }

        public void WriteMultiString(string name, string[] value)
        {
            Write(name, value, RegistryValueKind.MultiString);
        }

        public void DeleteValues(IEnumerable<string> names)
        {
            try
            {
                using var appNameKey = CurrentUserContext.OpenSubKey(RegistryStuff.StartPath, true);

                if (appNameKey == null)
                {
                    return;
                }

                foreach (var name in names)
                {
                    appNameKey.DeleteValue(name, false);
                }
            }
            catch (Exception e) when (ContextMenuRegistrar.IsRegistryFailure(e))
            {
                // Left for the next call
            }
        }

        private static void Write(string name, object value, RegistryValueKind kind)
        {
            using var appNameKey = CurrentUserContext.CreateSubKey(RegistryStuff.StartPath, true);

            appNameKey.SetValue(name, value, kind);
        }
    }
}
