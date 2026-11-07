using System;

namespace keycuts.Common
{
    public enum DestinationError
    {
        None,
        Empty,
        NotFound,
        AccessDenied,
        Unsupported,
        Timeout
    }

    public class DestinationResult
    {
        public bool IsValid { get; private set; }

        public KeycutKind Kind { get; private set; }

        public ShortcutType RawType { get; private set; }

        public string Destination { get; private set; }

        public string DisplayText { get; private set; }

        public DestinationError Error { get; private set; }

        public static DestinationResult Valid(ShortcutType type, string destination)
        {
            return new DestinationResult
            {
                IsValid = true,
                Kind = KeycutKinds.FromShortcutType(type),
                RawType = type,
                Destination = destination,
                DisplayText = destination,
                Error = DestinationError.None
            };
        }

        public static DestinationResult Invalid(DestinationError error, string rawText)
        {
            return new DestinationResult
            {
                IsValid = false,
                Kind = KeycutKinds.FromShortcutType(ShortcutType.Unknown),
                RawType = ShortcutType.Unknown,
                Destination = rawText ?? string.Empty,
                DisplayText = rawText ?? string.Empty,
                Error = error
            };
        }
    }
}
