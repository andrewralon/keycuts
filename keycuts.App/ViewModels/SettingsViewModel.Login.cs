using System;

namespace keycuts.GUI.ViewModels
{
    /// <summary>Where Settings reads and sets Open at login (Phase 6.5): <c>SMAppService</c> in the app, a fake in tests.</summary>
    public interface ILoginItem
    {
        bool IsOn { get; }

        /// <summary>Registered, but System Settings → General → Login Items has it switched off.</summary>
        bool NeedsApproval { get; }

        /// <summary>Turns it on or off; a message when macOS refused, otherwise null.</summary>
        string? Set(bool on);
    }

    /// <summary>"Open at login" (Phase 6.5): keycuts starts in the menu bar, so the Run box is ready after a restart.</summary>
    public partial class SettingsViewModel
    {
        public const string OpenAtLoginNote = "keycuts waits in the menu bar (about 90 MB).";

        public const string OpenAtLoginApprovalNote = "Turn keycuts on in System Settings → General → Login Items.";

        private ILoginItem? loginItem;

        /// <summary>macOS only: null hides the row.</summary>
        public ILoginItem? LoginItem
        {
            get { return loginItem; }
            init { loginItem = value; }
        }

        public bool IsOpenAtLoginOffered
        {
            get { return loginItem != null; }
        }

        public bool OpenAtLogin
        {
            get { return loginItem?.IsOn == true; }
            set
            {
                if (loginItem == null || value == OpenAtLogin)
                {
                    return;
                }

                SaveError = loginItem.Set(value);
                OnPropertyChanged(nameof(OpenAtLogin));
                OnPropertyChanged(nameof(OpenAtLoginText));
            }
        }

        public string OpenAtLoginText
        {
            get { return loginItem?.NeedsApproval == true ? OpenAtLoginApprovalNote : OpenAtLoginNote; }
        }
    }
}
