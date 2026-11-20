using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;
using keycuts.Common;

namespace keycuts.App.Controls
{
    /// <summary>
    /// Compact "icon + label" badge for a keycut's display kind (WPF's Controls/TypeBadge, ported). The look comes from its theme
    /// in Themes/Controls.axaml.
    /// </summary>
    public class TypeBadge : TemplatedControl
    {
        #region Properties

        public static readonly StyledProperty<KeycutKind> KindProperty =
            AvaloniaProperty.Register<TypeBadge, KeycutKind>(nameof(Kind), KeycutKind.Command, validate: kind => Enum.IsDefined(kind));

        public static readonly DirectProperty<TypeBadge, string> LabelProperty =
            AvaloniaProperty.RegisterDirect<TypeBadge, string>(nameof(Label), o => o.Label);

        public static readonly DirectProperty<TypeBadge, IconKind> IconKindProperty =
            AvaloniaProperty.RegisterDirect<TypeBadge, IconKind>(nameof(IconKind), o => o.IconKind);

        #endregion

        string label = GetLabel(KeycutKind.Command);
        IconKind iconKind = GetIconKind(KeycutKind.Command);

        static TypeBadge()
        {
            FocusableProperty.OverrideDefaultValue<TypeBadge>(false);
            IsTabStopProperty.OverrideDefaultValue<TypeBadge>(false);
        }

        public KeycutKind Kind
        {
            get => GetValue(KindProperty);
            set => SetValue(KindProperty, value);
        }

        public string Label
        {
            get => label;
            private set => SetAndRaise(LabelProperty, ref label, value);
        }

        public IconKind IconKind
        {
            get => iconKind;
            private set => SetAndRaise(IconKindProperty, ref iconKind, value);
        }

        public static string GetLabel(KeycutKind kind)
        {
            return kind switch
            {
                KeycutKind.Url => "URL",
                KeycutKind.Folder => "Folder",
                KeycutKind.File => "File",
                KeycutKind.Command => "Command",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        public static IconKind GetIconKind(KeycutKind kind)
        {
            return kind switch
            {
                KeycutKind.Url => IconKind.Link,
                KeycutKind.Folder => IconKind.Folder,
                KeycutKind.File => IconKind.File,
                KeycutKind.Command => IconKind.Play,
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        public static string GetAutomationName(KeycutKind kind)
        {
            return "Type: " + GetLabel(kind);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == KindProperty)
            {
                var kind = change.GetNewValue<KeycutKind>();
                Label = GetLabel(kind);
                IconKind = GetIconKind(kind);
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new TextAutomationPeer(this, nameof(TypeBadge), KindProperty, () => GetAutomationName(Kind));
        }
    }
}
