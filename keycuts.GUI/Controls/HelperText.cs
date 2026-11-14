using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using keycuts.GUI.Views;

namespace keycuts.GUI.Controls
{
    /// <summary>What a <see cref="HelperText"/> line is saying.</summary>
    public enum HelperTextState
    {
        Normal,
        Warning,
        Error,
        Success
    }

    /// <summary>
    /// The line under a field (docs/styleguide.md, Type: Shared text styles): 12px secondary text, 4 below the control, that
    /// wraps. Its states change the color and, for a warning or a success, put a 12px icon before it, so color is never the only
    /// cue; a style alone can't add the icon, which is why this is a control. Normal and Success are polite live regions, Warning
    /// and Error assertive, and a new text is announced (<see cref="LiveRegion.Announce"/>): a live setting alone raises nothing
    /// in WPF. So a view that uses it doesn't announce the line itself. The look comes from the default style in Themes/Controls.xaml.
    /// </summary>
    public class HelperText : Control
    {
        #region Dependency properties

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(HelperText), new FrameworkPropertyMetadata(string.Empty, OnTextChanged));

        public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
            nameof(State), typeof(HelperTextState), typeof(HelperText), new FrameworkPropertyMetadata(HelperTextState.Normal),
            value => System.Enum.IsDefined((HelperTextState)value));

        #endregion

        static HelperText()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(HelperText), new FrameworkPropertyMetadata(typeof(HelperText)));
            IsTabStopProperty.OverrideMetadata(typeof(HelperText), new FrameworkPropertyMetadata(false));
            FocusableProperty.OverrideMetadata(typeof(HelperText), new FrameworkPropertyMetadata(false));
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public HelperTextState State
        {
            get => (HelperTextState)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        // Not while the view is being built (the first text isn't news), and not when the line empties
        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var helper = (HelperText)d;
            if (helper.IsLoaded && !string.IsNullOrEmpty((string)e.NewValue))
            {
                LiveRegion.Announce(helper);
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new HelperTextAutomationPeer(this);
        }

        private sealed class HelperTextAutomationPeer : FrameworkElementAutomationPeer
        {
            public HelperTextAutomationPeer(HelperText owner) : base(owner)
            {
            }

            protected override string GetClassNameCore()
            {
                return nameof(HelperText);
            }

            protected override AutomationControlType GetAutomationControlTypeCore()
            {
                return AutomationControlType.Text;
            }

            protected override string GetNameCore()
            {
                var name = base.GetNameCore();
                return string.IsNullOrEmpty(name) ? ((HelperText)Owner).Text : name;
            }

            // The text drawn inside is the name already, and the icon only repeats the state
            protected override List<AutomationPeer>? GetChildrenCore()
            {
                return null;
            }
        }
    }
}
