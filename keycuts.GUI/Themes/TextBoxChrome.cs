using System.Windows;

namespace keycuts.GUI.Themes
{
    /// <summary>
    /// Attached properties that let views drive the themed TextBox chrome.
    /// </summary>
    public static class TextBoxChrome
    {
        public static readonly DependencyProperty HasErrorProperty =
            DependencyProperty.RegisterAttached("HasError", typeof(bool), typeof(TextBoxChrome), new PropertyMetadata(false));

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.RegisterAttached("Placeholder", typeof(string), typeof(TextBoxChrome), new PropertyMetadata(string.Empty));

        public static string GetPlaceholder(DependencyObject obj) => (string)obj.GetValue(PlaceholderProperty);

        public static void SetPlaceholder(DependencyObject obj, string value) => obj.SetValue(PlaceholderProperty, value);

        public static bool GetHasError(DependencyObject obj) => (bool)obj.GetValue(HasErrorProperty);

        public static void SetHasError(DependencyObject obj, bool value) => obj.SetValue(HasErrorProperty, value);
    }
}
