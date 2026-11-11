using System;
using System.Collections.Generic;
using System.Linq;

namespace keycuts.Common
{
    /// <summary>
    /// The names of the commands keycuts puts on PATH, the shims in the install folder's bin (keycuts.cmd, keypaste.cmd).
    /// A keycut with one of these names clashes with the command: Windows takes whichever comes first on PATH, so one of
    /// the two stops working from Win+R and terminals. New keycuts can't take them (<see cref="KeycutCreator.IsValidName"/>),
    /// and the app renames an existing one at startup (<see cref="KeycutRepository.MoveAsideCommandNames"/>).
    /// </summary>
    public static class CommandNames
    {
        /// <summary>Appended to a renamed keycut's name, then a number if that's taken too.</summary>
        public const string PreviousSuffix = "_previous";

        public static readonly IReadOnlyList<string> All = new[] { "keycuts", "keypaste" };

        public static bool IsCommandName(string name)
        {
            return name != null && All.Contains(name.Trim(), StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>A keycut <see cref="KeycutRepository.MoveAsideCommandNames"/> renamed, by name without .bat.</summary>
    public class RenamedKeycut
    {
        public RenamedKeycut(string oldName, string newName)
        {
            OldName = oldName;
            NewName = newName;
        }

        public string OldName { get; }

        public string NewName { get; }
    }
}
