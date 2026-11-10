using System;
using keycuts.Common;

namespace keycuts.Tests
{
    /// <summary>Name checks (#153) for view model tests, so none of them read the real PATH, registry or output folder.</summary>
    static class TestNameChecks
    {
        public const string OldDestination = "https://old.example.com";

        public const string ConflictPath = @"C:\Program Files\Tool\ex.exe";

        /// <summary>A check that finds nothing.</summary>
        public static readonly Func<string, NameCheck> NoWarnings = name => NameCheck.None(name);

        public static NameCheck Existing(string name, string destination = OldDestination)
        {
            return new NameCheck(name, new Keycut { Name = name, Destination = destination }, null);
        }

        public static NameCheck Conflict(string name, NameConflictEffect effect = NameConflictEffect.Shadows, string path = ConflictPath)
        {
            var source = effect == NameConflictEffect.Hidden ? NameConflictSource.SystemFolder : NameConflictSource.AppPaths;
            return new NameCheck(name, null, new[] { new NameConflict(path, effect, source) });
        }
    }
}
