using System.Windows;

namespace keycuts.GUI.Controls
{
    /// <summary>
    /// Hover and pressed state for a caption button that Windows, not WPF, receives the mouse for. Over the maximize button the window
    /// answers <c>WM_NCHITTEST</c> with <c>HTMAXBUTTON</c> so Windows 11 shows its snap layouts, which makes the mouse messages
    /// non-client ones: WPF's <c>IsMouseOver</c> and <c>IsPressed</c> never change there, so <see cref="WindowCaptionHook"/> sets these two.
    /// </summary>
    public static class CaptionButtonState
    {
        public static readonly DependencyProperty IsHotProperty = DependencyProperty.RegisterAttached(
            "IsHot", typeof(bool), typeof(CaptionButtonState), new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty IsPressedProperty = DependencyProperty.RegisterAttached(
            "IsPressed", typeof(bool), typeof(CaptionButtonState), new FrameworkPropertyMetadata(false));

        public static bool GetIsHot(DependencyObject element) => (bool)element.GetValue(IsHotProperty);

        public static void SetIsHot(DependencyObject element, bool value) => element.SetValue(IsHotProperty, value);

        public static bool GetIsPressed(DependencyObject element) => (bool)element.GetValue(IsPressedProperty);

        public static void SetIsPressed(DependencyObject element, bool value) => element.SetValue(IsPressedProperty, value);
    }
}
