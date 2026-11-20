using Avalonia;
using Avalonia.Controls;

namespace keycuts.App.Themes
{
    /// <summary>
    /// Attached properties that let views drive the themed TextBox chrome (WPF's TextBoxChrome, ported). The placeholder is
    /// Avalonia's own <see cref="TextBox.PlaceholderText"/>, which the theme draws.
    /// </summary>
    public sealed class TextBoxChrome
    {
        public static readonly AttachedProperty<bool> HasErrorProperty =
            AvaloniaProperty.RegisterAttached<TextBoxChrome, TextBox, bool>("HasError");

        /// <summary>An amber border for a warning to confirm, such as a name another command uses (#153). An error wins.</summary>
        public static readonly AttachedProperty<bool> HasWarningProperty =
            AvaloniaProperty.RegisterAttached<TextBoxChrome, TextBox, bool>("HasWarning");

        TextBoxChrome()
        {
        }

        public static bool GetHasError(TextBox textBox) => textBox.GetValue(HasErrorProperty);

        public static void SetHasError(TextBox textBox, bool value) => textBox.SetValue(HasErrorProperty, value);

        public static bool GetHasWarning(TextBox textBox) => textBox.GetValue(HasWarningProperty);

        public static void SetHasWarning(TextBox textBox, bool value) => textBox.SetValue(HasWarningProperty, value);
    }
}
