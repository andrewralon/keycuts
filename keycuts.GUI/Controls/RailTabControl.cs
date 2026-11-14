using System.Windows.Controls;
using System.Windows.Input;

namespace keycuts.GUI.Controls
{
    /// <summary>
    /// The tab bar's list of tabs (#218). A <see cref="TabControl"/> gives screen readers a real tab list with selected state, and Tab
    /// and the arrow keys reach it; its template holds only the tabs, since the shell picks the screen below. A TabControl also cycles
    /// its tabs on Ctrl+Tab and Ctrl+Page Up/Down, and #218 declined shortcuts for switching tabs (maybe later), so those keys are left out.
    /// </summary>
    public class RailTabControl : TabControl
    {
        private bool lastInputWasMouse;

        /// <summary>
        /// Whether a keyboard user is moving through the tabs: the focus is on one of them and the last input there was a key, not a
        /// click. A screen's own first-focus code (the Creator's fields, the Manager's search box) stands aside then, or an arrow that
        /// switches screens would leave the tabs and the next arrow would act on the page. A click on a tab still lands in the page.
        /// </summary>
        public static bool HoldsKeyboardFocus()
        {
            return Keyboard.FocusedElement is TabItem tab
                && ItemsControl.ItemsControlFromItemContainer(tab) is RailTabControl rail
                && !rail.lastInputWasMouse;
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            lastInputWasMouse = true;
            base.OnPreviewMouseDown(e);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            lastInputWasMouse = false;
            base.OnPreviewKeyDown(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (IsTabCycleKey(e.Key, e.KeyboardDevice.Modifiers))
            {
                return;
            }

            base.OnKeyDown(e);
        }

        /// <summary>Ctrl+Tab, Ctrl+Shift+Tab, Ctrl+Page Up and Ctrl+Page Down: what a TabControl cycles its tabs with.</summary>
        public static bool IsTabCycleKey(Key key, ModifierKeys modifiers)
        {
            return (modifiers & ModifierKeys.Control) != 0 && key is Key.Tab or Key.PageUp or Key.PageDown;
        }
    }
}
