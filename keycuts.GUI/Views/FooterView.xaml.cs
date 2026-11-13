using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI.Views
{
    /// <summary>
    /// The footer of the creator and the manager (#205): Settings, the keycut count, an optional message of the screen's own, and
    /// the update line. It takes its data from the view model of the view it sits in.
    /// </summary>
    public partial class FooterView : UserControl
    {
        public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
            nameof(Message), typeof(object), typeof(FooterView), new PropertyMetadata(null));

        public FooterView()
        {
            InitializeComponent();
        }

        /// <summary>What the screen shows between the count and the update line (the manager's status message and Undo).</summary>
        public object? Message
        {
            get { return GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        // The update line (#171) appears after a background check, with focus elsewhere, so it has to be announced
        private void UpdateNotice_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is UpdateNoticeViewModel oldNotice)
            {
                oldNotice.PropertyChanged -= UpdateNoticeViewModel_PropertyChanged;
            }

            if (e.NewValue is UpdateNoticeViewModel newNotice)
            {
                newNotice.PropertyChanged += UpdateNoticeViewModel_PropertyChanged;
            }
        }

        private void UpdateNoticeViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UpdateNoticeViewModel.Message) && sender is UpdateNoticeViewModel { IsVisible: true })
            {
                AnnounceLiveRegion(UpdateNoticeText);
            }
        }

        // The LiveSetting on the text block alone doesn't raise the event; the peer has to. CreatePeerForElement, not
        // FromElement: a line that was collapsed until now (the update line) may have no peer yet, and FromElement
        // returns only an existing one. It returns the existing peer when there is one.
        private void AnnounceLiveRegion(UIElement element)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (UIElementAutomationPeer.CreatePeerForElement(element) is AutomationPeer peer)
                {
                    peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
                }
            }));
        }
    }
}
