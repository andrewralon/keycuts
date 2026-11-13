using System;

namespace keycuts.Common
{
    public enum TargetStatus
    {
        Unknown,
        Ok,
        Missing
    }

    /// <summary>One keycut (.bat file) in the output folder, as shown in the manager.</summary>
    public record Keycut
    {
        /// <summary>File name without the .bat extension.</summary>
        public string Name { get; init; }

        public KeycutKind Kind { get; init; }

        public ShortcutType RawType { get; init; }

        /// <summary>May be empty.</summary>
        public string Description { get; init; } = "";

        public string Target { get; init; }

        /// <summary>
        /// For a File keycut: the path of the program, without its arguments (<see cref="ShortcutFile.Program"/>), which is what
        /// the Missing check looks for. Empty when the parser couldn't tell it apart from the arguments; the check then has
        /// nothing specific to look for. Folders and the rest have none.
        /// </summary>
        public string Program { get; init; } = "";

        /// <summary>For a File keycut: what follows <see cref="Program"/> on its launch line (<see cref="ShortcutFile.Arguments"/>); often empty.</summary>
        public string Arguments { get; init; } = "";

        public string FilePath { get; init; }

        /// <summary>
        /// When the .bat file was last written (local time), read when the keycut is loaded. Renaming rewrites the file,
        /// so it updates this. <c>default</c> when unknown (a keycut built by hand, not read from a file).
        /// </summary>
        public DateTime Modified { get; init; }

        /// <summary>Set in the background by <see cref="KeycutRepository.CheckTargetsAsync"/>.</summary>
        public TargetStatus TargetStatus { get; set; } = TargetStatus.Unknown;
    }
}
