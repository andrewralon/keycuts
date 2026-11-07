using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace keycuts.GUI.Controls
{
    /// <summary>
    /// A button that owns a context menu and exposes it to UI Automation as an expand/collapse control.
    /// </summary>
    public class MenuButton : Button
    {
        public bool IsMenuOpen
        {
            get { return ContextMenu?.IsOpen == true; }
        }

        public void Expand()
        {
            if (ContextMenu != null)
            {
                ContextMenu.IsOpen = true;
            }
        }

        public void Collapse()
        {
            if (ContextMenu != null)
            {
                ContextMenu.IsOpen = false;
            }
        }

        public void RaiseMenuStateChanged(bool wasOpen)
        {
            if (UIElementAutomationPeer.FromElement(this) is MenuButtonAutomationPeer peer)
            {
                peer.RaiseExpandCollapseChanged(wasOpen);
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new MenuButtonAutomationPeer(this);
        }
    }

    public class MenuButtonAutomationPeer : ButtonAutomationPeer, IExpandCollapseProvider
    {
        private readonly MenuButton owner;

        public MenuButtonAutomationPeer(MenuButton owner)
            : base(owner)
        {
            this.owner = owner;
        }

        public ExpandCollapseState ExpandCollapseState
        {
            get { return owner.IsMenuOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed; }
        }

        public override object? GetPattern(PatternInterface patternInterface)
        {
            return patternInterface == PatternInterface.ExpandCollapse ? this : base.GetPattern(patternInterface);
        }

        public void Expand()
        {
            owner.Expand();
        }

        public void Collapse()
        {
            owner.Collapse();
        }

        public void RaiseExpandCollapseChanged(bool wasOpen)
        {
            var oldState = wasOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
            RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty, oldState, ExpandCollapseState);
        }
    }
}
