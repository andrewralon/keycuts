using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using keycuts.GUI.Controls;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI.Views
{
    /// <summary>
    /// Creator view (WPF's CreatorView): drop, paste, type and Browse feed the target; Enter in the name field or the Create button
    /// submits (<see cref="CreatorInput"/>). Above the form, the welcome panel. Its live lines announce their own changes.
    /// </summary>
    public partial class CreatorView : UserControl
    {
        public CreatorView()
        {
            InitializeComponent();
            // The view holds it: it subscribes to the view's events
            _ = new CreatorInput(this, Named<TextBox>("TextboxTarget"), Named<TextBox>("TextboxShortcut"),
                Named<TextBox>("TextboxDescription"), Named<MenuButton>("BtnBrowse"), Named<ContextMenu>("BrowseMenu"));
            AttachedToVisualTree += (sender, e) => FocusFirstField();
        }

        private T Named<T>(string name) where T : Control
        {
            return this.FindControl<T>(name)!;
        }

        private void FocusFirstField()
        {
            // Arrowing through the tabs (#218) keeps the focus there
            if (RailTabControl.HoldsKeyboardFocus(TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement()))
            {
                return;
            }

            var nameFirst = (DataContext as CreatorViewModel)?.FocusNameFieldOnShow == true;
            Named<TextBox>(nameFirst ? "TextboxShortcut" : "TextboxTarget").Focus();
        }
    }
}
