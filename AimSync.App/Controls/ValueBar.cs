using System.Windows;
using System.Windows.Media;

namespace AimSync.App.Controls;

public sealed class ValueBar : FrameworkElement
{
    private static readonly Brush DefaultTrack = new SolidColorBrush(Color.FromRgb(0x2A, 0x33, 0x42));
    private static readonly Brush DefaultFill = new SolidColorBrush(Color.FromRgb(0x00, 0xA8, 0xFF));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(ValueBar),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(ValueBar),
        new FrameworkPropertyMetadata(DefaultTrack, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(
        nameof(FillBrush), typeof(Brush), typeof(ValueBar),
        new FrameworkPropertyMetadata(DefaultFill, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public Brush TrackBrush { get => (Brush)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public Brush FillBrush { get => (Brush)GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    public ValueBar()
    {
        SizeChanged += (_, _) => InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0)
            return;

        double corner = h / 2d;
        dc.DrawRoundedRectangle(TrackBrush, null, new Rect(0, 0, w, h), corner, corner);

        double fill = Math.Clamp(Value, 0d, 1d) * w;
        if (fill <= 0)
            return;

        var clip = new RectangleGeometry(new Rect(0, 0, fill, h), corner, corner);
        dc.PushClip(clip);
        dc.DrawRoundedRectangle(FillBrush, null, new Rect(0, 0, w, h), corner, corner);
        dc.Pop();
    }
}