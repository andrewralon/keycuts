using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace keycuts.GUI.Controls
{
    /// <summary>
    /// The tab bar's list of tabs (#218; WPF's Controls/RailTabControl, ported). A <see cref="TabControl"/> gives screen readers a
    /// real tab list with selected state, and Tab and the arrow keys reach it; its theme holds only the tabs, since the shell picks
    /// the screen below. #218 declined shortcuts for cycling the tabs (maybe later), so Ctrl+Tab and Ctrl+Page Up/Down are left out.
    /// </summary>
    public class RailTabControl : TabControl
    {
        bool lastInputWasMouse;

        public RailTabControl()
        {
            // Tunneling, so a tab's own handling can't hide which kind of input came last
            AddHandler(PointerPressedEvent, (_, _) => lastInputWasMouse = true, RoutingStrategies.Tunnel, handledEventsToo: true);
            AddHandler(KeyDownEvent, (_, _) => lastInputWasMouse = false, RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        /// <summary>
        /// Whether a keyboard user is moving through the tabs: <paramref name="focused"/> (the focused element) is one of them and the
        /// last input there was a key, not a click. A screen's own first-focus code (the Creator's fields, the Manager's search box)
        /// stands aside then, or an arrow that switches screens would leave the tabs and the next arrow would act on the page. A
        /// click on a tab still lands in the page. Avalonia has no global focused element, so the caller passes its window's.
        /// </summary>
        public static bool HoldsKeyboardFocus(IInputElement? focused)
        {
            return focused is TabItem tab
                && ItemsControlFromItemContainer(tab) is RailTabControl rail
                && !rail.lastInputWasMouse;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (IsTabCycleKey(e.Key, e.KeyModifiers))
            {
                return;
            }

            base.OnKeyDown(e);
        }

        /// <summary>Ctrl+Tab, Ctrl+Shift+Tab, Ctrl+Page Up and Ctrl+Page Down: what a TabControl cycles its tabs with.</summary>
        public static bool IsTabCycleKey(Key key, KeyModifiers modifiers)
        {
            return (modifiers & KeyModifiers.Control) != 0 && key is Key.Tab or Key.PageUp or Key.PageDown;
        }
    }
}
