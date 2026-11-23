using System;
using keycuts.Common;
using keycuts.GUI.Mvvm;

namespace keycuts.GUI.ViewModels
{
    /// <summary>Where Settings reads and sets the Run box shortcut (Phase 6.2): Carbon and the settings file in the app, a fake in tests.</summary>
    public interface IRunBoxShortcut
    {
        /// <summary>The shortcut chosen; null for none.</summary>
        RunBoxHotkey? Current { get; }

        /// <summary>0 when <see cref="Current"/> is registered (or there is none); otherwise why not (<c>-9878</c>: another app has it).</summary>
        int Status { get; }

        /// <summary>Stores and registers <paramref name="hotkey"/> (null: none). Returns <see cref="Status"/>.</summary>
        int Apply(RunBoxHotkey? hotkey);

        /// <summary>Lets go of the shortcut while a new one is recorded (or takes it back), so pressing it records instead of opening the box.</summary>
        void Pause(bool paused);

        /// <summary>Windows (issue #306): Ctrl, Alt, Shift and Win, written <c>Ctrl+Alt+Space</c>; otherwise macOS's ⌃⌥⇧⌘.</summary>
        bool UsesWindowsKeys => false;
    }

    /// <summary>The Run box section (Phase 6.2, mockup macos-settings-v1): the shortcut, recorded by pressing it.</summary>
    public partial class SettingsViewModel
    {
        public const string TakenError = "another app has it";

        /// <summary>Carbon's eventHotKeyExistsErr, and Windows' ERROR_HOTKEY_ALREADY_REGISTERED.</summary>
        private static bool IsTaken(int status) => status is -9878 or 1409;

        private bool WindowsKeys => runBoxShortcut?.UsesWindowsKeys == true;

        private string Describe(RunBoxHotkey hotkey) => WindowsKeys ? hotkey.WindowsDisplay : hotkey.Display;

        private IRunBoxShortcut? runBoxShortcut;

        private bool isRecordingShortcut;

        private RunBoxHotkey? refusedShortcut;

        /// <summary>macOS only: null hides the section.</summary>
        public IRunBoxShortcut? RunBoxShortcut
        {
            get { return runBoxShortcut; }
            init
            {
                runBoxShortcut = value;
                RecordShortcutCommand = new RelayCommand(() => IsRecordingShortcut = !IsRecordingShortcut);
            }
        }

        public bool IsRunBoxOffered
        {
            get { return runBoxShortcut != null; }
        }

        public RelayCommand? RecordShortcutCommand { get; private set; }

        /// <summary>While on, the next key with ⌘ or ⌃ (Ctrl, Alt or Win on Windows) becomes the shortcut (<see cref="RecordShortcut"/>); Esc cancels.</summary>
        public bool IsRecordingShortcut
        {
            get { return isRecordingShortcut; }
            set
            {
                if (runBoxShortcut == null || !SetProperty(ref isRecordingShortcut, value))
                {
                    return;
                }

                if (value)
                {
                    refusedShortcut = null;
                }

                runBoxShortcut.Pause(value);
                RaiseShortcut();
            }
        }

        /// <summary>What the recorder shows: the shortcut, "none", or the keys refused.</summary>
        public string ShortcutText
        {
            get
            {
                if (isRecordingShortcut)
                {
                    return WindowsKeys ? "Ctrl …" : "⌘ …";
                }

                return (refusedShortcut ?? runBoxShortcut?.Current) is { } shown ? Describe(shown) : "none";
            }
        }

        public bool ShortcutIsWarning
        {
            get { return !isRecordingShortcut && (refusedShortcut != null || (runBoxShortcut?.Current != null && runBoxShortcut.Status != 0)); }
        }

        public string ShortcutNote
        {
            get
            {
                if (runBoxShortcut == null)
                {
                    return string.Empty;
                }

                if (isRecordingShortcut)
                {
                    return "Press the keys. Esc cancels.";
                }

                if (refusedShortcut != null)
                {
                    return WindowsKeys ? "Add Ctrl, Alt or Win: Windows doesn’t allow Shift alone." : "Add ⌘ or ⌃: macOS doesn’t allow ⌥ or ⇧ alone.";
                }

                if (runBoxShortcut.Current is not { } current)
                {
                    return WindowsKeys ? "None. Open the Run box from the tray icon." : "None. Open the Run box from the menu bar.";
                }

                var name = Describe(current);
                return runBoxShortcut.Status switch
                {
                    0 => "Opens the Run box from any app.",
                    var status when IsTaken(status) => "Couldn’t use " + name + ": " + TakenError + ". Choose another.",
                    _ => "Couldn’t use " + name + ". Choose another."
                };
            }
        }

        /// <summary>The keys pressed while recording: taken when macOS allows them, otherwise shown as refused. Ends the recording.</summary>
        public void RecordShortcut(RunBoxHotkey hotkey)
        {
            if (runBoxShortcut == null || !isRecordingShortcut)
            {
                return;
            }

            isRecordingShortcut = false;
            if (WindowsKeys ? hotkey.IsAllowedOnWindows : hotkey.IsAllowed)
            {
                refusedShortcut = null;
                runBoxShortcut.Apply(hotkey);
            }
            else
            {
                refusedShortcut = hotkey;
                runBoxShortcut.Pause(false);
            }

            OnPropertyChanged(nameof(IsRecordingShortcut));
            RaiseShortcut();
        }

        /// <summary>Delete while recording: no shortcut; the menu bar still opens the box.</summary>
        public void ClearShortcut()
        {
            if (runBoxShortcut == null)
            {
                return;
            }

            isRecordingShortcut = false;
            refusedShortcut = null;
            runBoxShortcut.Apply(null);
            OnPropertyChanged(nameof(IsRecordingShortcut));
            RaiseShortcut();
        }

        private void RaiseShortcut()
        {
            OnPropertyChanged(nameof(ShortcutText));
            OnPropertyChanged(nameof(ShortcutNote));
            OnPropertyChanged(nameof(ShortcutIsWarning));
        }
    }
}
