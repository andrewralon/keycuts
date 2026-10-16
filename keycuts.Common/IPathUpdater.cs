using System;

namespace keycuts.Common
{
    /// <summary>Seam over the system PATH update that happens whenever a keycut is created.</summary>
    public interface IPathUpdater
    {
        /// <summary>Makes sure <paramref name="newPath"/> is on the PATH (replacing <paramref name="oldPath"/> if present). False on failure.</summary>
        bool AddToOrReplace(string oldPath, string newPath);
    }

    /// <summary>Updates the real machine PATH. Never throws; failures (such as missing admin rights) return false.</summary>
    public class SystemPathUpdater : IPathUpdater
    {
        public bool AddToOrReplace(string oldPath, string newPath)
        {
            try
            {
                return PathSetup.AddToOrReplaceInSystemPath(oldPath, newPath);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>Leaves the PATH alone. Used by tests.</summary>
    public class NoOpPathUpdater : IPathUpdater
    {
        public bool AddToOrReplace(string oldPath, string newPath)
        {
            return true;
        }
    }
}
