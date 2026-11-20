using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using keycuts.Common;
using keycuts.GUI.Mvvm;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// One of the keycuts the welcome panel suggests (#213): a bubble that fills its target and name into the creator's own
    /// fields. <see cref="Label"/> is what the bubble says about the target, <see cref="Kind"/> picks its type badge.
    /// </summary>
    public class WelcomeExample : ObservableObject
    {
        private bool isSelected;

        public WelcomeExample(string name, KeycutKind kind, string label, string target)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Kind = kind;
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        /// <summary>The keycut's name, which goes in the Name field.</summary>
        public string Name { get; }

        public KeycutKind Kind { get; }

        /// <summary>The short text on the bubble: <c>google.com</c>, <c>Downloads</c>, <c>keycuts</c>.</summary>
        public string Label { get; }

        /// <summary>What goes in the Target field: a full URL or path, since that is what the creator accepts.</summary>
        public string Target { get; }

        /// <summary>This bubble is the one whose fields are in the creator. The panel sets it; it has no other effect.</summary>
        public bool IsSelected
        {
            get { return isSelected; }
            set { SetProperty(ref isSelected, value); }
        }

        /// <summary>What a screen reader says for the bubble, e.g. "URL google.com, creates the keycut g".</summary>
        public string AutomationName
        {
            get { return string.Format("{0} {1}, creates the keycut {2}", Controls.TypeBadge.GetLabel(Kind), Label, Name); }
        }

        /// <summary>
        /// The suggestions, in order: a website, a folder every Windows user has, and keycuts itself. The last one is named
        /// <c>kc</c>, not <c>keycuts</c>, which would clash with the CLI's command on PATH (see <see cref="CommandNames"/>).
        /// </summary>
        public static IReadOnlyList<WelcomeExample> CreateDefaults()
        {
            // The app opens itself; ProcessPath is null only in unusual hosts
            var appPath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "keycuts.GUI.exe");

            return new[]
            {
                new WelcomeExample("g", KeycutKind.Url, "google.com", "https://www.google.com"),
                new WelcomeExample("dl", KeycutKind.Folder, "Downloads", GetDownloadsFolder()),
                new WelcomeExample("kc", KeycutKind.File, "keycuts", appPath)
            };
        }

        /// <summary>The user's Downloads folder, wherever it was moved to; the profile's Downloads if Windows can't say.</summary>
        public static string GetDownloadsFolder()
        {
            var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

            try
            {
                var id = new Guid("374DE290-123F-4565-9164-39C4925E467B");
                if (SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out var pointer) == 0 && pointer != IntPtr.Zero)
                {
                    try
                    {
                        var path = Marshal.PtrToStringUni(pointer);
                        return string.IsNullOrWhiteSpace(path) ? fallback : path;
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(pointer);
                    }
                }
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException || e is COMException)
            {
                // Use the profile's folder
            }

            return fallback;
        }

        [DllImport("shell32.dll")]
        private static extern int SHGetKnownFolderPath(ref Guid rfid, uint flags, IntPtr token, out IntPtr path);
    }
}
