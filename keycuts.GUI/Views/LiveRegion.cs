using System;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Threading;

namespace keycuts.GUI.Views
{
    /// <summary>Tells screen readers that the text of an element with a live setting has changed.</summary>
    public static class LiveRegion
    {
        /// <summary>
        /// Raised, with the element, each time <see cref="Announce"/> is called. Tests observe it, since the automation event
        /// itself is raised a moment later, off the layout pass, and there is no screen reader to hear it.
        /// </summary>
        public static event Action<UIElement>? Announced;

        /// <summary>
        /// The LiveSetting on a text block alone doesn't raise the event; the peer has to. CreatePeerForElement, not
        /// FromElement: a line that was collapsed until now may have no peer yet, and FromElement returns only an existing
        /// one. It returns the existing peer when there is one.
        /// </summary>
        public static void Announce(UIElement element)
        {
            Announced?.Invoke(element);

            element.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (UIElementAutomationPeer.CreatePeerForElement(element) is AutomationPeer peer)
                {
                    peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
                }
            }));
        }
    }
}
