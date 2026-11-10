using System;
using System.IO;

namespace keycuts.Common
{
    public enum CreateDecision
    {
        /// <summary>Go ahead; <see cref="CreateConfirmation.Overwrite"/> says whether an existing keycut may be replaced.</summary>
        Create,

        /// <summary>The user answered no. That's a choice, not a failure (#65).</summary>
        Declined,

        /// <summary>A confirmation was needed but input is redirected, so nobody can answer.</summary>
        ConfirmationNeeded
    }

    /// <summary>
    /// The CLI's confirmation before <c>keycuts create</c> (#153): it prints what the name runs into (an existing keycut,
    /// with its current and new destination, and other commands with the name) and asks y / n. <c>-f</c> skips it. With
    /// redirected input it never waits for an answer. Output goes to the writer it's given, since the library doesn't log.
    /// </summary>
    public static class CreateConfirmation
    {
        public const string RetryWithForceMessage = "Nothing was created. Run it again with -f to create it anyway.";

        /// <summary>Decides whether to create, and whether replacing an existing keycut is allowed.</summary>
        /// <param name="check">The name check, with <see cref="NameCheck.Existing"/> set only when replacing should ask.</param>
        /// <param name="newDestination">The destination as given, shown next to the existing keycut's.</param>
        /// <param name="readKey">Reads one key; called only when input isn't redirected.</param>
        public static CreateDecision Decide(NameCheck check, string newDestination, bool force, bool isInputRedirected,
            Func<char> readKey, TextWriter output, out bool overwrite)
        {
            // Without -f, a keycut is replaced only after a yes, or when the setting says not to ask (no Existing then)
            overwrite = force || check == null || check.Existing == null;

            if (force || check == null || !check.HasWarnings)
            {
                return CreateDecision.Create;
            }

            WriteWarnings(check, newDestination, output);

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
                overwrite = true;
                return CreateDecision.Create;
            }

            output.WriteLine(check.Existing != null ? "Kept the existing shortcut." : "Didn't create the shortcut.");
            return CreateDecision.Declined;
        }

        public static void WriteWarnings(NameCheck check, string newDestination, TextWriter output)
        {
            if (check.Existing != null)
            {
                output.WriteLine($"Shortcut \"{check.Name}\" already exists.");
                output.WriteLine($"  It opens:      {DescribeDestination(check.Existing.Destination)}");
                output.WriteLine($"  It would open: {newDestination}");
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

        private static string DescribeDestination(string destination)
        {
            return string.IsNullOrEmpty(destination) ? "(unknown)" : destination;
        }
    }
}
