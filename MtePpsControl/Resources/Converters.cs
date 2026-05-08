using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MtePpsControl.Resources;

/// <summary>True ⇒ Visible, False ⇒ Collapsed. Built-in equivalent without referencing PresentationFramework's nested type.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

/// <summary>Two-way: int value <-> bool checked-state where ConverterParameter is the int we're testing for.</summary>
public sealed class IntEqualsCheckConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        if (!int.TryParse(parameter.ToString(), out var p)) return false;
        return value is int v && v == p;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null && int.TryParse(parameter.ToString(), out var p))
            return p;
        return Binding.DoNothing;
    }
}

/// <summary>Non-empty string ⇒ Visible, otherwise Collapsed.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Inverts a bool — useful for IsEnabled bindings driven by an inverse flag.</summary>
public sealed class BoolNotConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}

/// <summary>Reply-status code (OK/E/?/ERR) ⇒ pill-background colour.</summary>
public sealed class StatusToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as string ?? "";
        return s switch
        {
            "OK"  => Color.FromRgb(0x2E, 0x8B, 0x57),
            "E"   => Color.FromRgb(0xE5, 0x39, 0x35),
            "?"   => Color.FromRgb(0xFF, 0xA8, 0x00),
            "ERR" => Color.FromRgb(0xE5, 0x39, 0x35),
            _     => Color.FromRgb(0x54, 0x63, 0x7A),
        };
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
