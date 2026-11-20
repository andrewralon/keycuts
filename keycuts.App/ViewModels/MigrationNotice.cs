using keycuts.Common;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// The message shown once after <see cref="KeycutMigration"/> brought keycuts written by older versions to the current format
    /// (#196, #207): how many, what changed for the user (folder keycuts' console goes sooner), and where the originals are.
    /// </summary>
    public static class MigrationNotice
    {
        /// <summary>The message, or null when nothing was changed (nothing to say).</summary>
        public static string? Format(MigrationResult result)
        {
            if (result == null || result.Migrated.Count == 0)
            {
                return null;
            }

            var count = result.Migrated.Count;
            var keycuts = count == 1 ? "1 keycut" : count + " keycuts";
            var they = count == 1 ? "It opens" : "They open";
            var faster = result.LaunchLinesUpdated == 0
                ? string.Empty
                : result.LaunchLinesUpdated == 1
                    ? " The folder keycut among them now closes its console window sooner."
                    : " Folder keycuts among them now close their console window sooner.";

            return "keycuts updated " + keycuts + " written by an older version to the current format. " +
                they + " the same thing as before, and the date modified didn't change." + faster + "\n\n" +
                "The originals were copied to:\n" + result.BackupFolder;
        }
    }
}
