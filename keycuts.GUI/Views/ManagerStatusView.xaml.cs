using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI.Views
{
    /// <summary>The manager's status message and Undo, for the footer's message slot (#205).</summary>
    public partial class ManagerStatusView : UserControl
    {
        public ManagerStatusView()
        {
            InitializeComponent();
            DataContextChanged += ManagerStatusView_DataContextChanged;
        }

        // A message (Deleted with its Undo, a save or restore error) appears while the focus is elsewhere, so it has to be
        // announced: LiveSetting alone doesn't raise the event, the peer has to.
        private void ManagerStatusView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
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

        // Only a message that appears: when it times out the text is empty and there is nothing to say
        private void Context_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ManagerViewModel.StatusMessage)
                && sender is ManagerViewModel { StatusMessage.Length: > 0 })
            {
                LiveRegion.Announce(StatusMessageText);
            }
        }
    }
}
