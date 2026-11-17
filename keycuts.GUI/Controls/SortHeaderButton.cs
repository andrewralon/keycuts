using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI.Controls
{
    public enum SortHeaderState
    {
        None,
        Ascending,
        Descending
    }

    /// <summary>
    /// Column header button. Compares its <see cref="Column"/> with the active sort and exposes the result
    /// as <see cref="State"/> (for the chevron and accent styling) and as UI Automation help text.
    /// </summary>
    public class SortHeaderButton : Button
    {
        #region Dependency properties

        public static readonly DependencyProperty ColumnProperty = DependencyProperty.Register(
            nameof(Column), typeof(SortColumn), typeof(SortHeaderButton), new PropertyMetadata(SortColumn.Shortcut, OnSortChanged));

        public static readonly DependencyProperty ActiveColumnProperty = DependencyProperty.Register(
            nameof(ActiveColumn), typeof(SortColumn), typeof(SortHeaderButton), new PropertyMetadata(SortColumn.Shortcut, OnSortChanged));

        public static readonly DependencyProperty IsDescendingProperty = DependencyProperty.Register(
            nameof(IsDescending), typeof(bool), typeof(SortHeaderButton), new PropertyMetadata(false, OnSortChanged));

        private static readonly DependencyPropertyKey StatePropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(State), typeof(SortHeaderState), typeof(SortHeaderButton), new PropertyMetadata(SortHeaderState.None));

        public static readonly DependencyProperty StateProperty = StatePropertyKey.DependencyProperty;

        #endregion

        public SortColumn Column
        {
            get => (SortColumn)GetValue(ColumnProperty);
            set => SetValue(ColumnProperty, value);
        }

        public SortColumn ActiveColumn
        {
            get => (SortColumn)GetValue(ActiveColumnProperty);
            set => SetValue(ActiveColumnProperty, value);
        }

        public bool IsDescending
        {
            get => (bool)GetValue(IsDescendingProperty);
            set => SetValue(IsDescendingProperty, value);
        }

        public SortHeaderState State => (SortHeaderState)GetValue(StateProperty);

        public static SortHeaderState GetState(SortColumn column, SortColumn activeColumn, bool descending)
        {
            if (column != activeColumn)
            {
                return SortHeaderState.None;
            }

            return descending ? SortHeaderState.Descending : SortHeaderState.Ascending;
        }

        /// <summary>
        /// A line about the column (#235: "Counting since Nov 15, 2026" on Runs and Last run): the header's tooltip, and read
        /// after the sort state, so a screen reader hears what the tooltip shows. Empty shows no tooltip.
        /// </summary>
        public string Note
        {
            get => (string)GetValue(NoteProperty);
            set => SetValue(NoteProperty, value);
        }

        public static readonly DependencyProperty NoteProperty = DependencyProperty.Register(
            nameof(Note), typeof(string), typeof(SortHeaderButton), new PropertyMetadata(string.Empty, OnNoteChanged));

        /// <summary>The help text for a sort state and a note: either, both (the state first), or empty.</summary>
        public static string GetHelpText(SortHeaderState state, string? note)
        {
            var sorted = state switch
            {
                SortHeaderState.Ascending => "Sorted ascending",
                SortHeaderState.Descending => "Sorted descending",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(note))
            {
                return sorted;
            }

            return sorted.Length == 0 ? note : sorted + ". " + note;
        }

        private static void OnSortChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (SortHeaderButton)d;
            button.SetValue(StatePropertyKey, GetState(button.Column, button.ActiveColumn, button.IsDescending));
            button.UpdateHelpText();
        }

        private static void OnNoteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (SortHeaderButton)d;
            var note = e.NewValue as string;

            if (string.IsNullOrEmpty(note))
            {
                button.ClearValue(ToolTipProperty);
            }
            else
            {
                button.ToolTip = note;
            }

            button.UpdateHelpText();
        }

        private void UpdateHelpText()
        {
            var text = GetHelpText(State, Note);

            if (text.Length == 0)
            {
                ClearValue(AutomationProperties.HelpTextProperty);
            }
            else
            {
                AutomationProperties.SetHelpText(this, text);
            }
        }
    }
}
