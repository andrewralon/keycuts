using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;

namespace keycuts.GUI.Controls
{
    /// <summary>
    /// A section label (docs/styleguide.md, Type): the small accent eyebrow over a group of settings or rows (WPF's
    /// Controls/SectionLabel, ported). Avalonia has no text-transform either, so the text is given in sentence case
    /// (<c>Text="Output"</c>) and drawn in capitals; the automation name keeps the sentence case, so screen readers, speech users
    /// and the tests all get "Output", not "OUTPUT". It is a level 2 heading unless <c>AutomationProperties.HeadingLevel</c> says
    /// otherwise (3 inside a level 2, such as the icon picker's groups). The look comes from its theme in Themes/Controls.axaml.
    /// </summary>
    public class SectionLabel : TemplatedControl
    {
        #region Properties

        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<SectionLabel, string>(nameof(Text), string.Empty);

        public static readonly DirectProperty<SectionLabel, string> DisplayTextProperty =
            AvaloniaProperty.RegisterDirect<SectionLabel, string>(nameof(DisplayText), o => o.DisplayText);

        #endregion

        string displayText = string.Empty;

        static SectionLabel()
        {
            FocusableProperty.OverrideDefaultValue<SectionLabel>(false);
            IsTabStopProperty.OverrideDefaultValue<SectionLabel>(false);
        }

        /// <summary>The label in sentence case, as it is read out.</summary>
        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        /// <summary>The label as it is drawn: <see cref="Text"/> in capitals.</summary>
        public string DisplayText
        {
            get => displayText;
            private set => SetAndRaise(DisplayTextProperty, ref displayText, value);
        }

        /// <summary>
        /// Capitals, the same on every machine: the labels are English, and a culture's own rules (Turkish's dotted capital I)
        /// would draw them differently.
        /// </summary>
        public static string ToDisplayText(string? text)
        {
            return (text ?? string.Empty).ToUpperInvariant();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == TextProperty)
            {
                DisplayText = ToDisplayText(change.GetNewValue<string?>());
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new TextAutomationPeer(this, nameof(SectionLabel), TextProperty, () => Text);
        }
    }

    /// <summary>
    /// The automation peer of a control that reads as one piece of text (<see cref="SectionLabel"/>, <see cref="HelperText"/>,
    /// <see cref="TypeBadge"/>): its name is the text, unless AutomationProperties.Name says otherwise, and the parts drawn inside
    /// aren't exposed, so the text isn't read twice. A change of the text raises a change of the name, as Avalonia's own TextBlock
    /// peer does, which is what a screen reader announces for a live region.
    /// </summary>
    sealed class TextAutomationPeer : ControlAutomationPeer
    {
        readonly string className;
        readonly Func<string> text;

        public TextAutomationPeer(TemplatedControl owner, string className, AvaloniaProperty textProperty, Func<string> text)
            : base(owner)
        {
            this.className = className;
            this.text = text;
            owner.PropertyChanged += (_, e) =>
            {
                if (e.Property == textProperty)
                {
                    RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, null, GetName());
                }
            };
        }

        protected override string GetClassNameCore()
        {
            return className;
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Text;
        }

        protected override string? GetNameCore()
        {
            var name = base.GetNameCore();
            return string.IsNullOrEmpty(name) ? text() : name;
        }

        protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore()
        {
            return Array.Empty<AutomationPeer>();
        }
    }
}
