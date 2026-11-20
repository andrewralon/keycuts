using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

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
        Rocket,
        Search,
        Settings,
        Spark,
        Table,
        Warning
    }

    /// <summary>
    /// Decorative vector icon on a 24x24 viewbox (WPF's Controls/Icon, ported). Strokes with the inherited Foreground so it follows
    /// theme and state colors. It has no automation peer of its own, so UI Automation skips it. When it is the only content of a
    /// button, put AutomationProperties.Name on the button.
    /// </summary>
    public class Icon : Control
    {
        public const double ViewBoxSize = 24;
        public const double StrokeWidth = 1.7;
        public const double DefaultSize = 18;

        #region Properties

        public static readonly StyledProperty<IconKind> KindProperty =
            AvaloniaProperty.Register<Icon, IconKind>(nameof(Kind), IconKind.File);

        public static readonly StyledProperty<double> SizeProperty =
            AvaloniaProperty.Register<Icon, double>(nameof(Size), DefaultSize,
                validate: size => size >= 0 && !double.IsNaN(size) && !double.IsInfinity(size));

        public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<Icon>();

        #endregion

        static Icon()
        {
            AffectsMeasure<Icon>(SizeProperty);
            AffectsRender<Icon>(KindProperty, SizeProperty, ForegroundProperty);
            IsHitTestVisibleProperty.OverrideDefaultValue<Icon>(false);
        }

        public IconKind Kind
        {
            get => GetValue(KindProperty);
            set => SetValue(KindProperty, value);
        }

        public double Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public IBrush? Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        /// <summary>Resource key of the geometry in Themes/Icons.axaml.</summary>
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

        /// <summary>The geometry this icon draws, from the app's resources; null if there is none.</summary>
        public Geometry? FindGeometry()
        {
            return this.TryFindResource(GetResourceKey(Kind), ActualThemeVariant, out var value) ? value as Geometry : null;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(Size, Size);
        }

        public override void Render(DrawingContext context)
        {
            if (Size <= 0 || FindGeometry() is not { } geometry)
            {
                return;
            }

            var foreground = Foreground;
            var filled = IsFilled(Kind);
            var pen = filled ? null : new Pen(foreground, StrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

            var scale = Size / ViewBoxSize;
            using (context.PushTransform(Matrix.CreateScale(scale, scale)))
            {
                context.DrawGeometry(filled ? foreground : null, pen, geometry);
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new NoneAutomationPeer(this);
        }
    }
}
