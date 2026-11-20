using Avalonia.Controls;

namespace keycuts.GUI.Views
{
    /// <summary>
    /// The Manager's create row: the Creator's fields in one row (docs/styleguide.md). <see cref="CreatorInput"/> does the work, with
    /// Enter in the target going on to create when the name is filled. It never takes the focus when the Manager opens: the
    /// search box does.
    /// </summary>
    public partial class CreateRowView : UserControl
    {
        public CreateRowView()
        {
            InitializeComponent();
            // The view holds it: it subscribes to the view's events
            _ = new CreatorInput(this, TextboxTarget, TextboxShortcut, TextboxDescription, BtnBrowse, BrowseMenu) { TargetEnterCreates = true };
        }
    }
}
