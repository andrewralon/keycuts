using System;

namespace keycuts.Common
{
    public enum TargetError
    {
        None,
        Empty,
        NotFound,
        AccessDenied,
        Unsupported,
        Timeout
    }

    public class TargetResult
    {
        public bool IsValid { get; private set; }

        public KeycutKind Kind { get; private set; }

        public ShortcutType RawType { get; private set; }

        public string Target { get; private set; }

        public string DisplayText { get; private set; }

        public TargetError Error { get; private set; }

        public static TargetResult Valid(ShortcutType type, string target)
        {
            return new TargetResult
            {
                IsValid = true,
                Kind = KeycutKinds.FromShortcutType(type),
                RawType = type,
                Target = target,
                DisplayText = target,
                Error = TargetError.None
            };
        }

        public static TargetResult Invalid(TargetError error, string rawText)
        {
            return new TargetResult
            {
                IsValid = false,
                Kind = KeycutKinds.FromShortcutType(ShortcutType.Unknown),
                RawType = ShortcutType.Unknown,
                Target = rawText ?? string.Empty,
                DisplayText = rawText ?? string.Empty,
                Error = error
            };
        }
    }
}
