using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace keycuts.GUI.Controls
{
    /// <summary>
    /// The shape of the window's torn bottom-right corner (docs/mockups/torn-corner-v2, B2: "Ripped, smallest"). Points are in
    /// corner space: (0, 0) is the window's bottom-right corner, so every point has x and y at or below 0. The rip runs from
    /// <see cref="Start"/> on the bottom edge to <see cref="End"/> on the right edge, bowing in, with a jitter that makes it ragged.
    /// It is made by the mockup's own generator (its seeded random numbers included), so the app tears the same way the approved
    /// picture does. Pure: nothing here touches a window.
    /// </summary>
    public sealed class TornCornerShape
    {
        /// <summary>How far the rip runs along the bottom edge.</summary>
        public const double TearWidth = 108;

        /// <summary>How far the rip runs up the right edge.</summary>
        public const double TearHeight = 50;

        // B2's settings in the mockup: bow (0 is a straight line), jaggedness, points, threads, long threads, weave band, thread length
        private const double Bow = 0.75;
        private const double Amplitude = 2.8;
        private const int Segments = 26;
        private const int ThreadCount = 18;
        private const int LongThreads = 2;
        private const double ThreadScale = 0.65;
        private const uint Seed = 5;

        /// <summary>The width of the band along the rip where the weave shows, inside the window.</summary>
        public const double WeaveWidth = 11;

        private TornCornerShape(IReadOnlyList<Point> edge, IReadOnlyList<TornThread> threads)
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
                context.BeginFigure(Offset(Edge[0], corner), isFilled: true, isClosed: true);
                for (var i = 1; i < Edge.Count; i++)
                {
                    context.LineTo(Offset(Edge[i], corner), isStroked: true, isSmoothJoin: false);
                }

                context.LineTo(corner, isStroked: false, isSmoothJoin: false);
            }

            geometry.Freeze();
            return geometry;
        }

        /// <summary>A <paramref name="size"/> rectangle with the bottom-right corner torn away: the window's clip.</summary>
        public Geometry GetWindowClip(Size size)
        {
            var corner = new Point(size.Width, size.Height);
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);
                context.LineTo(new Point(size.Width, 0), isStroked: false, isSmoothJoin: false);
                for (var i = Edge.Count - 1; i >= 0; i--)
                {
                    context.LineTo(Offset(Edge[i], corner), isStroked: false, isSmoothJoin: false);
                }

                context.LineTo(new Point(0, size.Height), isStroked: false, isSmoothJoin: false);
            }

            geometry.Freeze();
            return geometry;
        }

        /// <summary>The rip as an open line, offset so that corner space's (0, 0) lands on <paramref name="corner"/>.</summary>
        public Geometry GetEdgeGeometry(Point corner)
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(Offset(Edge[0], corner), isFilled: false, isClosed: false);
                for (var i = 1; i < Edge.Count; i++)
                {
                    context.LineTo(Offset(Edge[i], corner), isStroked: true, isSmoothJoin: true);
                }
            }

            geometry.Freeze();
            return geometry;
        }

        public static Point Offset(Point point, Point corner)
        {
            return new Point(point.X + corner.X, point.Y + corner.Y);
        }

        #region Generator

        // The mockup's generator (tearPoints and the thread loop in torn-corner-v2.html), call for call, so the random numbers match
        private static TornCornerShape Create()
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
        private sealed class Mulberry32
        {
            private uint state;

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
    /// the loose threads. The window's page is clipped to <see cref="TornCornerShape.GetWindowClip"/> separately (MainWindow), so
    /// what's behind the tear is the desktop. While <see cref="IsResizeGrip"/> is set (the large size), a strip along the rip is the
    /// window's bottom-right resize handle: almost invisible (alpha 1, since Windows sends clicks on fully transparent pixels of a
    /// layered window to whatever is behind it), with the diagonal resize cursor, and a press hands the drag to Windows.
    /// </summary>
    public sealed class TornCorner : FrameworkElement
    {
        /// <summary>The room around the rip for the threads and the inner bands.</summary>
        public const double Room = 24;

        /// <summary>How far the resize strip reaches on each side of the rip: about Windows' own resize border.</summary>
        public const double GripReach = 8;

        public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
            nameof(Stroke), typeof(Brush), typeof(TornCorner), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsResizeGripProperty = DependencyProperty.Register(
            nameof(IsResizeGrip), typeof(bool), typeof(TornCorner), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnIsResizeGripChanged));

        private static readonly Brush GripBrush = CreateGripBrush();

        private readonly TornCornerShape shape = TornCornerShape.Default;

        public TornCorner()
        {
            Width = TornCornerShape.TearWidth + Room;
            Height = TornCornerShape.TearHeight + Room;
            HorizontalAlignment = HorizontalAlignment.Right;
            VerticalAlignment = VerticalAlignment.Bottom;
            Focusable = false;
        }

        /// <summary>The rip's edge and threads: the theme's control border color.</summary>
        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        /// <summary>Whether the strip along the rip resizes the window (the Manager and Settings) or is only drawn (the Creator).</summary>
        public bool IsResizeGrip
        {
            get => (bool)GetValue(IsResizeGripProperty);
            set => SetValue(IsResizeGripProperty, value);
        }

        private Point Corner
        {
            get { return new Point(ActualWidth, ActualHeight); }
        }

        protected override void OnRender(DrawingContext dc)
        {
            var corner = Corner;
            var edge = shape.GetEdgeGeometry(corner);
            var inside = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(RenderSize)), shape.GetTearGeometry(corner));

            if (IsResizeGrip)
            {
                dc.DrawGeometry(null, new Pen(GripBrush, GripReach * 2), edge);
            }

            dc.PushClip(inside);
            dc.DrawGeometry(null, new Pen(CreateWeaveBrush(Stroke), TornCornerShape.WeaveWidth) { LineJoin = PenLineJoin.Round }, edge);
            dc.PushOpacity(.7);
            dc.DrawGeometry(null, new Pen(Stroke, 2) { LineJoin = PenLineJoin.Round }, edge);
            dc.Pop();
            dc.Pop();

            foreach (var thread in shape.Threads)
            {
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    context.BeginFigure(TornCornerShape.Offset(thread.From, corner), isFilled: false, isClosed: false);
                    context.QuadraticBezierTo(TornCornerShape.Offset(thread.Bend, corner), TornCornerShape.Offset(thread.To, corner), isStroked: true, isSmoothJoin: false);
                }

                dc.PushOpacity(thread.Opacity);
                dc.DrawGeometry(null, new Pen(Stroke, thread.Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, geometry);
                dc.Pop();
            }
        }

        // Only the resize strip takes the mouse, and only while it resizes; a thread or the weave is never in the way
        protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
        {
            if (!IsResizeGrip)
            {
                return null;
            }

            var strip = shape.GetEdgeGeometry(Corner).GetWidenedPathGeometry(new Pen(Brushes.Black, GripReach * 2));
            return strip.FillContains(hitTestParameters.HitPoint) ? new PointHitTestResult(this, hitTestParameters.HitPoint) : null;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (!IsResizeGrip || Window.GetWindow(this) is not Window window)
            {
                return;
            }

            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            // Windows runs the resize from here on, as from its own corner: the cursor, the outline, the minimum size and snapping
            e.Handled = true;
            ReleaseMouseCapture();
            ReleaseCapture();
            SendMessage(handle, WmSysCommand, new IntPtr(ScSize | WmszBottomRight), IntPtr.Zero);
        }

        private static void OnIsResizeGripChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TornCorner)d).Cursor = (bool)e.NewValue ? Cursors.SizeNWSE : null;
        }

        // A 4 x 4 tile with one thread each way, like the mockup's pattern
        private static Brush CreateWeaveBrush(Brush stroke)
        {
            var pen = new Pen(stroke, .7);
            var threads = new GeometryGroup();
            threads.Children.Add(new LineGeometry(new Point(0, 1), new Point(4, 1)));
            threads.Children.Add(new LineGeometry(new Point(1, 0), new Point(1, 4)));
            var drawing = new GeometryDrawing(null, pen, threads);
            var brush = new DrawingBrush(drawing)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 4, 4),
                ViewportUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, 4, 4),
                ViewboxUnits = BrushMappingMode.Absolute,
                Opacity = .35
            };
            return brush;
        }

        private static Brush CreateGripBrush()
        {
            var brush = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            brush.Freeze();
            return brush;
        }

        #region External Methods

        private const int WmSysCommand = 0x0112;
        private const int ScSize = 0xF000;
        private const int WmszBottomRight = 8;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        #endregion External Methods
    }
}
