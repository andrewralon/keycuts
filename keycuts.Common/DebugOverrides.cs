using System;
using System.IO;

namespace keycuts.Common
{
    /// <summary>
    /// Debug-only developer overrides. In release builds every member is inert, so nothing here can redirect a shipped app.
    /// </summary>
    public static class DebugOverrides
    {
        public const string OutputFolderVariable = "KEYCUTS_OUTPUT_FOLDER";

        /// <summary>
        /// The folder named by KEYCUTS_OUTPUT_FOLDER, or null. Always null in release builds.
        /// The override replaces the registry value without ever reading or writing it.
        /// </summary>
        public static string GetOutputFolder()
        {
#if DEBUG
            return ParseOutputFolder(Environment.GetEnvironmentVariable(OutputFolderVariable));
#else
            return null;
#endif
        }

        /// <summary>Returns the trimmed absolute path, or null when the value is empty or not a fully qualified path.</summary>
        public static string ParseOutputFolder(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim().Trim('"');

            if (trimmed.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || !Path.IsPathFullyQualified(trimmed))
            {
                return null;
            }

            return trimmed;
        }

        internal static bool IsOverride(string folder)
        {
            var overrideFolder = GetOutputFolder();

            return overrideFolder != null && string.Equals(overrideFolder, folder, StringComparison.OrdinalIgnoreCase);
        }
    }
}
