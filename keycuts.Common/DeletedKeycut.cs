using System;

namespace keycuts.Common
{
    /// <summary>
    /// A keycut that was just deleted, with what it takes to put it back (#179): its exact bytes and last write time,
    /// kept in memory. See <see cref="KeycutRepository.RestoreAsync"/>. The Recycle Bin is the backstop once the Undo is gone.
    /// </summary>
    public sealed class DeletedKeycut
    {
        public DeletedKeycut(Keycut keycut, byte[] contents, DateTime modified, bool recycled)
        {
            Keycut = keycut ?? throw new ArgumentNullException(nameof(keycut));
            Contents = contents ?? throw new ArgumentNullException(nameof(contents));
            Modified = modified;
            Recycled = recycled;
        }

        /// <summary>The keycut as the manager showed it; its <see cref="Common.Keycut.FilePath"/> is where it goes back.</summary>
        public Keycut Keycut { get; }

        /// <summary>The file's bytes as they were, so a restore keeps every hand edit and the file's old tags.</summary>
        public byte[] Contents { get; }

        /// <summary>The file's last write time when it was deleted; <c>default</c> if unknown. A restore sets it again.</summary>
        public DateTime Modified { get; }

        /// <summary>True if the file went to the Recycle Bin; false if it was deleted for good (a drive without one).</summary>
        public bool Recycled { get; }
    }
}
