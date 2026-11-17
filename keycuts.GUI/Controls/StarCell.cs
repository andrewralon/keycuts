using System.Windows;
using System.Windows.Controls;

namespace keycuts.GUI.Controls
{
    /// <summary>
    /// A cell of a Grid whose star columns share the width by their ratio alone (#252): what the child holds never moves the columns.
    /// <list type="bullet">
    /// <item>It asks for no width. A Grid takes each cell's desired width as a minimum when it shares out its star columns, so a
    /// text box whose text runs past its end, which asks for all of its share, could take a rounded-up pixel from the next column.</item>
    /// <item>It fits the child to the width it is given. The Grid can measure a column a pixel wider than it then arranges it (it
    /// rounds the two passes apart), and a child that asks for more than it gets is arranged at what it asked for, clipped: the
    /// full box came out a pixel wider than the same box with short text. So the child is measured again at the arranged width.</item>
    /// </list>
    /// In the Manager's create row the boxes jumped a pixel whenever the target crossed the box's end, as when Shift showed a long
    /// path in its shorter %VAR% form.
    /// </summary>
    public class StarCell : Decorator
    {
        private bool fittingChild;

        protected override Size MeasureOverride(Size constraint)
        {
            var child = Child;
            if (child == null)
            {
                return new Size();
            }

            child.Measure(constraint);
            return new Size(0, child.DesiredSize.Height);
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            var child = Child;
            if (child == null)
            {
                return arrangeSize;
            }

            if (child.DesiredSize.Width > arrangeSize.Width)
            {
                fittingChild = true;
                try
                {
                    child.Measure(arrangeSize);
                }
                finally
                {
                    fittingChild = false;
                }
            }

            child.Arrange(new Rect(arrangeSize));
            return arrangeSize;
        }

        // Fitting the child to the arranged width changes only how wide it wants to be, which this cell never passes on: measuring
        // it again would undo the fit, and the next arrange would redo it, forever
        protected override void OnChildDesiredSizeChanged(UIElement child)
        {
            if (!fittingChild)
            {
                base.OnChildDesiredSizeChanged(child);
            }
        }
    }
}
