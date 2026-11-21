using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering;

namespace keycuts.GUI.Controls
{
    /// <summary>
    /// The shape of the window's torn bottom-right corner (docs/mockups/torn-corner-v2, B2: "Ripped, smallest"; WPF's
    /// Controls/TornCorner, ported). Points are in corner space: (0, 0) is the window's bottom-right corner, so every point has x and
    /// y at or below 0. The rip runs from <see cref="Start"/> on the bottom edge to <see cref="End"/> on the right edge, bowing in,
    /// with a jitter that makes it ragged. It is made by the mockup's own generator (its seeded random numbers included), so the app
    /// tears the same way the approved picture does. Pure: nothing here touches a window.
    /// </summary>
    public sealed class TornCornerShape
    {
        /// <summary>How far the rip runs along the bottom edge.</summary>
        public const double TearWidth = 108;

        /// <summary>How far the rip runs up the right edge.</summary>
        public const double TearHeight = 50;

        // B2's settings in the mockup: bow (0 is a straight line), jaggedness, points, threads, long threads, weave band, thread length
        const double Bow = 0.75;
        const double Amplitude = 2.8;
        const int Segments = 26;
        const int ThreadCount = 18;
        const int LongThreads = 2;
        const double ThreadScale = 0.65;
        const uint Seed = 5;

        /// <summary>The width of the band along the rip where the weave shows, inside the window.</summary>
        public const double WeaveWidth = 11;

        TornCornerShape(IReadOnlyList<Point> edge, IReadOnlyList<TornThread> threads)
        {
            Edge = edge;
            Threads = threads;
        }

        public static TornCornerShape Default { get; } = Create();

        /// <summary>The rip, from the bottom edge to the right edge, in corner space.</summary>
        public IReadOnlyList<Point> Edge { get; }

        /// <summary>The loose threads past the rip, in corner space.</summary>
        public IReadOnlyList<TornThread> Threads { get; }

        public static Point Start
        {
            get { return new Point(-TearWidth, 0); }
        }

        public static Point End
        {
            get { return new Point(0, -TearHeight); }
        }

        /// <summary>The torn-away part as a closed shape in corner space: the rip, then the corner.</summary>
        public Geometry GetTearGeometry(Point corner)
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(Offset(Edge[0], corner), isFilled: true);
                for (var i = 1; i < Edge.Count; i++)
                {
                    context.LineTo(Offset(Edge[i], corner));
                }

                context.LineTo(corner, isStroked: false);
                context.EndFigure(isClosed: true);
            }

            return geometry;
        }

        /// <summary>A <paramref name="size"/> rectangle with the bottom-right corner torn away: the window's clip.</summary>
        public Geometry GetWindowClip(Size size)
        {
            var corner = new Point(size.Width, size.Height);
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(0, 0), isFilled: true);
                context.LineTo(new Point(size.Width, 0), isStroked: false);
                for (var i = Edge.Count - 1; i >= 0; i--)
                {
                    context.LineTo(Offset(Edge[i], corner), isStroked: false);
                }

                context.LineTo(new Point(0, size.Height), isStroked: false);
                context.EndFigure(isClosed: true);
            }

            return geometry;
        }

        /// <summary>The rip as an open line, offset so that corner space's (0, 0) lands on <paramref name="corner"/>.</summary>
        public Geometry GetEdgeGeometry(Point corner)
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(Offset(Edge[0], corner), isFilled: false);
                for (var i = 1; i < Edge.Count; i++)
                {
                    context.LineTo(Offset(Edge[i], corner));
                }

                context.EndFigure(isClosed: false);
            }

            return geometry;
        }

        public static Point Offset(Point point, Point corner)
        {
            return new Point(point.X + corner.X, point.Y + corner.Y);
        }

        #region Generator

        // The mockup's generator (tearPoints and the thread loop in torn-corner-v2.html), call for call, so the random numbers match
        static TornCornerShape Create()
        {
            var random = new Mulberry32(Seed);
            var start = Start;
            var end = End;
            var inner = new Point(-TearWidth, -TearHeight);
            var mid = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            var control = new Point(mid.X + (inner.X - mid.X) * Bow, mid.Y + (inner.Y - mid.Y) * Bow);
            var length = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
            // The outward normal: towards the corner
            var nx = (end.Y - start.Y) / length;
            var ny = -(end.X - start.X) / length;

            var edge = new List<Point>();
            for (var i = 0; i <= Segments; i++)
            {
                var t = (double)i / Segments;
                var u = 1 - t;
                var x = u * u * start.X + 2 * u * t * control.X + t * t * end.X;
                var y = u * u * start.Y + 2 * u * t * control.Y + t * t * end.Y;
                if (i > 0 && i < Segments)
                {
                    var jitter = (random.Next() - .5) * 2 * Amplitude;
                    if (random.Next() < .18)
                    {
                        // An occasional deeper notch
                        jitter -= Amplitude * 1.6 * random.Next();
                    }

                    x += nx * jitter;
                    y += ny * jitter;
                }

                edge.Add(new Point(x, y));
            }

            var threads = new List<TornThread>();
            for (var k = 0; k < ThreadCount; k++)
            {
                var from = edge[1 + (int)Math.Floor(random.Next() * (edge.Count - 2))];
                var isLong = k < LongThreads;
                var threadLength = (isLong ? 18 + random.Next() * 16 : 4 + random.Next() * 8) * ThreadScale;
                var angle = (random.Next() - .5) * 1.1;
                var dx = nx * Math.Cos(angle) - ny * Math.Sin(angle);
                var dy = nx * Math.Sin(angle) + ny * Math.Cos(angle);
                // Long threads droop a little
                var sag = isLong ? 6 : 1.5;
                var to = new Point(from.X + dx * threadLength, from.Y + dy * threadLength + sag * .6);
                var bend = new Point(from.X + dx * threadLength * .5 + (random.Next() - .5) * 3, from.Y + dy * threadLength * .5 + sag);
                var thickness = isLong ? 1.2 : .9 + random.Next() * .6;
                var opacity = .6 + random.Next() * .4;
                threads.Add(new TornThread(from, bend, to, thickness, opacity));
            }

            return new TornCornerShape(edge, threads);
        }

        /// <summary>The mockup's random numbers (mulberry32), so the same seed gives the same rip.</summary>
        sealed class Mulberry32
        {
            uint state;

            public Mulberry32(uint seed)
            {
                state = seed;
            }

            public double Next()
            {
                unchecked
                {
                    state += 0x6D2B79F5;
                    var t = (state ^ (state >> 15)) * (state | 1);
                    t = (t + (t ^ (t >> 7)) * (t | 61)) ^ t;
                    return (t ^ (t >> 14)) / 4294967296.0;
                }
            }
        }

        #endregion Generator
    }

    /// <summary>A loose thread: a curve from the rip outwards, in corner space.</summary>
    public readonly record struct TornThread(Point From, Point Bend, Point To, double Thickness, double Opacity);

    /// <summary>
    /// Draws the torn corner over the window's bottom-right: a band where the weave shows along the rip, the rip's frayed edge and
    /// the loose threads. The window's page is clipped to <see cref="TornCornerShape.GetWindowClip"/> separately (the main window,
    /// Phase 3.5), so what's behind the tear is the desktop. While <see cref="IsResizeGrip"/> is set (the large size), a strip along
    /// the rip is the window's bottom-right resize handle: almost invisible (alpha 1, since Windows sends clicks on fully transparent
    /// pixels of a layered window to whatever is behind it), with the diagonal resize cursor, and a press hands the drag to the
    /// platform (<see cref="Window.BeginResizeDrag"/>, where WPF sent Windows SC_SIZE).
    /// </summary>
    public sealed class TornCorner : Control, ICustomHitTest
    {
        /// <summary>The room around the rip for the threads and the inner bands.</summary>
        public const double Room = 24;

        /// <summary>How far the resize strip reaches on each side of the rip: about Windows' own resize border.</summary>
        public const double GripReach = 8;

        public static readonly StyledProperty<IBrush?> StrokeProperty =
            AvaloniaProperty.Register<TornCorner, IBrush?>(nameof(Stroke), Brushes.Gray);

        public static readonly StyledProperty<bool> IsResizeGripProperty =
            AvaloniaProperty.Register<TornCorner, bool>(nameof(IsResizeGrip));

        static readonly IBrush GripBrush = new ImmutableSolidColorBrush(Color.FromArgb(1, 0, 0, 0));

        static readonly Cursor ResizeCursor = new Cursor(StandardCursorType.BottomRightCorner);

        readonly TornCornerShape shape = TornCornerShape.Default;

        static TornCorner()
        {
            AffectsRender<TornCorner>(StrokeProperty, IsResizeGripProperty);
        }

        public TornCorner()
        {
            Width = TornCornerShape.TearWidth + Room;
            Height = TornCornerShape.TearHeight + Room;
            HorizontalAlignment = HorizontalAlignment.Right;
            VerticalAlignment = VerticalAlignment.Bottom;
            Focusable = false;
        }

        /// <summary>The rip's edge and threads: the theme's control border color.</summary>
        public IBrush? Stroke
        {
            get => GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        /// <summary>Whether the strip along the rip resizes the window (the Manager and Settings) or is only drawn (the Creator).</summary>
        public bool IsResizeGrip
        {
            get => GetValue(IsResizeGripProperty);
            set => SetValue(IsResizeGripProperty, value);
        }

        Point Corner
        {
            get { return new Point(Bounds.Width, Bounds.Height); }
        }

        public override void Render(DrawingContext context)
        {
            var corner = Corner;
            var edge = shape.GetEdgeGeometry(corner);
            var inside = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(Bounds.Size)), shape.GetTearGeometry(corner));

            if (IsResizeGrip)
            {
                context.DrawGeometry(null, new Pen(GripBrush, GripReach * 2), edge);
            }

            using (context.PushGeometryClip(inside))
            {
                context.DrawGeometry(null, new Pen(CreateWeaveBrush(Stroke), TornCornerShape.WeaveWidth, lineJoin: PenLineJoin.Round), edge);
                using (context.PushOpacity(.7))
                {
                    context.DrawGeometry(null, new Pen(Stroke, 2, lineJoin: PenLineJoin.Round), edge);
                }
            }

            foreach (var thread in shape.Threads)
            {
                var geometry = new StreamGeometry();
                using (var stream = geometry.Open())
                {
                    stream.BeginFigure(TornCornerShape.Offset(thread.From, corner), isFilled: false);
                    stream.QuadraticBezierTo(TornCornerShape.Offset(thread.Bend, corner), TornCornerShape.Offset(thread.To, corner));
                    stream.EndFigure(isClosed: false);
                }

                using (context.PushOpacity(thread.Opacity))
                {
                    context.DrawGeometry(null, new Pen(Stroke, thread.Thickness, lineCap: PenLineCap.Round), geometry);
                }
            }
        }

        /// <summary>Only the resize strip takes the pointer, and only while it resizes; a thread or the weave is never in the way.</summary>
        public bool HitTest(Point point)
        {
            return IsResizeGrip && shape.GetEdgeGeometry(Corner).StrokeContains(new Pen(Brushes.Black, GripReach * 2), point);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IsResizeGripProperty)
            {
                Cursor = change.GetNewValue<bool>() ? ResizeCursor : null;
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!IsResizeGrip || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || TopLevel.GetTopLevel(this) is not Window window)
            {
                return;
            }

            // The platform runs the resize from here on, as from its own corner: the cursor, the outline, the minimum size and snapping
            e.Handled = true;
            window.BeginResizeDrag(WindowEdge.SouthEast, e);
        }

        // A 4 x 4 tile with one thread each way, like the mockup's pattern
        static IBrush CreateWeaveBrush(IBrush? stroke)
        {
            var threads = new GeometryGroup();
            threads.Children.Add(new LineGeometry(new Point(0, 1), new Point(4, 1)));
            threads.Children.Add(new LineGeometry(new Point(1, 0), new Point(1, 4)));
            return new DrawingBrush(new GeometryDrawing { Pen = new Pen(stroke, .7), Geometry = threads })
            {
                TileMode = TileMode.Tile,
                DestinationRect = new RelativeRect(0, 0, 4, 4, RelativeUnit.Absolute),
                SourceRect = new RelativeRect(0, 0, 4, 4, RelativeUnit.Absolute),
                Opacity = .35
            };
        }
    }
}
