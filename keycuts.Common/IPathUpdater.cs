using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace keycuts.Common
{
    /// <summary>Seam over the PATH update that happens when the output folder changes.</summary>
    public interface IPathUpdater
    {
        /// <summary>Makes sure <paramref name="newPath"/> is on the PATH (replacing <paramref name="oldPath"/> if present). False on failure.</summary>
        bool AddToOrReplace(string oldPath, string newPath);
    }

    /// <summary>Whole-entry edits of a semicolon-separated PATH value. Unexpanded entries are left exactly as written.</summary>
    public static class PathList
    {
        public static bool Contains(string pathValue, string entry)
        {
            return Split(pathValue).Any(existing => IsSameEntry(existing, entry));
        }

        /// <summary>Removes <paramref name="oldEntry"/> and appends <paramref name="newEntry"/> unless it is null or already there.</summary>
        public static string AddOrReplace(string pathValue, string oldEntry, string newEntry)
        {
            var entries = Split(pathValue).Where(existing => oldEntry == null || !IsSameEntry(existing, oldEntry)).ToList();

            if (newEntry != null && !entries.Any(existing => IsSameEntry(existing, newEntry)))
            {
                entries.Add(newEntry);
            }

            return string.Join(";", entries);
        }

        public static bool IsSameEntry(string left, string right)
        {
            return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> Split(string pathValue)
        {
            return (pathValue ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static string Normalize(string entry)
        {
            return (entry ?? string.Empty).Trim().TrimEnd('\\');
        }
    }

    /// <summary>
    /// Updates the current user's PATH, which needs no elevation. Never throws; failures return false.
    /// Goes through the registry because Environment.SetEnvironmentVariable would expand %VARS% and rewrite the value as REG_SZ.
    /// </summary>
    public class UserPathUpdater : IPathUpdater
    {
        private const string EnvironmentKey = "Environment";

        private const string PathValueName = "Path";

        public bool AddToOrReplace(string oldPath, string newPath)
        {
            if (PathList.IsSameEntry(oldPath, newPath))
            {
                return true;
            }

            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(EnvironmentKey, true);

                var current = key.GetValue(PathValueName, string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                var machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
                var entryToAdd = PathList.Contains(machinePath, newPath) ? null : newPath;
                var updated = PathList.AddOrReplace(current, oldPath, entryToAdd);

                if (updated != PathList.AddOrReplace(current, null, null))
                {
                    key.SetValue(PathValueName, updated, RegistryValueKind.ExpandString);
                    BroadcastEnvironmentChange();
                }

                return true;
            }
            catch (Exception e) when (ContextMenuRegistrar.IsRegistryFailure(e))
            {
                return false;
            }
        }

        /// <summary>
        /// Whether <paramref name="folder"/> is on the user or machine PATH as stored in the registry, which is what Win+R
        /// sees (this process's own PATH can be stale). Entries are compared both as written and with %VARS% expanded.
        /// If the PATH can't be read it answers true, so nobody is asked to fix a problem that may not exist.
        /// </summary>
        public static bool IsOnPath(string folder)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(EnvironmentKey);

                var userPath = key?.GetValue(PathValueName, string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                var machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);

                return IsOnPath(folder, userPath, machinePath);
            }
            catch (Exception e) when (ContextMenuRegistrar.IsRegistryFailure(e))
            {
                return true;
            }
        }

        /// <summary>The pure part of <see cref="IsOnPath(string)"/>: checks both PATH values, as written and expanded.</summary>
        public static bool IsOnPath(string folder, string userPath, string machinePath)
        {
            foreach (var pathValue in new[] { userPath, machinePath })
            {
                if (PathList.Contains(pathValue, folder) ||
                    PathList.Contains(Environment.ExpandEnvironmentVariables(pathValue ?? string.Empty), folder))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Tells Explorer to reload its environment, so Win+R sees the change without a new login.</summary>
        private static void BroadcastEnvironmentChange()
        {
            SendMessageTimeout(HwndBroadcast, WmSettingChange, IntPtr.Zero, EnvironmentKey, SmtoAbortIfHung, 1000, out _);
        }

        #region External Methods

        private static readonly IntPtr HwndBroadcast = new IntPtr(0xffff);

        private const uint WmSettingChange = 0x001A;

        private const uint SmtoAbortIfHung = 0x0002;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam,
            uint flags, uint timeout, out IntPtr result);

        #endregion External Methods
    }

    /// <summary>Leaves the PATH alone. Used by tests.</summary>
    public class NoOpPathUpdater : IPathUpdater
    {
        public bool AddToOrReplace(string oldPath, string newPath)
        {
            return true;
        }
    }
}
