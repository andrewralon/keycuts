using System;

namespace keycuts.Common
{
    /// <summary>The names messages use for the platform's own things, so a Mac reads Terminal and Trash where Windows reads Win+R and Recycle Bin.</summary>
    public static class PlatformWords
    {
        /// <summary>Where a keycut is run by its name: Win+R, or Terminal on macOS (the Run box opens the file itself, so no name clashes there).</summary>
        public static readonly string Launcher = OperatingSystem.IsMacOS() ? "Terminal" : "Win+R";

        /// <summary>Where a deleted keycut goes: the Recycle Bin, or the Trash on macOS.</summary>
        public static readonly string Bin = OperatingSystem.IsMacOS() ? "Trash" : "Recycle Bin";
    }
}
