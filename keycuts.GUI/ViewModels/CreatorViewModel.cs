using System;
using System.Windows.Input;
using keycuts.GUI.Mvvm;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// State of the compact creator. The layout and submit flow are reworked by KC-09 to KC-11.
    /// </summary>
    public class CreatorViewModel : ObservableObject
    {
        private string destination = string.Empty;

        private string shortcutName = string.Empty;

        private int keycutCount;

        public CreatorViewModel(ICommand showManagerCommand, ICommand openSettingsCommand)
        {
            ShowManagerCommand = showManagerCommand;
            OpenSettingsCommand = openSettingsCommand;
        }

        /// <summary>Raised after a keycut was created so the shell can refresh the count.</summary>
        public event EventHandler? KeycutCreated;

        public ICommand ShowManagerCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public string Destination
        {
            get { return destination; }
            set { SetProperty(ref destination, value ?? string.Empty); }
        }

        public string ShortcutName
        {
            get { return shortcutName; }
            set { SetProperty(ref shortcutName, value ?? string.Empty); }
        }

        public int KeycutCount
        {
            get { return keycutCount; }
            set { SetProperty(ref keycutCount, value); }
        }

        /// <summary>The destination field is focused unless one is already set, in which case the name field is.</summary>
        public bool FocusNameFieldOnShow
        {
            get { return !string.IsNullOrWhiteSpace(destination); }
        }

        public void Clear()
        {
            Destination = string.Empty;
            ShortcutName = string.Empty;
        }

        public void NotifyKeycutCreated()
        {
            KeycutCreated?.Invoke(this, EventArgs.Empty);
        }
    }
}
