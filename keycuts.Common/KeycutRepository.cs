using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace keycuts.Common
{
    public enum KeycutRepositoryError
    {
        NotFound,
        AccessDenied,
        InvalidPath,
        InUse,
        Unknown,
        AlreadyExists,
        InvalidName
    }

    public class KeycutRepositoryException : Exception
    {
        public KeycutRepositoryError Error { get; }

        public KeycutRepositoryException(KeycutRepositoryError error, string message, Exception inner = null)
            : base(message, inner)
        {
            Error = error;
        }
    }

    /// <summary>Reads and deletes the keycut .bat files in one output folder.</summary>
    public class KeycutRepository
    {
        public static readonly TimeSpan DefaultCheckTimeout = TimeSpan.FromSeconds(2);

        private const int MaxParallelChecks = 8;

        private readonly string _outputFolder;

        public KeycutRepository(string outputFolder)
        {
            _outputFolder = outputFolder;
        }

        public string OutputFolder => _outputFolder;

        /// <summary>Production repository for the configured output folder.</summary>
        public static KeycutRepository CreateDefault()
        {
            return new KeycutRepository(new Runner().GetOutputFolder());
        }

        /// <summary>
        /// Loads every parseable keycut. A missing folder yields an empty list (it is never created here).
        /// </summary>
        /// <exception cref="KeycutRepositoryException">The folder can't be read.</exception>
        public Task<IReadOnlyList<Keycut>> LoadAllAsync(CancellationToken ct = default)
        {
            return Task.Run(() => LoadAll(ct), ct);
        }

        public bool Exists(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                var fileName = name + ShortcutFile.Extension;

                return Directory.Exists(_outputFolder) &&
                    Directory.EnumerateFiles(_outputFolder, "*" + ShortcutFile.Extension)
                        .Any(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<int> CountAsync(CancellationToken ct = default)
        {
            var all = await LoadAllAsync(ct).ConfigureAwait(false);

            return all.Count;
        }

        /// <exception cref="KeycutRepositoryException">The file is already gone, locked, or can't be deleted.</exception>
        public Task DeleteAsync(Keycut keycut, CancellationToken ct = default)
        {
            return Task.Run(() => Delete(keycut), ct);
        }

        /// <summary>
        /// Renames the keycut's file to <paramref name="newName"/>.bat in the same folder and returns the new path. Only the
        /// file name and its <c>REM &lt;shortcut&gt;</c> line change: the file is not re-emitted, so a hand-edited launch line
        /// (an added <c>%*</c>, say) survives. A name that is taken is refused, never overwritten; a change of case only is
        /// allowed. If the comment can't be rewritten (not UTF-8), the rename still stands, since nothing reads that line.
        /// </summary>
        /// <exception cref="KeycutRepositoryException">The name is invalid or taken, or the file is gone, locked, or can't be renamed.</exception>
        public Task<string> RenameAsync(Keycut keycut, string newName, CancellationToken ct = default)
        {
            return Task.Run(() => Rename(keycut.FilePath, newName), ct);
        }

        /// <summary>
        /// Renames each keycut named after a keycuts command (<see cref="CommandNames"/>) to that name plus
        /// <see cref="CommandNames.PreviousSuffix"/> (keypaste_previous, then keypaste_previous2 and so on if that's taken),
        /// so the command on PATH works again. It's renamed like <see cref="RenameAsync"/>, so it still works under the
        /// new name. A file that can't be renamed (locked, read-only) is left as it is. Returns what was renamed.
        /// </summary>
        public IReadOnlyList<RenamedKeycut> MoveAsideCommandNames()
        {
            var renamed = new List<RenamedKeycut>();

            if (string.IsNullOrWhiteSpace(_outputFolder) || !Directory.Exists(_outputFolder))
            {
                return renamed;
            }

            foreach (var command in CommandNames.All)
            {
                try
                {
                    // The file's own spelling, so a "KeyPaste.bat" is reported as it's named
                    var path = Directory.EnumerateFiles(_outputFolder, command + ShortcutFile.Extension)
                        .FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), command, StringComparison.OrdinalIgnoreCase));

                    if (path == null)
                    {
                        continue;
                    }

                    var newName = FreeName(command + CommandNames.PreviousSuffix);

                    if (newName == null)
                    {
                        continue;
                    }

                    Rename(path, newName);
                    renamed.Add(new RenamedKeycut(Path.GetFileNameWithoutExtension(path), newName));
                }
                catch (Exception ex) when (ex is KeycutRepositoryException || ex is IOException || ex is UnauthorizedAccessException)
                {
                    // Left in place; the next start tries again
                }
            }

            return renamed;
        }

        /// <summary><paramref name="baseName"/>, or it with 2, 3 and so on appended, whichever isn't taken; null if all are.</summary>
        private string FreeName(string baseName)
        {
            for (var i = 1; i <= 100; i++)
            {
                var candidate = i == 1 ? baseName : baseName + i;

                if (!File.Exists(Path.Combine(_outputFolder, candidate + ShortcutFile.Extension)))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Sets <see cref="Keycut.DestinationStatus"/> for File/Folder keycuts. URLs, commands and
        /// shell (CLSID) keys are never checked. Each check has its own timeout; on timeout the status stays Unknown.
        /// </summary>
        public Task CheckDestinationsAsync(IEnumerable<Keycut> keycuts, CancellationToken ct = default)
        {
            return CheckDestinationsAsync(keycuts, DefaultCheckTimeout, ct);
        }

        public async Task CheckDestinationsAsync(IEnumerable<Keycut> keycuts, TimeSpan timeout, CancellationToken ct = default)
        {
            var checkable = keycuts.Where(IsCheckable).ToList();

            using (var gate = new SemaphoreSlim(MaxParallelChecks))
            {
                var tasks = checkable.Select(async keycut =>
                {
                    await gate.WaitAsync(ct).ConfigureAwait(false);

                    try
                    {
                        var status = await CheckOneAsync(keycut.Destination, timeout, ct).ConfigureAwait(false);

                        if (status != DestinationStatus.Unknown)
                        {
                            keycut.DestinationStatus = status;
                        }
                    }
                    finally
                    {
                        gate.Release();
                    }
                });

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }

        private IReadOnlyList<Keycut> LoadAll(CancellationToken ct)
        {
            var keycuts = new List<Keycut>();

            if (string.IsNullOrWhiteSpace(_outputFolder) || _outputFolder.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                throw new KeycutRepositoryException(KeycutRepositoryError.InvalidPath, $"Invalid keycuts folder \"{_outputFolder}\".");
            }

            if (!Directory.Exists(_outputFolder))
            {
                return keycuts;
            }

            string[] files;

            try
            {
                files = Directory.GetFiles(_outputFolder, "*" + ShortcutFile.Extension);
            }
            catch (Exception ex)
            {
                throw Translate(ex, $"Can't read keycuts folder \"{_outputFolder}\".");
            }

            foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();

                var keycut = TryParse(file);

                if (keycut != null)
                {
                    keycuts.Add(keycut);
                }
            }

            return keycuts;
        }

        internal static Keycut TryParse(string file)
        {
            try
            {
                var shortcut = new ShortcutFile(file);

                if (string.IsNullOrEmpty(shortcut.Shortcut) ||
                    string.IsNullOrEmpty(shortcut.Command) ||
                    shortcut.Type == ShortcutType.Unknown)
                {
                    Debug.WriteLine($"keycuts: skipped unparseable file \"{file}\"");
                    return null;
                }

                return new Keycut
                {
                    Name = shortcut.Shortcut,
                    Kind = KeycutKinds.FromShortcutType(shortcut.Type),
                    RawType = shortcut.Type,
                    Description = shortcut.Description ?? "",
                    Destination = shortcut.Destination ?? "",
                    FilePath = file
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"keycuts: skipped \"{file}\": {ex.Message}");
                return null;
            }
        }

        private static void Delete(Keycut keycut)
        {
            var path = keycut.FilePath;

            try
            {
                if (!File.Exists(path))
                {
                    throw new KeycutRepositoryException(KeycutRepositoryError.NotFound, $"\"{path}\" no longer exists.");
                }

                File.Delete(path);
            }
            catch (KeycutRepositoryException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw Translate(ex, $"Can't delete \"{path}\".");
            }
        }

        private static readonly System.Text.RegularExpressions.Regex ShortcutRemLine = new System.Text.RegularExpressions.Regex(
            @"^(\s*REM\s+<shortcut>).*?(</shortcut>)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);

        private static string Rename(string path, string newName)
        {
            if (!KeycutCreator.IsValidName(newName))
            {
                throw new KeycutRepositoryException(KeycutRepositoryError.InvalidName, $"\"{newName}\" isn't a valid keycut name.");
            }

            try
            {
                if (!File.Exists(path))
                {
                    throw new KeycutRepositoryException(KeycutRepositoryError.NotFound, $"\"{path}\" no longer exists.");
                }

                var newPath = Path.Combine(Path.GetDirectoryName(path), newName + ShortcutFile.Extension);

                if (string.Equals(newPath, path, StringComparison.Ordinal))
                {
                    return path;
                }

                // Another keycut with that name, unless it's this file under a different case
                if (File.Exists(newPath) && !string.Equals(newPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    throw new KeycutRepositoryException(KeycutRepositoryError.AlreadyExists, $"\"{newName}\" already exists.");
                }

                File.Move(path, newPath);
                TryRewriteShortcutLine(newPath);

                return newPath;
            }
            catch (KeycutRepositoryException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw Translate(ex, $"Can't rename \"{path}\".");
            }
        }

        /// <summary>Points the REM &lt;shortcut&gt; line at the new path, keeping the encoding, BOM and line endings.</summary>
        private static void TryRewriteShortcutLine(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                var preamble = System.Text.Encoding.UTF8.GetPreamble();
                var hasBom = bytes.Length >= preamble.Length && bytes.AsSpan(0, preamble.Length).SequenceEqual(preamble);
                var strict = new System.Text.UTF8Encoding(false, true);
                var text = strict.GetString(bytes, hasBom ? preamble.Length : 0, bytes.Length - (hasBom ? preamble.Length : 0));
                var updated = ShortcutRemLine.Replace(text, match => match.Groups[1].Value + path + match.Groups[2].Value);

                if (updated == text)
                {
                    return;
                }

                var body = strict.GetBytes(updated);
                // Write a temp file beside it and swap it in only once it's complete: writing in place truncates first,
                // so a failed write (a full disk) would leave the keycut empty or half written
                var temp = Path.Combine(Path.GetDirectoryName(path), "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");

                try
                {
                    File.WriteAllBytes(temp, hasBom ? preamble.Concat(body).ToArray() : body);

                    // Replace, not Move: Windows' ReplaceFile keeps the original's identity, so its permissions and
                    // alternate data streams (the "downloaded from the internet" mark) survive. If they can't be kept
                    // it throws, and the original stays as it was.
                    File.Replace(temp, path, null);
                }
                finally
                {
                    if (File.Exists(temp))
                    {
                        File.Delete(temp);
                    }
                }
            }
            catch (Exception ex) when (ex is System.Text.DecoderFallbackException || ex is IOException || ex is UnauthorizedAccessException)
            {
                // The REM line is only a comment, so a file we can't decode or rewrite keeps its old one
                Debug.WriteLine($"keycuts: kept the old <shortcut> line in \"{path}\": {ex.Message}");
            }
        }

        private static bool IsCheckable(Keycut keycut)
        {
            return !string.IsNullOrWhiteSpace(keycut.Destination) &&
                (keycut.RawType == ShortcutType.File ||
                 keycut.RawType == ShortcutType.Folder ||
                 keycut.RawType == ShortcutType.HostsFile);
        }

        private static async Task<DestinationStatus> CheckOneAsync(string destination, TimeSpan timeout, CancellationToken ct)
        {
            var work = Task.Run(() =>
            {
                try
                {
                    var path = Environment.ExpandEnvironmentVariables(destination);

                    return File.Exists(path) || Directory.Exists(path) ?
                        DestinationStatus.Ok :
                        DestinationStatus.Missing;
                }
                catch (Exception)
                {
                    return DestinationStatus.Unknown;
                }
            });

            var finished = await Task.WhenAny(work, Task.Delay(timeout, ct)).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            return finished == work ? await work.ConfigureAwait(false) : DestinationStatus.Unknown;
        }

        private static KeycutRepositoryException Translate(Exception ex, string message)
        {
            var error = ex switch
            {
                UnauthorizedAccessException => KeycutRepositoryError.AccessDenied,
                FileNotFoundException => KeycutRepositoryError.NotFound,
                DirectoryNotFoundException => KeycutRepositoryError.NotFound,
                PathTooLongException => KeycutRepositoryError.InvalidPath,
                ArgumentException => KeycutRepositoryError.InvalidPath,
                NotSupportedException => KeycutRepositoryError.InvalidPath,
                IOException => KeycutRepositoryError.InUse,
                _ => KeycutRepositoryError.Unknown
            };

            return new KeycutRepositoryException(error, message, ex);
        }
    }
}
