using System;
using System.IO;

namespace keycuts.Common
{
    public enum CreateDecision
    {
        /// <summary>Go ahead; the <c>overwrite</c> result says whether an existing keycut may be replaced.</summary>
        Create,

        /// <summary>The user answered no. That's a choice, not a failure (#65).</summary>
        Declined,

        /// <summary>A confirmation was needed but input is redirected, so nobody can answer.</summary>
        ConfirmationNeeded
    }

    /// <summary>
    /// The CLI's confirmation before <c>keycuts create</c> (#153): it prints what the name runs into (an existing keycut,
    /// with its current and new target, and other commands with the name) and asks y / n. <c>-f</c> skips it. With
    /// redirected input it never waits for an answer. Output goes to the writer it's given, since the library doesn't log.
    /// </summary>
    public static class CreateConfirmation
    {
        public const string RetryWithForceMessage = "Nothing was created. Run it again with -f to create it anyway.";

        /// <summary>
        /// Decides whether to create, and whether replacing an existing keycut is allowed: only with -f, with
        /// <paramref name="askBeforeReplacing"/> off, or after a yes to replacing the keycut the check found. A keycut that
        /// appears after the check is never replaced unasked; the create then fails with FileAlreadyExists.
        /// </summary>
        /// <param name="check">The name check, with <see cref="NameCheck.Existing"/> set only when replacing should ask.</param>
        /// <param name="newTarget">The target as given, shown next to the existing keycut's.</param>
        /// <param name="askBeforeReplacing">The "Ask before replacing a keycut" setting.</param>
        /// <param name="readKey">Reads one key; called only when input isn't redirected.</param>
        public static CreateDecision Decide(NameCheck check, string newTarget, bool force, bool askBeforeReplacing,
            bool isInputRedirected, Func<char> readKey, TextWriter output, out bool overwrite)
        {
            overwrite = force || !askBeforeReplacing;

            if (force || check == null || !check.HasWarnings)
            {
                return CreateDecision.Create;
            }

            WriteWarnings(check, newTarget, output);

            if (isInputRedirected)
            {
                output.WriteLine(RetryWithForceMessage);
                return CreateDecision.ConfirmationNeeded;
            }

            output.Write(check.Existing != null ? "Overwrite? (y / n)  " : "Create it anyway? (y / n)  ");
            var answer = readKey();
            output.WriteLine();

            if (char.ToLowerInvariant(answer) == 'y')
            {
                // A yes to a name conflict alone doesn't allow replacing a keycut; a yes to the replace does
                overwrite = overwrite || check.Existing != null;
                return CreateDecision.Create;
            }

            output.WriteLine(check.Existing != null ? "Kept the existing shortcut." : "Didn't create the shortcut.");
            return CreateDecision.Declined;
        }

        public static void WriteWarnings(NameCheck check, string newTarget, TextWriter output)
        {
            if (check.Existing != null)
            {
                output.WriteLine($"Shortcut \"{check.Name}\" already exists.");
                output.WriteLine($"  It opens:      {DescribeTarget(check.Existing.Target)}");
                output.WriteLine($"  It would open: {newTarget}");
            }

            foreach (var conflict in check.Conflicts)
            {
                output.WriteLine(Describe(check.Name, conflict));
            }
        }

        /// <summary>One line about a conflict, in plain ASCII quotes for the console.</summary>
        public static string Describe(string name, NameConflict conflict)
        {
            return conflict.Effect == NameConflictEffect.Hidden
                ? $"\"{name}\" already opens {conflict.Path}, so Win+R would never run this shortcut."
                : $"\"{name}\" already opens {conflict.Path}. This shortcut would replace it in Win+R.";
        }

        private static string DescribeTarget(string target)
        {
            return string.IsNullOrEmpty(target) ? "(unknown)" : target;
        }
    }
}
