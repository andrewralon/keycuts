using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace keycuts.Common
{
    /// <summary>
    /// The Finder Quick Action "keycut this!" on macOS (Phase 7): a <c>.workflow</c> in <c>~/Library/Services</c> that runs
    /// <c>open -b com.teamralon.keycuts</c> on the selected items, which the running app receives and opens in the creator. This
    /// copies it in and takes it out. Plain file work, so it is tested on every platform with a temp home; <c>pbs</c> is told to
    /// look again through <paramref name="refreshServices"/>.
    /// </summary>
    public class QuickActionInstaller
    {
        /// <summary>The workflow's folder name, which is also the name in Finder's menu.</summary>
        public const string WorkflowName = "keycut this!.workflow";

        private readonly string _home;
        private readonly string _source;
        private readonly Action _refreshServices;

        /// <param name="home">The user's home folder: the action goes in <c>Library/Services</c> under it.</param>
        /// <param name="source">The <c>keycut this!.workflow</c> folder shipped with the app.</param>
        /// <param name="refreshServices">Asks macOS to rescan its services (<c>pbs -flush</c>); null does nothing.</param>
        public QuickActionInstaller(string home, string source, Action refreshServices = null)
        {
            _home = home;
            _source = source;
            _refreshServices = refreshServices ?? (() => { });
        }

        public static QuickActionInstaller CreateDefault()
        {
            return new QuickActionInstaller(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Path.Combine(AppContext.BaseDirectory, "QuickAction", WorkflowName),
                FlushServices);
        }

        public string InstalledPath
        {
            get { return Path.Combine(_home, "Library", "Services", WorkflowName); }
        }

        public bool IsInstalled
        {
            get { return Directory.Exists(InstalledPath); }
        }

        /// <summary>Installed and the same files as the ones shipped with this app, so an upgrade's new workflow replaces an old copy.</summary>
        public bool IsUpToDate
        {
            get { return IsInstalled && Directory.Exists(_source) && SameFiles(_source, InstalledPath); }
        }

        /// <summary>The app's start on macOS: installs the action, or brings an older copy up to date. False when it couldn't (or has nothing to install).</summary>
        public bool EnsureInstalled()
        {
            return IsUpToDate || Install();
        }

        private static bool SameFiles(string a, string b)
        {
            var filesA = Directory.GetFiles(a, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(a, f)).OrderBy(f => f, StringComparer.Ordinal).ToArray();
            var filesB = Directory.GetFiles(b, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(b, f)).OrderBy(f => f, StringComparer.Ordinal).ToArray();

            return filesA.SequenceEqual(filesB) && filesA.All(f => File.ReadAllBytes(Path.Combine(a, f)).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(b, f))));
        }

        /// <summary>Puts the workflow in place of any copy there; false when this app has no workflow to copy or the copy failed.</summary>
        public bool Install()
        {
            try
            {
                if (!Directory.Exists(_source))
                {
                    return false;
                }

                if (Directory.Exists(InstalledPath))
                {
                    Directory.Delete(InstalledPath, true);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(InstalledPath));
                Copy(_source, InstalledPath);
                _refreshServices();
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>Takes the workflow out; true when it is gone, also when it never was there.</summary>
        public bool Uninstall()
        {
            try
            {
                if (Directory.Exists(InstalledPath))
                {
                    Directory.Delete(InstalledPath, true);
                    _refreshServices();
                }

                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static void Copy(string from, string to)
        {
            Directory.CreateDirectory(to);

            foreach (var file in Directory.GetFiles(from))
            {
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
            }

            foreach (var folder in Directory.GetDirectories(from))
            {
                Copy(folder, Path.Combine(to, Path.GetFileName(folder)));
            }
        }

        /// <summary><c>pbs -flush</c>, so Finder lists the action (or stops listing it) without a logout. Best effort.</summary>
        private static void FlushServices()
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo("/System/Library/CoreServices/pbs", "-flush")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                process?.WaitForExit(5000);
            }
            catch (Exception)
            {
                // The action still appears after the next login
            }
        }
    }
}
