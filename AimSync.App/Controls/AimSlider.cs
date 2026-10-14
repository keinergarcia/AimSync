using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace AimSync.App.Controls;

public sealed class AimSlider : Slider
{
    private const double KnobDiameter = 14;

    private Border? _fill;

    static AimSlider()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AimSlider),
            new FrameworkPropertyMetadata(typeof(AimSlider)));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _fill = GetTemplateChild("PART_Fill") as Border;
        UpdateFill();
    }

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        UpdateFill();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateFill();
    }

    private void UpdateFill()
    {
        if (_fill is null || double.IsNaN(ActualWidth) || ActualWidth <= 0)
            return;

        double range = Math.Max(0d, Maximum - Minimum);
        double fraction = range <= 0d
            ? 0d
            : Math.Clamp((Value - Minimum) / range, 0d, 1d);

        _fill.Width = Math.Max(0d, ActualWidth - KnobDiameter) * fraction;
        _fill.Margin = new Thickness(KnobDiameter / 2d, 0, 0, 0);
    }
}