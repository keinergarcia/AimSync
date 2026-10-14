using System.Windows;
using System.Windows.Media;

namespace AimSync.App.Controls;

public sealed class StickVisual : FrameworkElement
{
    private static readonly Brush DefaultBackground = new SolidColorBrush(Color.FromRgb(0x18, 0x1F, 0x2B));
    private static readonly Brush DefaultRing = new SolidColorBrush(Color.FromRgb(0x2C, 0x36, 0x48));
    private static readonly Brush DefaultCross = new SolidColorBrush(Color.FromRgb(0x27, 0x31, 0x42));
    private static readonly Brush DefaultMarker = new SolidColorBrush(Color.FromRgb(0x00, 0xA8, 0xFF));
    private static readonly Brush DefaultCenter = new SolidColorBrush(Color.FromRgb(0x5A, 0x6A, 0x82));

    public static readonly DependencyProperty XProperty = DependencyProperty.Register(
        nameof(X), typeof(double), typeof(StickVisual),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty YProperty = DependencyProperty.Register(
        nameof(Y), typeof(double), typeof(StickVisual),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundBrushProperty = DependencyProperty.Register(
        nameof(BackgroundBrush), typeof(Brush), typeof(StickVisual),
        new FrameworkPropertyMetadata(DefaultBackground, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RingBrushProperty = DependencyProperty.Register(
        nameof(RingBrush), typeof(Brush), typeof(StickVisual),
        new FrameworkPropertyMetadata(DefaultRing, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CrossBrushProperty = DependencyProperty.Register(
        nameof(CrossBrush), typeof(Brush), typeof(StickVisual),
        new FrameworkPropertyMetadata(DefaultCross, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MarkerBrushProperty = DependencyProperty.Register(
        nameof(MarkerBrush), typeof(Brush), typeof(StickVisual),
        new FrameworkPropertyMetadata(DefaultMarker, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CenterBrushProperty = DependencyProperty.Register(
        nameof(CenterBrush), typeof(Brush), typeof(StickVisual),
        new FrameworkPropertyMetadata(DefaultCenter, FrameworkPropertyMetadataOptions.AffectsRender));

    public double X { get => (double)GetValue(XProperty); set => SetValue(XProperty, value); }

    public double Y { get => (double)GetValue(YProperty); set => SetValue(YProperty, value); }

    public Brush BackgroundBrush { get => (Brush)GetValue(BackgroundBrushProperty); set => SetValue(BackgroundBrushProperty, value); }

    public Brush RingBrush { get => (Brush)GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }

    public Brush CrossBrush { get => (Brush)GetValue(CrossBrushProperty); set => SetValue(CrossBrushProperty, value); }

    public Brush MarkerBrush { get => (Brush)GetValue(MarkerBrushProperty); set => SetValue(MarkerBrushProperty, value); }

    public Brush CenterBrush { get => (Brush)GetValue(CenterBrushProperty); set => SetValue(CenterBrushProperty, value); }

    public StickVisual()
    {
        SizeChanged += (_, _) => InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0)
            return;

        double cx = w / 2d;
        double cy = h / 2d;
        double radius = Math.Min(w, h) / 2d - 1.5;

        var ringPen = new Pen(RingBrush, 1.5);
        dc.DrawEllipse(BackgroundBrush, ringPen, new Point(cx, cy), radius, radius);

        double crossPen = Math.Max(1, Math.Min(w, h) / 220d);
        var linePen = new Pen(CrossBrush, crossPen)
        {
            DashStyle = new DashStyle([3, 3], 0),
        };
        dc.DrawLine(linePen, new Point(cx - radius * 0.72, cy), new Point(cx + radius * 0.72, cy));
        dc.DrawLine(linePen, new Point(cx, cy - radius * 0.72), new Point(cx, cy + radius * 0.72));

        double thumbR = Math.Max(7, radius * 0.2);
        double travel = Math.Max(0, radius - thumbR - 1);

        double nx = Math.Clamp(X, -1d, 1d);
        double ny = Math.Clamp(Y, -1d, 1d);
        double px = cx + nx * travel;
        double py = cy - ny * travel;

        dc.DrawEllipse(MarkerBrush, null, new Point(cx, cy), thumbR * 0.32, thumbR * 0.32);
        dc.DrawEllipse(MarkerBrush, null, new Point(px, py), thumbR, thumbR);

        var outlinePen = new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1);
        dc.DrawEllipse(null, outlinePen, new Point(px, py), thumbR, thumbR);
    }
}