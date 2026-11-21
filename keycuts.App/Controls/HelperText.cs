using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;

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
    /// The line under a field (docs/styleguide.md, Type: Shared text styles; WPF's Controls/HelperText, ported): 12px secondary
    /// text, 4 below the control, that wraps. Its states change the color and, for a warning or a success, put a 12px icon before
    /// it, so color is never the only cue; a style alone can't add the icon, which is why this is a control. Normal and Success
    /// are polite live regions, Warning and Error assertive. In Avalonia a new text raises a name change by itself, which a screen
    /// reader announces (WPF needed LiveRegion.Announce), so a view that uses it doesn't announce the line. Keep the line in the
    /// tree and clear its text rather than hiding it: one that comes back from hidden isn't announced (docs/styleguide.md,
    /// In Avalonia). The look comes from its theme in Themes/Controls.axaml.
    /// </summary>
    public class HelperText : TemplatedControl
    {
        #region Properties

        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<HelperText, string>(nameof(Text), string.Empty);

        public static readonly StyledProperty<HelperTextState> StateProperty =
            AvaloniaProperty.Register<HelperText, HelperTextState>(nameof(State), HelperTextState.Normal,
                validate: state => System.Enum.IsDefined(state));

        #endregion

        static HelperText()
        {
            FocusableProperty.OverrideDefaultValue<HelperText>(false);
            IsTabStopProperty.OverrideDefaultValue<HelperText>(false);
        }

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public HelperTextState State
        {
            get => GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new TextAutomationPeer(this, nameof(HelperText), TextProperty, () => Text);
        }
    }
}
