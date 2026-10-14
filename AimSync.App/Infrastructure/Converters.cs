using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AimSync.App.Infrastructure;

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class StatusBrushConverter : IValueConverter
{
    private static readonly Brush Ok = new SolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50));
    private static readonly Brush Warn = new SolidColorBrush(Color.FromRgb(0xD2, 0x99, 0x22));
    private static readonly Brush Danger = new SolidColorBrush(Color.FromRgb(0xF8, 0x51, 0x49));
    private static readonly Brush Idle = new SolidColorBrush(Color.FromRgb(0x6E, 0x7B, 0x8E));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? status = value as string;
        if (string.IsNullOrWhiteSpace(status))
            return Idle;

        return status.ToUpperInvariant() switch
        {
            "ACTIVO" or "ACTIVE" or "CONECTADO" or "CONNECTED" or "READY" or "ON" or "OK" => Ok,
            "CONECTANDO..." or "CONNECTING..." or "WARNING" or "WAITING" => Warn,
            "ERROR" or "FAILED" or "OFFLINE" => Danger,
            "INACTIVO" or "INACTIVE" or "IDLE" or "OFF" or "DESCONECTADO" or "DISCONNECTED" => Idle,
            _ => Idle,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class StateBrushConverter : IValueConverter
{
    public Brush? TrueBrush { get; set; }

    public Brush? FalseBrush { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? (TrueBrush ?? Brushes.White) : (FalseBrush ?? Brushes.Gray);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToStateTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? "PULSADO" : "LIBRE";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}