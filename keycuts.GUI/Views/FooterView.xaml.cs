using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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
            DataContextChanged += FooterView_DataContextChanged;
        }

        /// <summary>What the screen shows between the count and the update line (the manager's status message and Undo).</summary>
        public object? Message
        {
            get { return GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        // The count changes after a create or a delete, often while the focus is elsewhere. LiveSetting alone doesn't announce
        // that: the peer has to raise the event.
        private void FooterView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldContext)
            {
                oldContext.PropertyChanged -= Context_PropertyChanged;
            }

            if (e.NewValue is INotifyPropertyChanged newContext)
            {
                newContext.PropertyChanged += Context_PropertyChanged;
            }
        }

        private void Context_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CountDescription")
            {
                LiveRegion.Announce(CountText);
            }
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
                LiveRegion.Announce(UpdateNoticeText);
            }
        }
    }
}
