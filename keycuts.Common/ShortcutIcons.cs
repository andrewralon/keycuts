using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace keycuts.Common
{
    /// <summary>
    /// Points Windows shortcuts to the app at another icon, so the Start menu, the desktop and a pinned taskbar button show the
    /// icon picked in Settings. Only shortcuts whose target is the given exe are changed, so a Debug build never touches the
    /// installed app's shortcuts. The installer is per-user, so these are the user's own files and need no elevation.
    /// </summary>
    public static class ShortcutIcons
    {
        const int ShcneUpdateItem = 0x00002000;
        const uint ShcnfPathW = 0x0005;

        /// <summary>Where the app's shortcuts live: the Start menu (the installer's), the desktop, and the pinned taskbar buttons.</summary>
        public static IReadOnlyList<string> GetDefaultFolders()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Path.Combine(appData, @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"),
            };
        }

        /// <summary>
        /// Sets the icon of every <c>.lnk</c> directly in <paramref name="folders"/> whose target is <paramref name="exePath"/>,
        /// and tells the shell each one changed. Returns the shortcuts it changed; one that already shows the icon is left alone.
        /// Never throws: a folder or shortcut that can't be read or saved is skipped.
        /// </summary>
        public static IReadOnlyList<string> PointAt(IEnumerable<string> folders, string exePath, string iconPath)
        {
            var changed = new List<string>();
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null || string.IsNullOrEmpty(exePath) || string.IsNullOrEmpty(iconPath))
            {
                return changed;
            }

            dynamic shell;
            try
            {
                shell = Activator.CreateInstance(shellType);
            }
            catch (Exception)
            {
                return changed;
            }

            var exe = Path.GetFullPath(exePath);
            var location = iconPath + ",0";

            foreach (var folder in folders.Where(f => !string.IsNullOrEmpty(f)))
            {
                string[] links;
                try
                {
                    links = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.lnk") : Array.Empty<string>();
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (var link in links)
                {
                    try
                    {
                        dynamic shortcut = shell.CreateShortcut(link);
                        string target = shortcut.TargetPath;
                        if (string.IsNullOrEmpty(target) || !string.Equals(Path.GetFullPath(target), exe, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string current = shortcut.IconLocation;
                        if (string.Equals(current, location, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        shortcut.IconLocation = location;
                        shortcut.Save();
                        changed.Add(link);
                        SHChangeNotify(ShcneUpdateItem, ShcnfPathW, link, IntPtr.Zero);
                    }
                    catch (Exception)
                    {
                        // A broken or locked shortcut keeps its icon; the others still change
                    }
                }
            }

            return changed;
        }

        /// <summary>The icon a shortcut shows, as <c>path,index</c>; empty when it can't be read. For tests and diagnostics.</summary>
        public static string GetIconLocation(string link)
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    return string.Empty;
                }

                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(link);
                return (string)shortcut.IconLocation ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern void SHChangeNotify(int eventId, uint flags, string item1, IntPtr item2);
    }
}
