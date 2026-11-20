using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI.Views
{
    /// <summary>The manager's status message and Undo (#205). It takes no room while it has nothing to say.</summary>
    public partial class ManagerStatusView : UserControl
    {
        private ManagerViewModel? viewModel;

        public ManagerStatusView()
        {
            InitializeComponent();
            DataContextChanged += (sender, e) =>
            {
                if (viewModel != null)
                {
                    viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                }

                viewModel = DataContext as ManagerViewModel;
                if (viewModel != null)
                {
                    viewModel.PropertyChanged += ViewModel_PropertyChanged;
                }

                UpdateRoom();
            };
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ManagerViewModel.StatusMessage) or nameof(ManagerViewModel.CanUndoDelete))
            {
                UpdateRoom();
            }
        }

        private void UpdateRoom()
        {
            var showing = viewModel != null && (viewModel.StatusMessage.Length > 0 || viewModel.CanUndoDelete);
            Root.Margin = showing ? new Thickness(12, 0, 0, 0) : default;
        }
    }
}
