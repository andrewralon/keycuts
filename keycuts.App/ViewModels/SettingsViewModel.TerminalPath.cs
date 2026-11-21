using System;
using System.IO;
using keycuts.Common;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// "Run keycuts in Terminal" (Phase 5.4, mockup macos-settings-v1): the output folder's block in the shell profile, as
    /// <c>keycuts path add</c> and <c>remove</c> write it (Phase 4.9). macOS only; the row is hidden elsewhere.
    /// </summary>
    public partial class SettingsViewModel
    {
        public const string RunInTerminalName = "Run keycuts in Terminal";

        public const string TerminalPathOnElsewhereNote = "It’s already on PATH, so there’s nothing to add.";

        public const string TerminalPathAddedNote = "Open a new Terminal window to use it.";

        private readonly MacShellPath? shellPath;

        private bool terminalPathJustAdded;

        /// <summary>Whether the row is shown: macOS, outside the Debug folder override.</summary>
        public bool IsTerminalPathOffered
        {
            get { return shellPath != null; }
        }

        /// <summary>On while Terminal finds the folder or the profile's block names it; turning it on or off writes the profile.</summary>
        public bool RunInTerminal
        {
            get { return shellPath != null && shellPath.Status(committedFolder) != ShellPathStatus.NotOnPath; }
            set
            {
                if (shellPath == null || value == RunInTerminal || !CanChangeTerminalPath)
                {
                    return;
                }

                var done = value ? shellPath.Add(committedFolder) : shellPath.Remove();
                terminalPathJustAdded = value && done;
                SaveError = done ? null : "Couldn’t update " + ProfileName + ".";
                RaiseTerminalPath();
            }
        }

        /// <summary>Off when the shell isn't one keycuts edits (fish, nushell) and when something other than keycuts put the folder on PATH.</summary>
        public bool CanChangeTerminalPath
        {
            get { return shellPath?.ProfilePath != null && !IsOnPathElsewhere; }
        }

        /// <summary>The row's note: what the switch does, or why it can't.</summary>
        public string TerminalPathNote
        {
            get
            {
                if (shellPath == null)
                {
                    return string.Empty;
                }

                if (shellPath.ProfilePath == null)
                {
                    return shellPath.Instructions(committedFolder) ?? string.Empty;
                }

                if (IsOnPathElsewhere)
                {
                    return TerminalPathOnElsewhereNote;
                }

                return terminalPathJustAdded && RunInTerminal ? TerminalPathAddedNote : "Adds the folder to PATH in " + ProfileName + ".";
            }
        }

        private string ProfileName
        {
            get { return "~/" + Path.GetFileName(shellPath?.ProfilePath ?? ".zprofile"); }
        }

        // On PATH (this process's or /etc/paths) without the profile's block naming this folder
        private bool IsOnPathElsewhere
        {
            get
            {
                return shellPath != null && shellPath.Status(committedFolder) == ShellPathStatus.OnPath &&
                    !MacShellPath.IsSameFolder(shellPath.ReadBlockFolder(), committedFolder);
            }
        }

        private void RaiseTerminalPath()
        {
            OnPropertyChanged(nameof(RunInTerminal));
            OnPropertyChanged(nameof(CanChangeTerminalPath));
            OnPropertyChanged(nameof(TerminalPathNote));
        }
    }
}
