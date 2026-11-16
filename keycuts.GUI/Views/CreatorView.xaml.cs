using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI.Views
{
    /// <summary>
    /// Creator view: drop, paste, type and Browse feed the target; Enter in the name field or the Create button submits
    /// (<see cref="CreatorInput"/>). Above the form, the welcome panel.
    /// </summary>
    public partial class CreatorView : UserControl
    {
        public CreatorView()
        {
            InitializeComponent();
            // The view holds it: it subscribes to the view's events
            _ = new CreatorInput(this, TextboxTarget, TextboxShortcut, TextboxDescription, BtnBrowse, BrowseMenu);
            DataContextChanged += CreatorView_DataContextChanged;
        }

        private CreatorViewModel? ViewModel
        {
            get { return DataContext as CreatorViewModel; }
        }

        private void CreatorView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is CreatorViewModel { Welcome: { } oldWelcome })
            {
                oldWelcome.PropertyChanged -= Welcome_PropertyChanged;
            }

            if (e.NewValue is CreatorViewModel { Welcome: { } newWelcome })
            {
                newWelcome.PropertyChanged += Welcome_PropertyChanged;
            }
        }

        // The helper line (name warnings and errors) and the status by the Name label (Saving, Created) are HelperText
        // controls, which announce their own changes

        // The welcome panel's helper line (name errors, Saving, how to use the new keycut, the PATH warning)
        private void Welcome_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WelcomeViewModel.HelperText))
            {
                LiveRegion.Announce(WelcomeHelperTextBlock);
            }
        }

        private void CreatorView_Loaded(object sender, RoutedEventArgs e)
        {
            // Arrowing through the tabs (#218) keeps the focus there
            if (Controls.RailTabControl.HoldsKeyboardFocus())
            {
                return;
            }

            var nameFirst = ViewModel?.FocusNameFieldOnShow == true;
            var target = nameFirst ? TextboxShortcut : TextboxTarget;
            target.Focus();
            Keyboard.Focus(target);
        }
    }
}
