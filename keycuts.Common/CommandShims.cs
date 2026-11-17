using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace keycuts.Common
{
    /// <summary>
    /// The <c>keycuts.cmd</c> and <c>keypaste.cmd</c> commands (#248). They live in the keycuts folder, which is already on PATH,
    /// beside the keycuts they work with, instead of in a <c>bin\</c> of their own. From there <c>%~dp0..\keycuts.exe</c> can't
    /// find the exe, so each reads the install folder the installer records (<c>HKCU\Software\TeamRalon\keycuts</c>,
    /// <c>InstallFolder</c>). That keeps their bytes the same on every machine: the MSI ships them into the keycuts folder, and the
    /// app writes the same bytes there at startup and when Settings changes the folder (<see cref="Ensure"/>), and takes them out
    /// of the old folder (<see cref="RemoveFrom"/>, only files that are exactly these). CRLF, no BOM: cmd reads LF-only scripts badly.
    /// <c>keycuts.bat</c> would beat <c>keycuts.cmd</c> in the same folder (Win+R tries .bat first, #209), so those names stay
    /// refused (<see cref="CommandNames"/>). The Manager lists .bat files only, so it never offers to delete these, and
    /// <see cref="KeycutRepository.DeleteAsync"/> refuses them too.
    /// </summary>
    public static class CommandShims
    {
        public const string Extension = ".cmd";

        /// <summary>The shim file names: <c>keycuts.cmd</c>, <c>keypaste.cmd</c>.</summary>
        public static IReadOnlyList<string> FileNames => CommandNames.All.Select(name => name + Extension).ToList();

        /// <summary>True for a path whose file name is one of the shims, whatever folder it is in.</summary>
        public static bool IsShimFileName(string path)
        {
            var name = Path.GetFileName(path ?? "");
            return FileNames.Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>The shim's text, lines joined with CRLF. <paramref name="command"/> is <c>keycuts</c> or <c>keypaste</c>.</summary>
        public static string Content(string command)
        {
            var paste = string.Equals(command, "keypaste", StringComparison.OrdinalIgnoreCase);
            var lines = new[]
            {
                paste
                    ? "@REM keypaste (#97): \"keycuts paste\" under its old name. keycuts keeps this file in the keycuts folder (#248)."
                    : "@REM keycuts on PATH: keycuts keeps this file in the keycuts folder, which is on PATH (#248).",
                "@REM It runs keycuts.exe from the install folder the installer recorded. Changes to this file are replaced.",
                "@SETLOCAL",
                // A KEYCUTS_DIR the caller has set must not stand in for a missing InstallFolder
                "@SET \"KEYCUTS_DIR=\"",
                "@FOR /F \"skip=2 tokens=2,*\" %%A IN ('REG QUERY \"HKCU\\Software\\TeamRalon\\keycuts\" /v InstallFolder 2^>NUL') DO @SET \"KEYCUTS_DIR=%%B\"",
                "@IF NOT DEFINED KEYCUTS_DIR (ECHO keycuts isn't installed: install it again, or run keycuts.exe from its folder. 1>&2& EXIT /B 2)",
                paste ? "@\"%KEYCUTS_DIR%\\keycuts.exe\" paste %*" : "@\"%KEYCUTS_DIR%\\keycuts.exe\" %*"
            };

            return string.Join("\r\n", lines) + "\r\n";
        }

        /// <summary>The shim's bytes, as they are written: UTF-8 (ASCII in fact), no BOM.</summary>
        public static byte[] Bytes(string command)
        {
            return Encoding.ASCII.GetBytes(Content(command));
        }

        /// <summary>
        /// Writes each shim into <paramref name="folder"/> unless it is already exactly right. A missing folder gets nothing. Never
        /// throws; returns the files written.
        /// </summary>
        public static IReadOnlyList<string> Ensure(string folder)
        {
            var written = new List<string>();

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return written;
            }

            foreach (var command in CommandNames.All)
            {
                var path = Path.Combine(folder, command + Extension);
                var bytes = Bytes(command);

                try
                {
                    if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
                    {
                        continue;
                    }

                    if (File.Exists(path))
                    {
                        File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
                    }

                    // Whole or not at all: a half-written shim would break the command
                    var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    File.WriteAllBytes(temp, bytes);
                    File.Move(temp, path, true);
                    written.Add(path);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
                {
                    Debug.WriteLine($"keycuts: couldn't write \"{path}\": {ex.Message}");
                }
            }

            return written;
        }

        /// <summary>
        /// Whether this copy of the app looks after the shims: only the installed one (<paramref name="runningFolder"/> is the
        /// <paramref name="installFolder"/> the MSI recorded), since the shims run that copy's keycuts.exe, and never under the Debug
        /// folder override, whose folder holds test fixtures.
        /// </summary>
        public static bool ShouldKeep(string installFolder, string runningFolder, bool usesDebugFolder)
        {
            return !usesDebugFolder && NameConflicts.IsSameFolder(installFolder, runningFolder);
        }

        /// <summary>The shims into <paramref name="newFolder"/>, and out of <paramref name="oldFolder"/> when that is another folder.</summary>
        public static void Move(string oldFolder, string newFolder)
        {
            Ensure(newFolder);

            if (!string.IsNullOrWhiteSpace(oldFolder) && !NameConflicts.IsSameFolder(oldFolder, newFolder))
            {
                RemoveFrom(oldFolder);
            }
        }

        /// <summary>
        /// The app's step at startup and on a folder change: the shims into <paramref name="folder"/>, out of the folder they were
        /// last put in (<paramref name="recordedFolder"/>, kept in HKCU), then <paramref name="record"/> the new one when it changed.
        /// With nothing recorded yet (the MSI put them in, and the app hasn't moved them since), a folder change cleans the folder
        /// it moved from, <paramref name="previousFolder"/>.
        /// </summary>
        public static void Keep(string recordedFolder, string folder, Action<string> record, string previousFolder = null)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return;
            }

            var lastFolder = string.IsNullOrWhiteSpace(recordedFolder) ? previousFolder : recordedFolder;
            Move(lastFolder, folder);

            if (record != null && (string.IsNullOrWhiteSpace(recordedFolder) || !NameConflicts.IsSameFolder(recordedFolder, folder)))
            {
                record(folder);
            }
        }

        /// <summary>
        /// Deletes the shims from a folder keycuts no longer uses, but only files that are exactly the shims: a <c>keycuts.cmd</c> of
        /// the user's own stays. Never throws; returns the files deleted.
        /// </summary>
        public static IReadOnlyList<string> RemoveFrom(string folder)
        {
            var removed = new List<string>();

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return removed;
            }

            foreach (var command in CommandNames.All)
            {
                var path = Path.Combine(folder, command + Extension);

                try
                {
                    if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(Bytes(command)))
                    {
                        File.Delete(path);
                        removed.Add(path);
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
                {
                    Debug.WriteLine($"keycuts: couldn't remove \"{path}\": {ex.Message}");
                }
            }

            return removed;
        }
    }
}
