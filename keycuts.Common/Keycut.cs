namespace keycuts.Common
{
    public enum DestinationStatus
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

        public string Destination { get; init; }

        public string FilePath { get; init; }

        /// <summary>Set in the background by <see cref="KeycutRepository.CheckDestinationsAsync"/>.</summary>
        public DestinationStatus DestinationStatus { get; set; } = DestinationStatus.Unknown;
    }
}
