using System;

namespace keycuts.GUI.ViewModels
{
    /// <summary>Where Settings reads and sets Open at login (Phase 6.5): <c>SMAppService</c> on macOS, the Run key on Windows, a fake in tests.</summary>
    public interface ILoginItem
    {
        bool IsOn { get; }

        /// <summary>Registered, but the system's list of login items (System Settings on macOS, Startup apps on Windows) has it switched off.</summary>
        bool NeedsApproval { get; }

        /// <summary>Turns it on or off; a message when macOS refused, otherwise null.</summary>
        string? Set(bool on);
    }

    /// <summary>"Open at login" (Phase 6.5): keycuts starts in the menu bar, so the Run box is ready after a restart.</summary>
    public partial class SettingsViewModel
    {
        /// <summary>Where keycuts waits after the login: the menu bar on macOS, the system tray on Windows (issue #307).</summary>
        public static readonly string OpenAtLoginNote = PlatformKeys.IsMac ? "keycuts waits in the menu bar." : "keycuts waits in the system tray.";

        /// <summary>Where the user turned the item off: System Settings on macOS, Task Manager's Startup apps (Settings) on Windows.</summary>
        public static readonly string OpenAtLoginApprovalNote = PlatformKeys.IsMac
            ? "Turn keycuts on in System Settings → General → Login Items."
            : "Turn keycuts on in Settings → Apps → Startup.";

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
