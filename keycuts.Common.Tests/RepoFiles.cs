using System;
using System.IO;

namespace keycuts.Tests
{
    /// <summary>Finds a file in the repository from the test output folder. Shared with keycuts.Tests as a linked file.</summary>
    static class RepoFiles
    {
        public static string Find(string relativePath)
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
            {
                var candidate = Path.Combine(folder.FullName, relativePath);

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new FileNotFoundException(relativePath);
        }
    }
}
