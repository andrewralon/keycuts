using System.Threading.Tasks;
using keycuts.Common;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// The side effects behind the manager's row actions (launching processes, the clipboard, deleting files).
    /// Tests replace it with a fake so nothing real is launched, copied or deleted.
    /// </summary>
    public interface IKeycutActions
    {
        /// <summary>Runs the keycut's .bat file.</summary>
        void Launch(string path);

        /// <summary>Opens the keycut's .bat file in Notepad ("Edit in Notepad"). Throws <see cref="System.IO.FileNotFoundException"/> if the file is gone.</summary>
        void Edit(string path);

        /// <summary>
        /// Whether the Edit form can save this keycut without losing anything (#180): the emitter would write its file
        /// identically. Throws <see cref="KeycutRepositoryException"/> (<see cref="KeycutRepositoryError.NotFound"/>) if the file is gone.
        /// </summary>
        Task<bool> CanEditInFormAsync(Keycut keycut);

        /// <summary>
        /// Saves the Edit form: writes the keycut again and returns it as saved. Fills in the "use environment variables"
        /// setting for a changed target. Throws <see cref="KeycutRepositoryException"/> for what the form can explain.
        /// </summary>
        Task<Keycut> UpdateAsync(Keycut keycut, KeycutEdit edit);

        /// <summary>Opens Explorer with the keycut's .bat file selected. Throws <see cref="System.IO.FileNotFoundException"/> if the file is gone.</summary>
        void OpenFileLocation(string path);

        /// <summary>Puts the text on the clipboard. Throws if the clipboard can't be used.</summary>
        void CopyText(string text);

        /// <summary>
        /// Deletes the keycut file, to the Recycle Bin, and returns what <see cref="RestoreAsync"/> needs to put it back.
        /// Throws <see cref="KeycutRepositoryException"/> if that fails.
        /// </summary>
        Task<DeletedKeycut> DeleteAsync(Keycut keycut);

        /// <summary>
        /// Puts a deleted keycut back exactly as it was (#179). Throws <see cref="KeycutRepositoryException"/> if its name
        /// was taken again (<see cref="KeycutRepositoryError.AlreadyExists"/>: it is never overwritten) or it can't be written.
        /// </summary>
        Task RestoreAsync(DeletedKeycut deleted);

        /// <summary>Renames the keycut file and returns its new path. Throws <see cref="KeycutRepositoryException"/> if that fails.</summary>
        Task<string> RenameAsync(Keycut keycut, string newName);

        /// <summary>
        /// Changes the keycut's description in its file, rewriting only that line (#202), and returns the keycut as read back.
        /// Throws <see cref="KeycutRepositoryException"/> if the file is gone, isn't valid UTF-8, has changed, or can't be written.
        /// </summary>
        Task<Keycut> SetDescriptionAsync(Keycut keycut, string description);
    }

    /// <summary>
    /// The production implementation, which uses the shell, the clipboard and the configured output folder. The clipboard is each
    /// app's own (<see cref="SetClipboardText"/>: ViewModels/Wpf, keycuts.App).
    /// </summary>
    public partial class SystemKeycutActions : IKeycutActions
    {
        public void Launch(string path)
        {
            Desktops.Current.RunKeycut(path);
        }

        public void Edit(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                throw new System.IO.FileNotFoundException("The keycut file no longer exists.", path);
            }

            Desktops.Current.EditFile(path);
        }

        public void OpenFileLocation(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                throw new System.IO.FileNotFoundException("The keycut file no longer exists.", path);
            }

            Desktops.Current.RevealFile(path);
        }

        public void CopyText(string text)
        {
            SetClipboardText(text);
        }

        /// <summary>Puts the text on the clipboard; throws if it can't be used.</summary>
        private static partial void SetClipboardText(string text);

        public Task<DeletedKeycut> DeleteAsync(Keycut keycut)
        {
            return KeycutRepository.CreateDefault().DeleteAsync(keycut);
        }

        public Task RestoreAsync(DeletedKeycut deleted)
        {
            return KeycutRepository.CreateDefault().RestoreAsync(deleted);
        }

        public Task<bool> CanEditInFormAsync(Keycut keycut)
        {
            return KeycutRepository.CreateDefault().CanEditInFormAsync(keycut);
        }

        public Task<Keycut> UpdateAsync(Keycut keycut, KeycutEdit edit)
        {
            // A changed target is saved with environment variables if the setting says so, as a new keycut is
            return KeycutRepository.CreateDefault().UpdateAsync(keycut, edit with { UseEnvironmentVariables = new Runner().GetUseEnvironmentVariables() });
        }

        public Task<string> RenameAsync(Keycut keycut, string newName)
        {
            return KeycutRepository.CreateDefault().RenameAsync(keycut, newName);
        }

        public Task<Keycut> SetDescriptionAsync(Keycut keycut, string description)
        {
            return KeycutRepository.CreateDefault().SetDescriptionAsync(keycut, description);
        }
    }
}
