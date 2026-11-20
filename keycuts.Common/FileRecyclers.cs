using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.VisualBasic.FileIO;

namespace keycuts.Common
{
    /// <summary>
    /// How a keycut's file is removed (#179). The app sends it to the Recycle Bin (<see cref="RecycleBinRecycler"/>);
    /// <see cref="PermanentDeleter"/> deletes it outright, which is what tests use so they never touch the real bin.
    /// </summary>
    public interface IFileRecycler
    {
        /// <summary>
        /// Removes the file and returns where it went when that's known (the Trash on macOS), so an Undo can move it back;
        /// null otherwise. Throws what <see cref="File.Delete"/> would when it can't.
        /// </summary>
        string Remove(string path);

        /// <summary>True if <see cref="Remove"/> keeps the file in the Recycle Bin, so the caller can say when it doesn't.</summary>
        bool UsesRecycleBin(string path);
    }

    /// <summary>
    /// Moves the file to the Recycle Bin. <c>Microsoft.VisualBasic.FileIO</c> is in the base runtime, so this needs no
    /// package and no WinForms. The shell shows its own error dialog if it can't (a file that's open elsewhere), and a
    /// drive without a Recycle Bin (a network drive, a USB stick) deletes for good: see <see cref="HasRecycleBin"/>.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class RecycleBinRecycler : IFileRecycler
    {
        public string Remove(string path)
        {
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            return null;
        }

        public bool UsesRecycleBin(string path)
        {
            return HasRecycleBin(path);
        }

        /// <summary>
        /// Whether the path's drive has a Recycle Bin: a fixed local drive does. Network shares and removable drives don't,
        /// and the shell then deletes without a word, so this is how the caller knows to say so.
        /// </summary>
        public static bool HasRecycleBin(string path)
        {
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(path));

                // A UNC root (\\server\share) isn't a drive, so DriveInfo refuses it: no bin
                return !string.IsNullOrEmpty(root) && new DriveInfo(root).DriveType == DriveType.Fixed;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is NotSupportedException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// The macOS Trash, through <c>[NSFileManager trashItemAtURL:resultingItemURL:error:]</c>, so Finder's Put Back works. A
    /// volume with no Trash makes it fail, and the file stays (nothing is deleted for good without a word). It returns where the
    /// file landed in the Trash, so the Manager's Undo moves it back from there: Finder has no Put Back to call.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public sealed class TrashRecycler : IFileRecycler
    {
        public string Remove(string path)
        {
            using (ObjC.AutoreleasePool())
            {
                var url = ObjC.Send(ObjC.GetClass("NSURL"), ObjC.Selector("fileURLWithPath:"), ObjC.String(Path.GetFullPath(path)));
                var manager = ObjC.Send(ObjC.GetClass("NSFileManager"), ObjC.Selector("defaultManager"));

                if (!ObjC.SendBool(manager, ObjC.Selector("trashItemAtURL:resultingItemURL:error:"), url, out var trashed, out var error))
                {
                    throw new IOException(ObjC.ErrorText(error) ?? $"Couldn't move \"{path}\" to the Trash.");
                }

                return trashed == IntPtr.Zero ? null : ObjC.ToManaged(ObjC.Send(trashed, ObjC.Selector("path")));
            }
        }

        public bool UsesRecycleBin(string path)
        {
            return true;
        }
    }

    /// <summary>Deletes the file for good. The default of <see cref="KeycutRepository"/>'s plain constructor, and what tests use.</summary>
    public sealed class PermanentDeleter : IFileRecycler
    {
        public string Remove(string path)
        {
            File.Delete(path);
            return null;
        }

        public bool UsesRecycleBin(string path)
        {
            return false;
        }
    }
}
