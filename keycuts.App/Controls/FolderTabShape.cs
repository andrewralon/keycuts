using System;
using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace keycuts.App.Controls
{
    /// <summary>
    /// The outline of a folder tab (#218; WPF's Controls/FolderTabShape, ported): a square left side with a small cut corner at the
    /// top, a flat top, and a slanted right side, standing on its bottom edge. The path is open at the bottom, so a stroke draws the
    /// left, top and slanted sides only and the tab runs straight into the page below it. The stroke is drawn inside the bounds, so
    /// neighbours 4 apart don't overlap. It takes the size it is given, like a Rectangle.
    /// </summary>
    public class FolderTabShape : Shape
    {
        public static readonly StyledProperty<double> CutProperty =
            AvaloniaProperty.Register<FolderTabShape, double>(nameof(Cut), 8, validate: IsLength);

        public static readonly StyledProperty<double> SlantProperty =
            AvaloniaProperty.Register<FolderTabShape, double>(nameof(Slant), 16, validate: IsLength);

        static FolderTabShape()
        {
            AffectsGeometry<FolderTabShape>(BoundsProperty, CutProperty, SlantProperty, StrokeThicknessProperty);
        }

        /// <summary>The size of the cut corner at the top left.</summary>
        public double Cut
        {
            get => GetValue(CutProperty);
            set => SetValue(CutProperty, value);
        }

        /// <summary>How far the right side leans: the base is this much wider than the top.</summary>
        public double Slant
        {
            get => GetValue(SlantProperty);
            set => SetValue(SlantProperty, value);
        }

        /// <summary>The points of the outline from the bottom left, round to the bottom right. Inset by half the stroke so it stays inside.</summary>
        public static Point[] GetPoints(double width, double height, double cut, double slant, double strokeThickness)
        {
            var inset = Math.Max(0, strokeThickness) / 2;
            var left = inset;
            var right = Math.Max(left, width - inset);
            var top = inset;
            var bottom = Math.Max(top, height);
            var topRight = Math.Max(left + cut, right - slant);

            return new[]
            {
                new Point(left, bottom),
                new Point(left, top + cut),
                new Point(left + cut, top),
                new Point(topRight, top),
                new Point(right, bottom)
            };
        }

        protected override Geometry? CreateDefiningGeometry()
        {
            var size = Bounds.Size;
            if (size.Width <= 0 || size.Height <= 0)
            {
                return null;
            }

            var points = GetPoints(size.Width, size.Height, Cut, Slant, StrokeThickness);
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(points[0], isFilled: true);
                for (var i = 1; i < points.Length; i++)
                {
                    context.LineTo(points[i]);
                }

                context.EndFigure(isClosed: false);
            }

            return geometry;
        }

        // Like a Rectangle: the shape fills what it is given, and its geometry is already in its own coordinates
        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(StrokeThickness, StrokeThickness);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            return finalSize;
        }

        static bool IsLength(double length)
        {
            return length >= 0 && !double.IsNaN(length) && !double.IsInfinity(length);
        }
    }
}
