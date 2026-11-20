using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
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
    /// Column header button (WPF's Controls/SortHeaderButton, ported). Compares its <see cref="Column"/> with the active sort and
    /// exposes the result as <see cref="State"/> (for the chevron and accent styling) and as UI Automation help text. Its look is
    /// the Manager's (Phase 3.7); until then it is a plain button.
    /// </summary>
    public class SortHeaderButton : Button
    {
        #region Properties

        public static readonly StyledProperty<SortColumn> ColumnProperty =
            AvaloniaProperty.Register<SortHeaderButton, SortColumn>(nameof(Column), SortColumn.Shortcut);

        public static readonly StyledProperty<SortColumn> ActiveColumnProperty =
            AvaloniaProperty.Register<SortHeaderButton, SortColumn>(nameof(ActiveColumn), SortColumn.Shortcut);

        public static readonly StyledProperty<bool> IsDescendingProperty =
            AvaloniaProperty.Register<SortHeaderButton, bool>(nameof(IsDescending));

        public static readonly DirectProperty<SortHeaderButton, SortHeaderState> StateProperty =
            AvaloniaProperty.RegisterDirect<SortHeaderButton, SortHeaderState>(nameof(State), o => o.State);

        public static readonly StyledProperty<string> NoteProperty =
            AvaloniaProperty.Register<SortHeaderButton, string>(nameof(Note), string.Empty);

        #endregion

        SortHeaderState state;

        protected override Type StyleKeyOverride => typeof(Button);

        public SortColumn Column
        {
            get => GetValue(ColumnProperty);
            set => SetValue(ColumnProperty, value);
        }

        public SortColumn ActiveColumn
        {
            get => GetValue(ActiveColumnProperty);
            set => SetValue(ActiveColumnProperty, value);
        }

        public bool IsDescending
        {
            get => GetValue(IsDescendingProperty);
            set => SetValue(IsDescendingProperty, value);
        }

        public SortHeaderState State
        {
            get => state;
            private set => SetAndRaise(StateProperty, ref state, value);
        }

        /// <summary>
        /// A line about the column (#235: "Counting since Nov 15, 2026" on Runs and Last run): the header's tooltip, and read
        /// after the sort state, so a screen reader hears what the tooltip shows. Empty shows no tooltip.
        /// </summary>
        public string Note
        {
            get => GetValue(NoteProperty);
            set => SetValue(NoteProperty, value);
        }

        public static SortHeaderState GetState(SortColumn column, SortColumn activeColumn, bool descending)
        {
            if (column != activeColumn)
            {
                return SortHeaderState.None;
            }

            return descending ? SortHeaderState.Descending : SortHeaderState.Ascending;
        }

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

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ColumnProperty || change.Property == ActiveColumnProperty || change.Property == IsDescendingProperty)
            {
                State = GetState(Column, ActiveColumn, IsDescending);
                UpdateHelpText();
            }
            else if (change.Property == NoteProperty)
            {
                var note = change.GetNewValue<string?>();
                if (string.IsNullOrEmpty(note))
                {
                    ClearValue(ToolTip.TipProperty);
                }
                else
                {
                    ToolTip.SetTip(this, note);
                }

                UpdateHelpText();
            }
        }

        void UpdateHelpText()
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
