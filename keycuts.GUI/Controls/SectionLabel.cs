using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace keycuts.GUI.Controls
{
    /// <summary>
    /// A section label (docs/styleguide.md, Type): the small accent eyebrow over a group of settings or rows. WPF has no
    /// text-transform, so the text is given in sentence case (<c>Text="Output"</c>) and drawn in capitals; the automation name
    /// keeps the sentence case, so Narrator, speech users and the tests all get "Output", not "OUTPUT". It is a level 2 heading
    /// unless <c>AutomationProperties.HeadingLevel</c> says otherwise (level 3 inside a level 2, such as the icon picker's groups).
    /// The look comes from the default style in Themes/Controls.xaml.
    /// </summary>
    public class SectionLabel : Control
    {
        #region Dependency properties

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(SectionLabel), new FrameworkPropertyMetadata(string.Empty, OnTextChanged));

        private static readonly DependencyPropertyKey DisplayTextPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(DisplayText), typeof(string), typeof(SectionLabel), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty DisplayTextProperty = DisplayTextPropertyKey.DependencyProperty;

        #endregion

        static SectionLabel()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SectionLabel), new FrameworkPropertyMetadata(typeof(SectionLabel)));
            IsTabStopProperty.OverrideMetadata(typeof(SectionLabel), new FrameworkPropertyMetadata(false));
            FocusableProperty.OverrideMetadata(typeof(SectionLabel), new FrameworkPropertyMetadata(false));
        }

        /// <summary>The label in sentence case, as it is read out.</summary>
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        /// <summary>The label as it is drawn: <see cref="Text"/> in capitals.</summary>
        public string DisplayText => (string)GetValue(DisplayTextProperty);

        /// <summary>
        /// Capitals, the same on every machine: the labels are English, and a culture's own rules (Turkish's dotted capital I)
        /// would draw them differently.
        /// </summary>
        public static string ToDisplayText(string? text)
        {
            return (text ?? string.Empty).ToUpperInvariant();
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            d.SetValue(DisplayTextPropertyKey, ToDisplayText((string)e.NewValue));
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new SectionLabelAutomationPeer(this);
        }

        private sealed class SectionLabelAutomationPeer : FrameworkElementAutomationPeer
        {
            public SectionLabelAutomationPeer(SectionLabel owner) : base(owner)
            {
            }

            protected override string GetClassNameCore()
            {
                return nameof(SectionLabel);
            }

            protected override AutomationControlType GetAutomationControlTypeCore()
            {
                return AutomationControlType.Text;
            }

            protected override string GetNameCore()
            {
                var name = base.GetNameCore();
                return string.IsNullOrEmpty(name) ? ((SectionLabel)Owner).Text : name;
            }

            // The capitals drawn inside are the same words: exposing them too would read the label twice
            protected override List<AutomationPeer>? GetChildrenCore()
            {
                return null;
            }
        }
    }
}
