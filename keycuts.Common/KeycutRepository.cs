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
        InvalidName,

        /// <summary>The file isn't one the Edit form can rewrite without losing something (#180).</summary>
        NotEditable,

        /// <summary>The keycut's file no longer holds the target and description the Edit form was opened with (#180).</summary>
        Changed,

        /// <summary>The new target of an edit was rejected; <see cref="KeycutRepositoryException.TargetError"/> says why.</summary>
        InvalidTarget
    }

    public class KeycutRepositoryException : Exception
    {
        public KeycutRepositoryError Error { get; }

        /// <summary>For <see cref="KeycutRepositoryError.InvalidTarget"/>: why the target was rejected.</summary>
        public TargetError TargetError { get; }

        public KeycutRepositoryException(KeycutRepositoryError error, string message, Exception inner = null, TargetError targetError = TargetError.None)
            : base(message, inner)
        {
            Error = error;
            TargetError = targetError;
        }
    }

    /// <summary>Reads and deletes the keycut .bat files in one output folder.</summary>
    public class KeycutRepository
    {
        public static readonly TimeSpan DefaultCheckTimeout = TimeSpan.FromSeconds(2);

        private const int MaxParallelChecks = 8;

        private readonly string _outputFolder;

        private readonly IFileRecycler _recycler;

        private readonly Func<NameConflictEnvironment> _programLookup;

        /// <summary>
        /// A repository that deletes files for good. Use <see cref="CreateDefault"/> (or pass a
        /// <see cref="RecycleBinRecycler"/>) for the app, which sends them to the Recycle Bin.
        /// </summary>
        public KeycutRepository(string outputFolder)
            : this(outputFolder, new PermanentDeleter())
        {
        }

        public KeycutRepository(string outputFolder, IFileRecycler recycler)
            : this(outputFolder, recycler, null)
        {
        }

        /// <param name="programLookup">Where to look for a keycut's program when it is a bare name such as <c>chrome</c>
        /// (#203): the folders, PATH and App Paths that Windows searches. Null reads this machine's, only when a check needs
        /// it; tests pass their own so a check never reads the real PATH or registry.</param>
        public KeycutRepository(string outputFolder, IFileRecycler recycler, Func<NameConflictEnvironment> programLookup)
        {
            _outputFolder = outputFolder;
            _recycler = recycler ?? throw new ArgumentNullException(nameof(recycler));
            _programLookup = programLookup ?? NameConflictEnvironment.GetDefault;
        }

        public string OutputFolder => _outputFolder;

        /// <summary>Production repository for the configured output folder; its deletes go to the Recycle Bin (#179).</summary>
        public static KeycutRepository CreateDefault()
        {
            return new KeycutRepository(new Runner().GetOutputFolder(), new RecycleBinRecycler());
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

        /// <summary>
        /// The keycut named <paramref name="name"/> (any case), or null if there is none. A file that exists but can't be
        /// parsed still counts: it comes back with just its name and path, and an empty target.
        /// </summary>
        public Keycut Find(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                var fileName = name + ShortcutFile.Extension;

                if (!Directory.Exists(_outputFolder))
                {
                    return null;
                }

                var file = Directory.EnumerateFiles(_outputFolder, "*" + ShortcutFile.Extension)
                    .FirstOrDefault(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));

                if (file == null)
                {
                    return null;
                }

                return TryParse(file) ?? new Keycut
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    Kind = KeycutKind.File,
                    RawType = ShortcutType.Unknown,
                    Target = "",
                    FilePath = file,
                    CanEditInForm = false,
                    Modified = File.GetLastWriteTime(file)
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<int> CountAsync(CancellationToken ct = default)
        {
            var all = await LoadAllAsync(ct).ConfigureAwait(false);

            return all.Count;
        }

        /// <summary>
        /// Removes the keycut's file (to the Recycle Bin, with <see cref="CreateDefault"/>) and returns what
        /// <see cref="RestoreAsync"/> needs to put it back: the file's exact bytes and write time, read first.
        /// </summary>
        /// <exception cref="KeycutRepositoryException">The file is already gone, locked, or can't be deleted.</exception>
        public Task<DeletedKeycut> DeleteAsync(Keycut keycut, CancellationToken ct = default)
        {
            return Task.Run(() => Delete(keycut), ct);
        }

        /// <summary>
        /// Writes a deleted keycut back exactly as it was, bytes and last write time, and never over a file: if its name
        /// was taken again in the meantime that's refused. The bytes go to a temp file first and are moved into place, so
        /// a failure can't leave a half-written keycut, and a keycut created at that moment is never replaced.
        /// </summary>
        /// <exception cref="KeycutRepositoryException">The name is taken (<see cref="KeycutRepositoryError.AlreadyExists"/>), or the folder is gone or can't be written.</exception>
        public Task RestoreAsync(DeletedKeycut deleted, CancellationToken ct = default)
        {
            return Task.Run(() => Restore(deleted), ct);
        }

        /// <summary>
        /// Whether the Edit form can save this keycut without losing anything (#180): true when the emitter would write
        /// the very same file from what the parser reads. A hand-edited launch line (an added <c>%*</c>), a file the
        /// parser reads wrongly, and the 0.1.x format fail it; so does a file that can't be read.
        /// </summary>
        /// <exception cref="KeycutRepositoryException">The file is already gone (<see cref="KeycutRepositoryError.NotFound"/>), which isn't the same as "not editable".</exception>
        public Task<bool> CanEditInFormAsync(Keycut keycut, CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                if (!File.Exists(keycut.FilePath))
                {
                    throw new KeycutRepositoryException(KeycutRepositoryError.NotFound, $"\"{keycut.FilePath}\" no longer exists.");
                }

                return CanEditInForm(keycut.FilePath);
            }, ct);
        }

        /// <summary>
        /// <see cref="CanEditInFormAsync"/> for a file path. False when the file can't be read, or isn't valid UTF-8: saving
        /// writes the lines again, so a byte that decodes to a replacement character (an ANSI "é" a Notepad save left) would
        /// be lost for good.
        /// </summary>
        public static bool CanEditInForm(string path)
        {
            try
            {
                return CanEditInForm(path, ReadLinesStrict(path));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                // DecoderFallbackException is an ArgumentException
                return false;
            }
        }

        /// <summary>
        /// The file's lines, as <see cref="File.ReadAllLines(string)"/> reads them, except that bytes that aren't valid UTF-8
        /// throw a <see cref="System.Text.DecoderFallbackException"/> instead of becoming U+FFFD, as <see cref="TryRewriteShortcutLine"/> also insists.
        /// </summary>
        private static string[] ReadLinesStrict(string path)
        {
            // The bytes are decoded here, not by File.ReadAllText: that honours a byte-order mark, and then swaps the strict
            // decoder for one that replaces what it can't read (and reads UTF-16 and UTF-32 files, which a save would turn
            // into UTF-8). Only a UTF-8 BOM is stripped; any other BOM is bytes that aren't valid UTF-8, so it throws.
            var bytes = File.ReadAllBytes(path);
            var preamble = System.Text.Encoding.UTF8.GetPreamble();
            var start = bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
            var text = new System.Text.UTF8Encoding(false, true).GetString(bytes, start, bytes.Length - start);
            var lines = new List<string>();

            using (var reader = new StringReader(text))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }

            return lines.ToArray();
        }

        /// <summary>
        /// The test behind <see cref="CanEditInFormAsync"/>, on lines already read: <see cref="Runner.BuildShortcutLines"/>
        /// of the parse must equal the file. Three things are ignored because they change nothing about what runs: the
        /// version in the <c>REM keycuts</c> header, the old <c>&lt;destination&gt;</c> tag name (#195) and the path in
        /// the <c>REM &lt;shortcut&gt;</c> line. A missing description line counts as an empty one, because keycuts written
        /// before descriptions existed (1.0) have none and nothing is lost by adding it. Touches no files.
        /// </summary>
        public static bool CanEditInForm(string path, string[] lines)
        {
            return TryReadEditable(path, lines, out _);
        }

        private static bool TryReadEditable(string path, string[] lines, out ShortcutFile parsed)
        {
            parsed = new ShortcutFile(path, lines);

            return IsEditable(parsed, path, lines);
        }

        /// <summary><see cref="TryReadEditable"/> for lines that are already parsed, so loading a folder parses each file once.</summary>
        private static bool IsEditable(ShortcutFile parsed, string path, string[] lines)
        {
            if (parsed.Type == ShortcutType.Unknown || string.IsNullOrEmpty(parsed.Target))
            {
                return false;
            }

            List<string> emitted;

            try
            {
                var shortcut = Shortcut.FromParts(parsed.Type, parsed.Target, path, string.IsNullOrEmpty(parsed.OpenWithApp) ? null : parsed.OpenWithApp);
                emitted = Runner.BuildShortcutLines(shortcut, parsed.Description);
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            // Written before descriptions: no line to compare, and an empty one would be added, which loses nothing
            if (parsed.Description.Length == 0 && !lines.Any(line => DescriptionRemLine.IsMatch(line)))
            {
                emitted.RemoveAll(line => DescriptionRemLine.IsMatch(line));
            }

            return lines.Select(NormalizeForComparison).SequenceEqual(emitted.Select(NormalizeForComparison), StringComparer.Ordinal);
        }

        private static readonly System.Text.RegularExpressions.Regex DescriptionRemLine = new System.Text.RegularExpressions.Regex(
            @"^\s*REM\s+<description>.*</description>\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex HeaderRemLine = new System.Text.RegularExpressions.Regex(
            @"^REM keycuts(\s.*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex ShortcutRemPath = new System.Text.RegularExpressions.Regex(
            @"^REM <shortcut>.*</shortcut>$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex DestinationRemTag = new System.Text.RegularExpressions.Regex(
            @"^REM <destination>(.*)</destination>$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>A line with what <see cref="CanEditInForm(string, string[])"/> ignores taken out.</summary>
        private static string NormalizeForComparison(string line)
        {
            if (HeaderRemLine.IsMatch(line))
            {
                return "REM keycuts";
            }

            if (ShortcutRemPath.IsMatch(line))
            {
                return "REM <shortcut></shortcut>";
            }

            return DestinationRemTag.Replace(line, "REM <target>$1</target>");
        }

        /// <summary>
        /// Saves the Edit form (#180): writes the keycut again with a new target, name and description. Only a file
        /// <see cref="CanEditInFormAsync"/> accepts is touched, since the file is re-emitted whole. The target is written
        /// exactly as it was when its text is unchanged (no re-reading of the type, so nothing about it can drift);
        /// otherwise it is resolved like a new keycut's, with environment variables if <see cref="KeycutEdit.UseEnvironmentVariables"/>.
        /// A new name is a rename and an edit in one: the file is moved to its new name first (which keeps its permissions and
        /// alternate streams, and never overwrites, so a name that is taken is refused before anything changes), then its
        /// content is swapped in from a temp file. If that fails the name is put back, so a failed write leaves the keycut as it
        /// was. The file must still hold the target and description of the <paramref name="keycut"/> the form was opened for,
        /// or saving would quietly put old values back. Returns the keycut as read back from the file.
        /// </summary>
        /// <exception cref="KeycutRepositoryException">The name or target is invalid (<see cref="KeycutRepositoryError.InvalidName"/>,
        /// <see cref="KeycutRepositoryError.InvalidTarget"/>), the name is taken, the file is gone, can't be edited here
        /// (<see cref="KeycutRepositoryError.NotEditable"/>: not valid UTF-8 included), has changed
        /// (<see cref="KeycutRepositoryError.Changed"/>), or can't be written.</exception>
        public Task<Keycut> UpdateAsync(Keycut keycut, KeycutEdit edit, CancellationToken ct = default)
        {
            return Task.Run(() => Update(keycut, edit), ct);
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
        /// Changes the keycut's description by rewriting just its <c>REM &lt;description&gt;</c> line (#202), as
        /// <see cref="RenameAsync"/> does for <c>REM &lt;shortcut&gt;</c>: the file isn't emitted again, so a hand-edited
        /// launch line survives, and a keycut the Edit form refuses can still have its description changed. The text is
        /// written as the Creator writes one: on one line (a newline becomes a space) with every <c>%</c> doubled (#162).
        /// A file with no description line (keycuts 1.0, or a hand-written launcher) gets one just before its launch line.
        /// The temp file is swapped in with <c>File.Replace</c>, which keeps the file's permissions and alternate streams,
        /// and the file's date changes. A description that is already the one in the file leaves the file alone. Returns
        /// the keycut as read back.
        /// </summary>
        /// <exception cref="KeycutRepositoryException">The file is gone (<see cref="KeycutRepositoryError.NotFound"/>), isn't valid
        /// UTF-8 (<see cref="KeycutRepositoryError.NotEditable"/>: the bytes would be lost), no longer holds the description the
        /// keycut was loaded with (<see cref="KeycutRepositoryError.Changed"/>), or can't be written.</exception>
        public Task<Keycut> SetDescriptionAsync(Keycut keycut, string description, CancellationToken ct = default)
        {
            return Task.Run(() => SetDescription(keycut, description), ct);
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
        /// Sets <see cref="Keycut.TargetStatus"/> for File/Folder keycuts. URLs, commands and
        /// shell (CLSID) keys are never checked. Each check has its own timeout; on timeout the status stays Unknown.
        /// </summary>
        public Task CheckTargetsAsync(IEnumerable<Keycut> keycuts, CancellationToken ct = default)
        {
            return CheckTargetsAsync(keycuts, DefaultCheckTimeout, ct);
        }

        public async Task CheckTargetsAsync(IEnumerable<Keycut> keycuts, TimeSpan timeout, CancellationToken ct = default)
        {
            var checkable = keycuts.Where(IsCheckable).ToList();

            // Read once per run, and only if a keycut has a bare program name to look for
            var lookup = new Lazy<NameConflictEnvironment>(_programLookup);

            using (var gate = new SemaphoreSlim(MaxParallelChecks))
            {
                var tasks = checkable.Select(async keycut =>
                {
                    await gate.WaitAsync(ct).ConfigureAwait(false);

                    try
                    {
                        // The program, not the whole target: arguments after it aren't part of any path
                        var result = await CheckOneAsync(string.IsNullOrEmpty(keycut.Program) ? keycut.Target : keycut.Program, keycut.RawType, lookup, timeout, ct).ConfigureAwait(false);

                        if (result.Status != TargetStatus.Unknown)
                        {
                            keycut.CheckedPath = result.LookedFor;
                            keycut.TargetStatus = result.Status;
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
                // Read as the Edit form reads it, strictly: a file that isn't valid UTF-8 is still listed (read leniently, as
                // it always was), but the form would lose bytes of it, so it can't be edited there (#202). For valid UTF-8,
                // with or without a BOM, the lines are the ones File.ReadAllLines gives.
                string[] lines;
                var strict = true;

                try
                {
                    lines = ReadLinesStrict(file);
                }
                catch (System.Text.DecoderFallbackException)
                {
                    lines = File.ReadAllLines(file);
                    strict = false;
                }

                var shortcut = new ShortcutFile(file, lines);

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
                    Target = shortcut.Target ?? "",
                    Program = shortcut.Program ?? "",
                    Arguments = shortcut.Arguments ?? "",
                    FilePath = file,
                    CanEditInForm = strict && IsEditable(shortcut, file, lines),
                    Modified = File.GetLastWriteTime(file)
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"keycuts: skipped \"{file}\": {ex.Message}");
                return null;
            }
        }

        private DeletedKeycut Delete(Keycut keycut)
        {
            var path = keycut.FilePath;

            try
            {
                if (!File.Exists(path))
                {
                    throw new KeycutRepositoryException(KeycutRepositoryError.NotFound, $"\"{path}\" no longer exists.");
                }

                // Read before removing: a file that can't be read can't be undone, and isn't deleted
                var contents = File.ReadAllBytes(path);
                var modified = File.GetLastWriteTime(path);
                var recycled = _recycler.UsesRecycleBin(path);

                _recycler.Remove(path);

                return new DeletedKeycut(keycut, contents, modified, recycled);
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

        private static Keycut Update(Keycut keycut, KeycutEdit edit)
        {
            var oldPath = keycut.FilePath;
            var name = (edit.Name ?? string.Empty).Trim();

            if (!KeycutCreator.IsValidName(name))
            {
                throw new KeycutRepositoryException(KeycutRepositoryError.InvalidName, $"\"{name}\" isn't a valid keycut name.");
            }

            var folder = Path.GetDirectoryName(oldPath);
            var newPath = Path.Combine(folder, name + ShortcutFile.Extension);
            var sameFile = string.Equals(newPath, oldPath, StringComparison.OrdinalIgnoreCase);

            try
            {
                if (!File.Exists(oldPath))
                {
                    throw new KeycutRepositoryException(KeycutRepositoryError.NotFound, $"\"{oldPath}\" no longer exists.");
                }

                ShortcutFile parsed;

                try
                {
                    if (!TryReadEditable(oldPath, ReadLinesStrict(oldPath), out parsed))
                    {
                        throw new KeycutRepositoryException(KeycutRepositoryError.NotEditable, $"\"{oldPath}\" can't be edited here.");
                    }
                }
                catch (System.Text.DecoderFallbackException ex)
                {
                    throw new KeycutRepositoryException(KeycutRepositoryError.NotEditable, $"\"{oldPath}\" isn't valid UTF-8, so it can't be edited here.", ex);
                }

                // The form was opened for the keycut as it was loaded. If its file has changed since (another window, Notepad),
                // saving would quietly put the old target or description back
                if (!string.Equals(parsed.Target, keycut.Target ?? string.Empty, StringComparison.Ordinal) ||
                    !string.Equals(parsed.Description, keycut.Description ?? string.Empty, StringComparison.Ordinal))
                {
                    throw new KeycutRepositoryException(KeycutRepositoryError.Changed, $"\"{oldPath}\" changed since it was loaded.");
                }

                // Another keycut with that name, unless it's this file under a different case
                if (!sameFile && File.Exists(newPath))
                {
                    throw new KeycutRepositoryException(KeycutRepositoryError.AlreadyExists, $"\"{name}\" already exists.");
                }

                var lines = Runner.BuildShortcutLines(BuildShortcutForEdit(parsed, edit, name, newPath, folder), edit.Description);

                // Beside the keycut, and not a .bat, so a listing never sees it; swapped in only once it's complete
                var temp = Path.Combine(folder, "." + Path.GetFileName(oldPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

                try
                {
                    File.WriteAllLines(temp, lines);

                    if (sameFile)
                    {
                        // Replace, not Move: it keeps the original's identity (permissions, the "downloaded" mark)
                        File.Replace(temp, oldPath, null);

                        if (!string.Equals(newPath, oldPath, StringComparison.Ordinal))
                        {
                            File.Move(oldPath, newPath);
                        }
                    }
                    else
                    {
                        // Rename first. Move keeps the file's identity (its permissions and alternate streams such as the
                        // "downloaded" mark), which a copy and a delete would not, and it never overwrites: a name taken since
                        // the check above throws here, before anything has changed
                        File.Move(oldPath, newPath);

                        try
                        {
                            File.Replace(temp, newPath, null);
                        }
                        catch (Exception)
                        {
                            // The content couldn't be written: put the name back, so a failed save changes nothing
                            TryMoveBack(newPath, oldPath);
                            throw;
                        }
                    }
                }
                finally
                {
                    if (File.Exists(temp))
                    {
                        File.Delete(temp);
                    }
                }

                return TryParse(newPath) ?? new Keycut
                {
                    Name = name,
                    Kind = KeycutKind.File,
                    RawType = ShortcutType.Unknown,
                    Target = edit.Target,
                    FilePath = newPath,
                    CanEditInForm = false,
                    Modified = File.GetLastWriteTime(newPath)
                };
            }
            catch (KeycutRepositoryException)
            {
                throw;
            }
            catch (IOException) when (!sameFile && File.Exists(newPath))
            {
                // Taken between the check and the move
                throw new KeycutRepositoryException(KeycutRepositoryError.AlreadyExists, $"\"{name}\" already exists.");
            }
            catch (Exception ex)
            {
                throw Translate(ex, $"Can't save \"{oldPath}\".");
            }
        }

        /// <summary>Best effort: undoes a rename after a failed save. If that fails too, the keycut keeps its new name and the old content.</summary>
        private static void TryMoveBack(string from, string to)
        {
            try
            {
                File.Move(from, to);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.WriteLine($"keycuts: couldn't put \"{from}\" back as \"{to}\": {ex.Message}");
            }
        }

        /// <summary>The keycut the form's values describe, for the emitter to write.</summary>
        private static Shortcut BuildShortcutForEdit(ShortcutFile parsed, KeycutEdit edit, string name, string newPath, string folder)
        {
            var target = (edit.Target ?? string.Empty).Trim();

            // Unchanged: the type and target as read, so the launch line comes out as it was (TryReadEditable proved it does)
            if (string.Equals(target, parsed.Target, StringComparison.Ordinal))
            {
                return Shortcut.FromParts(parsed.Type, parsed.Target, newPath, string.IsNullOrEmpty(parsed.OpenWithApp) ? null : parsed.OpenWithApp);
            }

            var resolved = TargetResolver.Resolve(target);

            if (!resolved.IsValid)
            {
                var error = resolved.Error == TargetError.None || resolved.Error == TargetError.Empty ? TargetError.NotFound : resolved.Error;
                throw new KeycutRepositoryException(KeycutRepositoryError.InvalidTarget, $"\"{target}\" can't be used as a target.", null, error);
            }

            // As a new keycut: the ".bat" keeps a dot in the name from being read as an extension
            var shortcut = new Shortcut(resolved.Target, name + ShortcutFile.Extension, folder);

            if (shortcut.Type == ShortcutType.Unknown)
            {
                throw new KeycutRepositoryException(KeycutRepositoryError.InvalidTarget, $"\"{target}\" can't be used as a target.", null, TargetError.NotFound);
            }

            if (edit.UseEnvironmentVariables && PathVariables.AppliesTo(shortcut.Type))
            {
                shortcut.SetTarget(PathVariables.Substitute(shortcut.Target, edit.PathVariables ?? PathVariables.GetDefault()));
            }

            return shortcut;
        }

        private static void Restore(DeletedKeycut deleted)
        {
            var path = deleted.Keycut.FilePath;
            var name = Path.GetFileNameWithoutExtension(path);

            try
            {
                // Never overwrite: the name may have been used again since (a new keycut with the same name)
                if (File.Exists(path))
                {
                    throw new KeycutRepositoryException(KeycutRepositoryError.AlreadyExists, $"\"{name}\" already exists.");
                }

                var temp = Path.Combine(Path.GetDirectoryName(path), "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");

                try
                {
                    File.WriteAllBytes(temp, deleted.Contents);

                    if (deleted.Modified != default)
                    {
                        File.SetLastWriteTime(temp, deleted.Modified);
                    }

                    // Move without overwriting, so a file created between the check and here is kept (it throws)
                    File.Move(temp, path);
                }
                finally
                {
                    if (File.Exists(temp))
                    {
                        File.Delete(temp);
                    }
                }
            }
            catch (KeycutRepositoryException)
            {
                throw;
            }
            catch (IOException) when (File.Exists(path))
            {
                throw new KeycutRepositoryException(KeycutRepositoryError.AlreadyExists, $"\"{name}\" already exists.");
            }
            catch (Exception ex)
            {
                throw Translate(ex, $"Can't restore \"{path}\".");
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

                ReplaceFileContent(path, hasBom ? preamble.Concat(body).ToArray() : body);
            }
            catch (Exception ex) when (ex is System.Text.DecoderFallbackException || ex is IOException || ex is UnauthorizedAccessException)
            {
                // The REM line is only a comment, so a file we can't decode or rewrite keeps its old one
                Debug.WriteLine($"keycuts: kept the old <shortcut> line in \"{path}\": {ex.Message}");
            }
        }

        /// <summary>
        /// Writes <paramref name="content"/> to a temp file beside <paramref name="path"/> and swaps it in only once it's
        /// complete: writing in place truncates first, so a failed write (a full disk) would leave the keycut empty or half written.
        /// </summary>
        private static void ReplaceFileContent(string path, byte[] content)
        {
            var temp = Path.Combine(Path.GetDirectoryName(path), "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                File.WriteAllBytes(temp, content);

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

        private static readonly System.Text.RegularExpressions.Regex DescriptionLineParts = new System.Text.RegularExpressions.Regex(
            @"^(?<pre>\s*REM\s+<description>)(?<text>.*)(?<post></description>\s*)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly char[] LineBreaks = { '\r', '\n' };

        /// <summary>Where each line of <paramref name="text"/> starts and how long it is, without its line break (<c>\r\n</c>, <c>\n</c> or <c>\r</c>), as <see cref="StringReader.ReadLine"/> splits it.</summary>
        private static List<(int Start, int Length)> ScanLines(string text)
        {
            var lines = new List<(int, int)>();
            var position = 0;

            while (position < text.Length)
            {
                var end = text.IndexOfAny(LineBreaks, position);

                if (end < 0)
                {
                    lines.Add((position, text.Length - position));
                    break;
                }

                lines.Add((position, end - position));
                position = end + (text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n' ? 2 : 1);
            }

            return lines;
        }

        private static Keycut SetDescription(Keycut keycut, string description)
        {
            var path = keycut.FilePath;

            try
            {
                if (!File.Exists(path))
                {
                    throw new KeycutRepositoryException(KeycutRepositoryError.NotFound, $"\"{path}\" no longer exists.");
                }

                var bytes = File.ReadAllBytes(path);
                var preamble = System.Text.Encoding.UTF8.GetPreamble();
                var hasBom = bytes.AsSpan().StartsWith(preamble);
                var strict = new System.Text.UTF8Encoding(false, true);
                string text;

                try
                {
                    var start = hasBom ? preamble.Length : 0;
                    text = strict.GetString(bytes, start, bytes.Length - start);
                }
                catch (System.Text.DecoderFallbackException ex)
                {
                    // Written back as UTF-8, a byte that isn't valid would be lost for good
                    throw new KeycutRepositoryException(KeycutRepositoryError.NotEditable, $"\"{path}\" isn't valid UTF-8, so its description can't be changed here.", ex);
                }

                var spans = ScanLines(text);
                var lines = spans.Select(span => text.Substring(span.Start, span.Length)).ToArray();

                // The keycut was loaded some time ago. If its description has been changed since (Notepad, another window),
                // saving would quietly put the old one back over it
                if (!string.Equals(ShortcutFile.ParseDescription(lines), keycut.Description ?? string.Empty, StringComparison.Ordinal))
                {
                    throw new KeycutRepositoryException(KeycutRepositoryError.Changed, $"\"{path}\" changed since it was loaded.");
                }

                // As the Creator writes it (one line, every % doubled, #162), and as reading it back gives it
                var sanitized = Runner.SanitizeDescription(description);
                var wanted = ShortcutFile.ParseDescription(new[] { $"REM <description>{sanitized}</description>" });
                var existing = Array.FindIndex(lines, line => DescriptionLineParts.IsMatch(line));

                // Nothing to change: the file keeps its bytes and its date. No line and nothing to say needs no line.
                if (existing >= 0 ? string.Equals(ShortcutFile.ParseDescription(lines), wanted, StringComparison.Ordinal) : wanted.Length == 0)
                {
                    return TryParse(path) ?? UnreadableKeycut(keycut);
                }

                string updated;

                if (existing >= 0)
                {
                    // Only the text between the tags changes; the line's own spacing, and the file's line breaks, stay
                    var match = DescriptionLineParts.Match(lines[existing]);
                    var replacement = match.Groups["pre"].Value + sanitized + match.Groups["post"].Value;
                    updated = text.Substring(0, spans[existing].Start) + replacement + text.Substring(spans[existing].Start + spans[existing].Length);
                }
                else
                {
                    // A keycut from before descriptions (1.0) or a hand-written launcher has no line: add one where the
                    // emitter puts it, just before the launch line, with the file's own line breaks
                    var launch = Array.FindIndex(lines, ShortcutFile.IsLaunchLine);

                    if (launch < 0)
                    {
                        throw new KeycutRepositoryException(KeycutRepositoryError.NotEditable, $"\"{path}\" has no launch line, so no description can be added.");
                    }

                    var firstBreak = text.IndexOfAny(LineBreaks);
                    var lineBreak = firstBreak < 0 ? "\r\n" : text[firstBreak] == '\r' && firstBreak + 1 < text.Length && text[firstBreak + 1] == '\n' ? "\r\n" : text[firstBreak].ToString();
                    updated = text.Insert(spans[launch].Start, $"REM <description>{sanitized}</description>" + lineBreak);
                }

                var body = strict.GetBytes(updated);
                ReplaceFileContent(path, hasBom ? preamble.Concat(body).ToArray() : body);

                return TryParse(path) ?? UnreadableKeycut(keycut);
            }
            catch (KeycutRepositoryException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw Translate(ex, $"Can't save \"{path}\".");
            }
        }

        /// <summary>A keycut whose file can't be read as one any more, as <see cref="Find"/> reports it: just its name and path.</summary>
        private static Keycut UnreadableKeycut(Keycut keycut)
        {
            return new Keycut
            {
                Name = Path.GetFileNameWithoutExtension(keycut.FilePath),
                Kind = KeycutKind.File,
                RawType = ShortcutType.Unknown,
                Target = "",
                FilePath = keycut.FilePath,
                CanEditInForm = false,
                Modified = File.GetLastWriteTime(keycut.FilePath)
            };
        }

        private static bool IsCheckable(Keycut keycut)
        {
            return !string.IsNullOrWhiteSpace(keycut.Target) &&
                (keycut.RawType == ShortcutType.File ||
                 keycut.RawType == ShortcutType.Folder ||
                 keycut.RawType == ShortcutType.HostsFile);
        }

        private async Task<CheckResult> CheckOneAsync(string target, ShortcutType type, Lazy<NameConflictEnvironment> lookup, TimeSpan timeout, CancellationToken ct)
        {
            var work = Task.Run(() =>
            {
                try
                {
                    var path = Environment.ExpandEnvironmentVariables(target);

                    // Missing means "we looked for this and it isn't there". Text that can't be a path (a quote in it, a
                    // wildcard) was never looked for, so the keycut is left unmarked rather than claimed missing.
                    if (!IsPlausiblePath(path))
                    {
                        return new CheckResult(TargetStatus.Unknown, path);
                    }

                    // A program with no folder (chrome, notepad.exe) is found where Windows finds it, not in this process's
                    // current directory, which is all File.Exists looks at
                    if (type == ShortcutType.File && IsBareName(path))
                    {
                        var environment = lookup.Value;

                        // No output folder: that argument gives a name its conflict rules (the keycut's own .bat is skipped, the
                        // folder is searched even when it isn't on PATH), and this only asks what Windows would find
                        if (NameConflicts.Find(path, null, environment).Count > 0)
                        {
                            return new CheckResult(TargetStatus.Ok, path);
                        }

                        // A name like readme.txt is searched for as typed only when PATHEXT lists its extension, so a
                        // search that came up empty says nothing about it
                        return HasUnlistedExtension(path, environment) ?
                            new CheckResult(TargetStatus.Unknown, path) :
                            new CheckResult(TargetStatus.Missing, path);
                    }

                    return File.Exists(path) || Directory.Exists(path) ?
                        new CheckResult(TargetStatus.Ok, path) :
                        new CheckResult(TargetStatus.Missing, path);
                }
                catch (Exception)
                {
                    return new CheckResult(TargetStatus.Unknown, string.Empty);
                }
            });

            var finished = await Task.WhenAny(work, Task.Delay(timeout, ct)).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            return finished == work ? await work.ConfigureAwait(false) : new CheckResult(TargetStatus.Unknown, string.Empty);
        }

        /// <summary>What a check found out, and the path (expanded) or name it looked for.</summary>
        private readonly record struct CheckResult(TargetStatus Status, string LookedFor);

        // GetInvalidPathChars() is only | and the control characters in .NET (not the quote, < and >, which Windows paths can't hold either), and a wildcard names no one file
        private static readonly char[] ImplausiblePathChars = Path.GetInvalidPathChars().Concat(new[] { '"', '<', '>', '*' }).Distinct().ToArray();

        /// <summary>False for text that can't name a file: empty, with a quote, a wildcard or another character Windows paths can't hold.</summary>
        private static bool IsPlausiblePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(ImplausiblePathChars) >= 0)
            {
                return false;
            }

            // ? is the other wildcard, except in the \\?\ prefix of an extended path
            var body = path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal) ? path.Substring(4) : path;

            return body.IndexOf('?') < 0;
        }

        /// <summary>A program name with no folder in it, like <c>chrome</c> or <c>notepad.exe</c>: a file name, not a path.</summary>
        private static bool IsBareName(string path)
        {
            return path.Trim('.').Length > 0 && path.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        private static bool HasUnlistedExtension(string name, NameConflictEnvironment environment)
        {
            var extension = Path.GetExtension(name);

            return extension.Length > 0 && !NameConflicts.ParsePathExt(environment.PathExt)
                .Contains(extension, StringComparer.OrdinalIgnoreCase);
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
