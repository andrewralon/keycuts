using System;
using System.IO;
using System.Security;
using keycuts.Common;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// Where the Creator remembers whether its description box is shown (#205). The real one is the <c>ShowDescription</c>
    /// value in <c>HKCU</c>; tests pass a fake and never touch the registry.
    /// </summary>
    public interface IDescriptionVisibilityStore
    {
        /// <summary>The last saved state; false when nothing was saved or it can't be read. Never writes.</summary>
        bool Load();

        /// <summary>Saves the state. A store that can't save does so quietly: forgetting the choice is not worth an error.</summary>
        void Save(bool shown);
    }

    public class RegistryDescriptionVisibilityStore : IDescriptionVisibilityStore
    {
        public bool Load()
        {
            return RegistryStuff.GetShowDescription();
        }

        public void Save(bool shown)
        {
            try
            {
                RegistryStuff.SetShowDescription(shown);
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is SecurityException || e is IOException)
            {
                // A locked-down registry just means the choice isn't remembered
                System.Diagnostics.Debug.WriteLine("keycuts: couldn't save whether the description is shown: " + e.Message);
            }
        }
    }
}
