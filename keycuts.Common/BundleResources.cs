using System;
using System.IO;

namespace keycuts.Common
{
    /// <summary>
    /// Where the files shipped with the app live. In a macOS bundle they are in <c>Contents/Resources</c>, not beside the executable:
    /// everything in <c>Contents/MacOS</c> counts as code to sign, and Apple's notary refused the app with data folders there
    /// (the main executable's signature "invalid"). Everywhere else, and in a dev build, they sit beside the executable.
    /// </summary>
    public static class BundleResources
    {
        /// <summary>The folder <paramref name="name"/> in the bundle's Resources if it has one, else beside the executable.</summary>
        public static string Folder(string name)
        {
            return Folder(AppContext.BaseDirectory, name, Directory.Exists);
        }

        public static string Folder(string baseDirectory, string name, Func<string, bool> directoryExists)
        {
            if (OperatingSystem.IsMacOS())
            {
                var bundled = Path.GetFullPath(Path.Combine(baseDirectory, "..", "Resources", name));
                if (directoryExists(bundled))
                {
                    return bundled;
                }
            }

            return Path.Combine(baseDirectory, name);
        }
    }
}
