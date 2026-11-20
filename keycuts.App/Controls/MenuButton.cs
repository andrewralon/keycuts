using System;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace keycuts.App.Controls
{
    /// <summary>
    /// A button that owns a context menu and exposes it to UI Automation as an expand/collapse control (WPF's Controls/MenuButton,
    /// ported).
    /// </summary>
    public class MenuButton : Button
    {
        protected override Type StyleKeyOverride => typeof(Button);

        public bool IsMenuOpen
        {
            get { return ContextMenu?.IsOpen == true; }
        }

        public void Expand()
        {
            ContextMenu?.Open(this);
        }

        public void Collapse()
        {
            ContextMenu?.Close();
        }

        public void RaiseMenuStateChanged(bool wasOpen)
        {
            if (ControlAutomationPeer.FromElement(this) is MenuButtonAutomationPeer peer)
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
        readonly MenuButton owner;

        public MenuButtonAutomationPeer(MenuButton owner)
            : base(owner)
        {
            this.owner = owner;
        }

        public ExpandCollapseState ExpandCollapseState
        {
            get { return owner.IsMenuOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed; }
        }

        public bool ShowsMenu => true;

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
