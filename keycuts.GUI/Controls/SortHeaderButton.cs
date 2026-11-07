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

        private static void OnSortChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (SortHeaderButton)d;
            var state = GetState(button.Column, button.ActiveColumn, button.IsDescending);
            button.SetValue(StatePropertyKey, state);

            switch (state)
            {
                case SortHeaderState.Ascending:
                    AutomationProperties.SetHelpText(button, "Sorted ascending");
                    break;
                case SortHeaderState.Descending:
                    AutomationProperties.SetHelpText(button, "Sorted descending");
                    break;
                default:
                    button.ClearValue(AutomationProperties.HelpTextProperty);
                    break;
            }
        }
    }
}
