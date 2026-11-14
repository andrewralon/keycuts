using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace keycuts.GUI.Controls
{
    /// <summary>
    /// The outline of a folder tab (#218): a square left side with a small cut corner at the top, a flat top, and a slanted right
    /// side, standing on its bottom edge. The path is open at the bottom, so a stroke draws the left, top and slanted sides only
    /// and the tab runs straight into the page below it. The stroke is drawn inside the bounds, so neighbours 4 apart don't overlap.
    /// </summary>
    public class FolderTabShape : Shape
    {
        public static readonly DependencyProperty CutProperty = DependencyProperty.Register(
            nameof(Cut), typeof(double), typeof(FolderTabShape),
            new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure),
            IsLength);

        public static readonly DependencyProperty SlantProperty = DependencyProperty.Register(
            nameof(Slant), typeof(double), typeof(FolderTabShape),
            new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure),
            IsLength);

        /// <summary>The size of the cut corner at the top left.</summary>
        public double Cut
        {
            get => (double)GetValue(CutProperty);
            set => SetValue(CutProperty, value);
        }

        /// <summary>How far the right side leans: the base is this much wider than the top.</summary>
        public double Slant
        {
            get => (double)GetValue(SlantProperty);
            set => SetValue(SlantProperty, value);
        }

        protected override Geometry DefiningGeometry
        {
            get { return Build(RenderSize.Width, RenderSize.Height, Cut, Slant, StrokeThickness); }
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

        private static Geometry Build(double width, double height, double cut, double slant, double strokeThickness)
        {
            if (width <= 0 || height <= 0)
            {
                return Geometry.Empty;
            }

            var points = GetPoints(width, height, cut, slant, strokeThickness);
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(points[0], isFilled: true, isClosed: false);
                context.PolyLineTo(new[] { points[1], points[2], points[3], points[4] }, isStroked: true, isSmoothJoin: false);
            }

            geometry.Freeze();
            return geometry;
        }

        private static bool IsLength(object value)
        {
            return value is double length && length >= 0 && !double.IsNaN(length) && !double.IsInfinity(length);
        }
    }
}
