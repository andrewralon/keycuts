using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI.Views
{
    /// <summary>
    /// The bar at the bottom of the screens (#205), there only while a newer version is out (#171). It takes its data from the
    /// view model of the view it sits in.
    /// </summary>
    public partial class FooterView : UserControl
    {
        public FooterView()
        {
            InitializeComponent();
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
