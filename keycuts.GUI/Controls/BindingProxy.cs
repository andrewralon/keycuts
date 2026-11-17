using System.Windows;

namespace keycuts.GUI.Controls
{
    /// <summary>
    /// Carries a view's DataContext to elements that can't find it themselves. Put in a view's resources as
    /// <c>&lt;controls:BindingProxy x:Key="..." Data="{Binding}" /&gt;</c>: a Freezable resource inherits the DataContext of the element
    /// that holds it, and a binding with <c>Source={StaticResource ...}</c> doesn't depend on where its target sits. The Manager's column
    /// headers need it: a column that starts hidden (Runs, Last run, Modified, while the table is narrow) builds its header off the
    /// visual tree, and a <c>RelativeSource FindAncestor</c> binding on that header failed then and never looked again once the column
    /// showed, so the header lost its sort state and its click.
    /// </summary>
    public class BindingProxy : Freezable
    {
        public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
            nameof(Data), typeof(object), typeof(BindingProxy), new PropertyMetadata(null));

        public object? Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        protected override Freezable CreateInstanceCore()
        {
            return new BindingProxy();
        }
    }
}
