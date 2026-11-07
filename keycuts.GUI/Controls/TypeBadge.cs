using System;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using keycuts.Common;

namespace keycuts.GUI.Controls
{
    /// <summary>
    /// Compact "icon + label" badge for a keycut's display kind. The look comes from the default style in Themes/Controls.xaml.
    /// </summary>
    public class TypeBadge : Control
    {
        #region Dependency properties

        public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
            nameof(Kind), typeof(KeycutKind), typeof(TypeBadge),
            new FrameworkPropertyMetadata(KeycutKind.Command, OnKindChanged),
            value => Enum.IsDefined((KeycutKind)value));

        private static readonly DependencyPropertyKey LabelPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(Label), typeof(string), typeof(TypeBadge), new PropertyMetadata(GetLabel(KeycutKind.Command)));

        public static readonly DependencyProperty LabelProperty = LabelPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey IconKindPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(IconKind), typeof(IconKind), typeof(TypeBadge), new PropertyMetadata(GetIconKind(KeycutKind.Command)));

        public static readonly DependencyProperty IconKindProperty = IconKindPropertyKey.DependencyProperty;

        #endregion

        static TypeBadge()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TypeBadge), new FrameworkPropertyMetadata(typeof(TypeBadge)));
            IsTabStopProperty.OverrideMetadata(typeof(TypeBadge), new FrameworkPropertyMetadata(false));
            FocusableProperty.OverrideMetadata(typeof(TypeBadge), new FrameworkPropertyMetadata(false));
        }

        public KeycutKind Kind
        {
            get => (KeycutKind)GetValue(KindProperty);
            set => SetValue(KindProperty, value);
        }

        public string Label => (string)GetValue(LabelProperty);

        public IconKind IconKind => (IconKind)GetValue(IconKindProperty);

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

        private static void OnKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var kind = (KeycutKind)e.NewValue;
            d.SetValue(LabelPropertyKey, GetLabel(kind));
            d.SetValue(IconKindPropertyKey, GetIconKind(kind));
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new TypeBadgeAutomationPeer(this);
        }

        private sealed class TypeBadgeAutomationPeer : FrameworkElementAutomationPeer
        {
            public TypeBadgeAutomationPeer(TypeBadge owner) : base(owner)
            {
            }

            protected override string GetClassNameCore()
            {
                return nameof(TypeBadge);
            }

            protected override AutomationControlType GetAutomationControlTypeCore()
            {
                return AutomationControlType.Text;
            }

            protected override string GetNameCore()
            {
                return GetAutomationName(((TypeBadge)Owner).Kind);
            }
        }
    }
}
