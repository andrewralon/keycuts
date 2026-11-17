using System;
using System.IO;

namespace keycuts.Common
{
    /// <summary>What <see cref="KeycutCommands"/> needs: the folder's repository, the console, the name check and a way to run a file.</summary>
    public sealed class KeycutCommandContext
    {
        public KeycutCommandContext(KeycutRepository repository, TextWriter output, TextWriter error)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            Output = output ?? TextWriter.Null;
            Error = error ?? TextWriter.Null;
        }

        public KeycutRepository Repository { get; }

        public TextWriter Output { get; }

        public TextWriter Error { get; }

        /// <summary>True when nobody can answer a question (a script, a pipe): a rename that needs one exits <see cref="ExitCode.ConfirmationNeeded"/>.</summary>
        public bool IsInputRedirected { get; init; }

        /// <summary>Reads one key for a y / n answer; only called while input isn't redirected.</summary>
        public Func<char> ReadKey { get; init; } = () => 'n';

        /// <summary>What a new name runs into besides another keycut (<see cref="NameCheck.Run"/>); null checks nothing.</summary>
        public Func<string, NameCheck> CheckName { get; init; }

        /// <summary>Runs a keycut's .bat with arguments, as Win+R would (the shell, not cmd /c).</summary>
        public Action<string, string> Launch { get; init; } = (path, arguments) => { };
    }

    /// <summary>
    /// The CLI verbs that match the Manager (#173): <c>rm</c>, <c>rename</c> and <c>open</c>. Each goes through
    /// <see cref="KeycutRepository"/>, so it follows the Manager's rules: delete to the Recycle Bin (with the run count), rename
    /// only to a free valid name and ask first about a name that runs into another command, and never treat the keycuts and
    /// keypaste commands (#248) as keycuts. Messages go to the writers given, errors to <see cref="KeycutCommandContext.Error"/>.
    /// </summary>
    public static class KeycutCommands
    {
        public const string RetryRenameWithForceMessage = "Nothing was renamed. Run it again with -f to rename it anyway.";

        /// <summary><c>keycuts rm &lt;name&gt;</c>: deletes the keycut as the Manager does.</summary>
        public static ExitCode Remove(KeycutCommandContext context, string name)
        {
            var keycut = Find(context, name, out var notFound);
            if (keycut == null)
            {
                return notFound;
            }

            try
            {
                var deleted = context.Repository.DeleteAsync(keycut).GetAwaiter().GetResult();
                context.Output.WriteLine(deleted.Recycled ? $"Deleted {keycut.Name}. It's in the Recycle Bin." : $"Deleted {keycut.Name}.");
                return ExitCode.Success;
            }
            catch (KeycutRepositoryException ex)
            {
                return Fail(context, ex);
            }
        }

        /// <summary>
        /// <c>keycuts rename &lt;old&gt; &lt;new&gt; [-f]</c>: the Manager's rename. A taken or invalid name is refused; a name that runs into
        /// another command asks first, unless <paramref name="force"/>, and can't ask when input is redirected.
        /// </summary>
        public static ExitCode Rename(KeycutCommandContext context, string oldName, string newName, bool force)
        {
            var keycut = Find(context, oldName, out var notFound);
            if (keycut == null)
            {
                return notFound;
            }

            newName = (newName ?? "").Trim();
            if (!KeycutCreator.IsValidName(newName))
            {
                context.Error.WriteLine(CommandNames.IsCommandName(newName)
                    ? $"keycuts: \"{newName}\" is a keycuts command, so it can't be a keycut's name."
                    : $"keycuts: \"{newName}\" can't be a keycut's name: no \\ / : * ? \" < > |, no trailing dot, and not a reserved name like NUL.");
                return ExitCode.BadArguments;
            }

            // A change of case only is the same name; anything else asks about what else the name opens
            var sameName = string.Equals(newName, keycut.Name, StringComparison.OrdinalIgnoreCase);
            if (!force && !sameName && context.CheckName != null)
            {
                var check = context.CheckName(newName);
                if (check != null && check.Conflicts.Count > 0)
                {
                    foreach (var conflict in check.Conflicts)
                    {
                        context.Output.WriteLine(conflict.Effect == NameConflictEffect.Hidden
                            ? $"\"{newName}\" already opens {conflict.Path}, so Win+R would never run the keycut under that name."
                            : $"\"{newName}\" already opens {conflict.Path}. The keycut would replace it in Win+R.");
                    }

                    if (context.IsInputRedirected)
                    {
                        context.Output.WriteLine(RetryRenameWithForceMessage);
                        return ExitCode.ConfirmationNeeded;
                    }

                    context.Output.Write("Rename it anyway? (y / n)  ");
                    var answer = context.ReadKey();
                    context.Output.WriteLine();

                    if (char.ToLowerInvariant(answer) != 'y')
                    {
                        // Declining is a choice, not a failure (#65)
                        context.Output.WriteLine($"Didn't rename {keycut.Name}.");
                        return ExitCode.Success;
                    }
                }
            }

            try
            {
                context.Repository.RenameAsync(keycut, newName).GetAwaiter().GetResult();
                context.Output.WriteLine($"Renamed {keycut.Name} to {newName}.");
                return ExitCode.Success;
            }
            catch (KeycutRepositoryException ex)
            {
                return Fail(context, ex, newName);
            }
        }

        /// <summary><c>keycuts open &lt;name&gt; [arguments]</c>: runs the keycut as Win+R would, with any arguments after the name.</summary>
        public static ExitCode Open(KeycutCommandContext context, string name, string arguments)
        {
            var keycut = Find(context, name, out var notFound);
            if (keycut == null)
            {
                return notFound;
            }

            try
            {
                context.Launch(keycut.FilePath, arguments ?? "");
                return ExitCode.Success;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is IOException || ex is InvalidOperationException)
            {
                context.Error.WriteLine($"keycuts: couldn't open {keycut.Name}: {ex.Message}");
                return ExitCode.CannotOpenFile;
            }
        }

        /// <summary>The keycut named <paramref name="name"/> (any case, without .bat), or null after saying it isn't there.</summary>
        private static Keycut Find(KeycutCommandContext context, string name, out ExitCode notFound)
        {
            notFound = ExitCode.CannotFindFile;
            var trimmed = (name ?? "").Trim();

            if (trimmed.EndsWith(ShortcutFile.Extension, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - ShortcutFile.Extension.Length);
            }

            var keycut = trimmed.Length == 0 ? null : context.Repository.Find(trimmed);
            if (keycut == null)
            {
                context.Error.WriteLine($"keycuts: there's no keycut named \"{trimmed}\" in {context.Repository.OutputFolder}.");
            }

            return keycut;
        }

        private static ExitCode Fail(KeycutCommandContext context, KeycutRepositoryException ex, string newName = null)
        {
            switch (ex.Error)
            {
                case KeycutRepositoryError.AlreadyExists:
                    context.Error.WriteLine($"keycuts: there's already a keycut named \"{newName}\".");
                    return ExitCode.FileAlreadyExists;
                case KeycutRepositoryError.NotFound:
                    context.Error.WriteLine("keycuts: " + ex.Message);
                    return ExitCode.CannotFindFile;
                case KeycutRepositoryError.AccessDenied:
                    context.Error.WriteLine("keycuts: " + ex.Message);
                    return ExitCode.AccessIsDenied;
                case KeycutRepositoryError.InvalidName:
                    context.Error.WriteLine("keycuts: " + ex.Message);
                    return ExitCode.BadArguments;
                default:
                    context.Error.WriteLine("keycuts: " + ex.Message);
                    return ExitCode.CannotOpenFile;
            }
        }
    }
}
