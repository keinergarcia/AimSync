using System.Windows;
using System.Windows.Controls;

namespace AimSync.App.Controls;

public sealed class ButtonFace : ContentControl
{
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(ButtonFace),
        new PropertyMetadata(false));

    public static readonly DependencyProperty RoundProperty = DependencyProperty.Register(
        nameof(Round), typeof(bool), typeof(ButtonFace),
        new PropertyMetadata(false));

    static ButtonFace()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ButtonFace),
            new FrameworkPropertyMetadata(typeof(ButtonFace)));
    }

    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    public bool Round { get => (bool)GetValue(RoundProperty); set => SetValue(RoundProperty, value); }
}