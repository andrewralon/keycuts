using System;
using System.Windows;
using System.Windows.Media;

namespace keycuts.GUI.Controls
{
    public enum IconKind
    {
        Arrow,
        Check,
        Chevron,
        Close,
        Copy,
        Delete,
        External,
        File,
        Folder,
        Link,
        More,
        Play,
        Search,
        Settings,
        Spark
    }

    /// <summary>
    /// Decorative vector icon on a 24x24 viewbox. Strokes with the inherited Foreground so it follows theme and state colors.
    /// It has no automation peer, so UI Automation skips it. When it is the only content of a button, put AutomationProperties.Name on the button.
    /// </summary>
    public class Icon : FrameworkElement
    {
        public const double ViewBoxSize = 24;
        public const double StrokeWidth = 1.7;
        public const double DefaultSize = 18;

        #region Dependency properties

        public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
            nameof(Kind), typeof(IconKind), typeof(Icon),
            new FrameworkPropertyMetadata(IconKind.File, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(double), typeof(Icon),
            new FrameworkPropertyMetadata(DefaultSize, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender),
            value => value is double size && size >= 0 && !double.IsNaN(size) && !double.IsInfinity(size));

        public static readonly DependencyProperty ForegroundProperty =
            System.Windows.Documents.TextElement.ForegroundProperty.AddOwner(
                typeof(Icon), new FrameworkPropertyMetadata(SystemColors.ControlTextBrush, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

        #endregion

        static Icon()
        {
            IsHitTestVisibleProperty.OverrideMetadata(typeof(Icon), new UIPropertyMetadata(false));
        }

        public IconKind Kind
        {
            get => (IconKind)GetValue(KindProperty);
            set => SetValue(KindProperty, value);
        }

        public double Size
        {
            get => (double)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public Brush? Foreground
        {
            get => (Brush?)GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        /// <summary>Resource key of the geometry in Themes/Icons.xaml.</summary>
        public static string GetResourceKey(IconKind kind)
        {
            if (!Enum.IsDefined(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            return "Icon." + kind;
        }

        /// <summary>The "more" dots are solid; every other icon is an outline.</summary>
        public static bool IsFilled(IconKind kind)
        {
            return kind == IconKind.More;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(Size, Size);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (Size <= 0 || TryFindResource(GetResourceKey(Kind)) is not Geometry geometry)
            {
                return;
            }

            var foreground = Foreground;
            var filled = IsFilled(Kind);
            var pen = filled
                ? null
                : new Pen(foreground, StrokeWidth)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };

            var scale = Size / ViewBoxSize;
            drawingContext.PushTransform(new ScaleTransform(scale, scale));
            drawingContext.DrawGeometry(filled ? foreground : null, pen, geometry);
            drawingContext.Pop();
        }
    }
}
