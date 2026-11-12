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
