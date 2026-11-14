using System;
using System.Collections.Generic;
using System.Linq;
using keycuts.Common;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// What the confirmation dialog shows (#153) before a keycut replaces another or takes a name another command uses:
    /// a heading, a line of context, the current and new targets for a replace, and one line per name conflict.
    /// </summary>
    public class ConfirmViewModel
    {
        /// <summary>Conflicts beyond this many are summed up in one line, so a long PATH can't make the dialog huge.</summary>
        public const int MaxConflictLines = 4;

        public const string ReplaceIntro = "A keycut with this name already exists. Replacing it changes what it opens.";

        public const string ConflictIntro = "Something else already answers to this name in Win+R.";

        private ConfirmViewModel(string title, string heading, string intro, string confirmText, string? currentTarget,
            string? newTarget, IReadOnlyList<string> warnings)
        {
            Title = title;
            Heading = heading;
            Intro = intro;
            ConfirmText = confirmText;
            CurrentTarget = currentTarget;
            NewTarget = newTarget;
            Warnings = warnings;
        }

        /// <summary>The window title.</summary>
        public string Title { get; }

        public string Heading { get; }

        public string Intro { get; }

        /// <summary>The primary button, which is also what Enter does: "replace", "create anyway", "rename anyway" or "restore defaults" (button text is lowercase, docs/styleguide.md).</summary>
        public string ConfirmText { get; }

        /// <summary>What the keycut being replaced opens now; null when nothing is replaced.</summary>
        public string? CurrentTarget { get; }

        /// <summary>What it will open instead; null when nothing is replaced.</summary>
        public string? NewTarget { get; }

        public bool ShowTargets
        {
            get { return CurrentTarget != null; }
        }

        /// <summary>One line per name conflict, most important first.</summary>
        public IReadOnlyList<string> Warnings { get; }

        public bool ShowWarnings
        {
            get { return Warnings.Count > 0; }
        }

        /// <summary>Everything the dialog says, in reading order, for screen readers (the help text of the focused button).</summary>
        public string Summary
        {
            get
            {
                var parts = new List<string> { Heading, Intro };

                if (ShowTargets)
                {
                    parts.Add("Opens now: " + CurrentTarget + ".");
                    parts.Add("Will open: " + NewTarget + ".");
                }

                parts.AddRange(Warnings);
                return string.Join(" ", parts);
            }
        }

        /// <summary>For a new keycut: replace the existing one and/or use a name other commands have.</summary>
        public static ConfirmViewModel ForCreate(NameCheck check, string newTarget)
        {
            if (check == null)
            {
                throw new ArgumentNullException(nameof(check));
            }

            var warnings = GetWarnings(check, newTarget);

            if (check.Existing != null)
            {
                var current = string.IsNullOrEmpty(check.Existing.Target) ? "(unknown)" : check.Existing.Target;

                return new ConfirmViewModel("replace keycut", Quote("Replace ", check.Name, "?"), ReplaceIntro, "replace",
                    current, newTarget ?? string.Empty, warnings);
            }

            return new ConfirmViewModel("name already in use", Quote("Use ", check.Name, " anyway?"), ConflictIntro,
                "create anyway", null, null, warnings);
        }

        public const string RestoreDefaultsIntro = "Every setting on this page goes back to how a new install has it. The output folder stays as it is.";

        /// <summary>Settings' restore defaults (#218), which stores at once like any other change, so it asks first.</summary>
        public static ConfirmViewModel ForRestoreDefaults()
        {
            return new ConfirmViewModel("restore defaults", "Restore the default settings?", RestoreDefaultsIntro, "restore defaults",
                null, null, Array.Empty<string>());
        }

        /// <summary>For a rename to a name other commands have. A taken keycut name is refused, never confirmed.</summary>
        public static ConfirmViewModel ForRename(NameCheck check)
        {
            if (check == null)
            {
                throw new ArgumentNullException(nameof(check));
            }

            return new ConfirmViewModel("name already in use", Quote("Rename to ", check.Name, " anyway?"), ConflictIntro,
                "rename anyway", null, null, GetWarnings(check, null));
        }

        private static IReadOnlyList<string> GetWarnings(NameCheck check, string? newTarget)
        {
            // The one that matters most first: a conflict that hides the keycut outranks one it shadows
            var ordered = check.Conflicts.Where(c => c.Effect == NameConflictEffect.Hidden)
                .Concat(check.Conflicts.Where(c => c.Effect != NameConflictEffect.Hidden))
                .ToList();

            var lines = ordered.Take(MaxConflictLines).Select(c => CreatorViewModel.GetConflictMessage(check.Name, c, newTarget)).ToList();
            var more = ordered.Count - lines.Count;

            if (more > 0)
            {
                lines.Add(more == 1 ? "1 more program uses this name." : more + " more programs use this name.");
            }

            return lines;
        }

        private static string Quote(string before, string name, string after)
        {
            return before + "“" + name + "”" + after;
        }
    }
}
