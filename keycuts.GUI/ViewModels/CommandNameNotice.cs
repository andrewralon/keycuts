using System.Collections.Generic;
using System.Linq;
using keycuts.Common;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// The message shown once at startup after <see cref="KeycutRepository.MoveAsideCommandNames"/> renamed keycuts that
    /// clashed with the keycuts and keypaste commands on PATH.
    /// </summary>
    public static class CommandNameNotice
    {
        public static string Format(IReadOnlyList<RenamedKeycut> renamed)
        {
            var lines = renamed.Select(keycut => "    " + keycut.OldName + "  →  " + keycut.NewName);
            var them = renamed.Count == 1 ? "it" : "them";
            var those = renamed.Count == 1 ? "This keycut" : "These keycuts";

            return those + " had the same name as a keycuts command, which hid the command in Win+R and terminals, so keycuts renamed " +
                them + ":\n\n" + string.Join("\n", lines) + "\n\n" +
                "They still work under the new names. Rename or delete them in the manager.";
        }
    }
}
