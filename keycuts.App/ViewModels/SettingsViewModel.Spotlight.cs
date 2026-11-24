using System;
using keycuts.Common;
using keycuts.GUI.Mvvm;

namespace keycuts.GUI.ViewModels
{
    /// <summary>The Spotlight row (macOS 26 and later, issue #328): adds the "Run keycut" Shortcut, so a quick key in Spotlight runs a keycut.</summary>
    public partial class SettingsViewModel
    {
        public const string SpotlightButtonText = "add to Shortcuts";

        /// <summary>What the row says. The quick key is added in Spotlight, since the Shortcut file can't carry it.</summary>
        public static readonly string SpotlightNote =
            $"Adds “{SpotlightShortcut.Name}” to Shortcuts. In Spotlight, press Add quick keys on it and type {SpotlightShortcut.QuickKey}; then “{SpotlightShortcut.QuickKey} gmail” runs a keycut.";

        private RelayCommand? addSpotlightShortcutCommand;

        /// <summary>The signed Shortcut file to open (<see cref="SpotlightShortcut.FindFile"/>); null hides the row.</summary>
        public string? SpotlightShortcutFile { get; init; }

        /// <summary>Opens a file with its app (<c>Desktops.Current.OpenTarget</c> in the app); tests pass a fake.</summary>
        public Action<string>? OpenSpotlightShortcut { get; init; }

        public bool IsSpotlightOffered
        {
            get { return SpotlightShortcutFile != null; }
        }

        /// <summary>Opens the file, so Shortcuts shows its own import sheet.</summary>
        public RelayCommand AddSpotlightShortcutCommand
        {
            get { return addSpotlightShortcutCommand ??= new RelayCommand(AddSpotlightShortcut, () => IsSpotlightOffered); }
        }

        private void AddSpotlightShortcut()
        {
            if (SpotlightShortcutFile == null || OpenSpotlightShortcut == null)
            {
                return;
            }

            try
            {
                OpenSpotlightShortcut(SpotlightShortcutFile);
                SaveError = null;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is System.IO.IOException || ex is InvalidOperationException)
            {
                SaveError = "Couldn’t open the shortcut in Shortcuts.";
            }
        }
    }
}
