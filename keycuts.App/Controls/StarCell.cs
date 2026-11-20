using Avalonia;
using Avalonia.Controls;

namespace keycuts.App.Controls
{
    /// <summary>
    /// A cell of a Grid whose star columns share the width by their ratio alone (#252; WPF's Controls/StarCell, ported): what the
    /// child holds never moves the columns. It asks for no width, so a text box whose text runs past its end can't take a
    /// rounded-up pixel from the next column. In the Manager's create row the boxes jumped a pixel whenever the target crossed the
    /// box's end, as when Shift showed a long path in its shorter %VAR% form.
    /// </summary>
    /// <remarks>
    /// WPF's cell also measured the child again at the arranged width, because WPF arranges a child that wants more than it is
    /// given at the width it wants, clipped. Avalonia arranges a stretched child at the width it is given, so that half isn't
    /// needed (a measure from arrange would loop here: Avalonia's parent hook for it is internal).
    /// </remarks>
    public class StarCell : Decorator
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var child = Child;
            if (child == null)
            {
                return new Size();
            }

            child.Measure(availableSize);
            return new Size(0, child.DesiredSize.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Child?.Arrange(new Rect(finalSize));
            return finalSize;
        }
    }
}
