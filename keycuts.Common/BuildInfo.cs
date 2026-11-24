using System;
using System.Linq;
using System.Reflection;

namespace keycuts.Common
{
    /// <summary>
    /// What the build says about itself beyond the version number. The SDK appends <c>+&lt;full commit&gt;</c> to the assembly's
    /// informational version when the build has git information; a source archive has none. The three-part <c>&lt;Version&gt;</c>
    /// never carries it: the MSI version, the update check and <c>keycuts --version</c> read the number alone.
    /// </summary>
    public static class BuildInfo
    {
        /// <summary>The length of the commit shown: git's default short form.</summary>
        public const int CommitLength = 7;

        /// <summary>The short commit this build was made from, or null when it has none.</summary>
        public static string Commit { get; } = ParseCommit(
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

        /// <summary>The first <see cref="CommitLength"/> characters of the hex commit after the <c>+</c> in an informational version; null for anything else.</summary>
        public static string ParseCommit(string informationalVersion)
        {
            var plus = informationalVersion?.IndexOf('+') ?? -1;
            if (plus < 0)
            {
                return null;
            }

            // The metadata can be dotted (a custom build's own): the commit is the first part
            var commit = informationalVersion.Substring(plus + 1).Split('.')[0];

            return commit.Length >= CommitLength && commit.All(Uri.IsHexDigit) ? commit.Substring(0, CommitLength).ToLowerInvariant() : null;
        }
    }
}
