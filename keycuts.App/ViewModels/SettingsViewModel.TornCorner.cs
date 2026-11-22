namespace keycuts.GUI.ViewModels
{
    /// <summary>Where Settings reads and sets the macOS torn corner (issue #292): the settings file and the window in the app, a fake in tests.</summary>
    public interface ITornCorner
    {
        bool IsOn { get; }

        /// <summary>Turns it on or off and shows it now; a message when it couldn't be saved, otherwise null.</summary>
        string? Set(bool on);
    }

    /// <summary>"Torn corner" (issue #292): macOS keeps its native window unless this is on; Windows always has the corner.</summary>
    public partial class SettingsViewModel
    {
        private ITornCorner? tornCorner;

        /// <summary>macOS only: null hides the row.</summary>
        public ITornCorner? TornCornerOption
        {
            get { return tornCorner; }
            init { tornCorner = value; }
        }

        public bool IsTornCornerOffered
        {
            get { return tornCorner != null; }
        }

        public bool TornCorner
        {
            get { return tornCorner?.IsOn == true; }
            set
            {
                if (tornCorner == null || value == TornCorner)
                {
                    return;
                }

                SaveError = tornCorner.Set(value);
                OnPropertyChanged(nameof(TornCorner));
            }
        }
    }
}
