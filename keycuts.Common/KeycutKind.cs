using System;

namespace keycuts.Common
{
    /// <summary>The four kinds shown in the UI (badges, icons).</summary>
    public enum KeycutKind
    {
        Url,
        File,
        Folder,
        Command
    }

    public static class KeycutKinds
    {
        /// <summary>Single source of truth for the ShortcutType to display kind mapping.</summary>
        public static KeycutKind FromShortcutType(ShortcutType type)
        {
            switch (type)
            {
                case ShortcutType.Url:
                    return KeycutKind.Url;
                case ShortcutType.Folder:
                case ShortcutType.CLSIDKey:
                    return KeycutKind.Folder;
                case ShortcutType.File:
                case ShortcutType.HostsFile:
                    return KeycutKind.File;
                default:
                    return KeycutKind.Command;
            }
        }
    }
}
