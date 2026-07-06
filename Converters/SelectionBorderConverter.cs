using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OpenGameHUB.Converters;

public sealed class SelectionBorderConverter : IValueConverter
{
    public static readonly SelectionBorderConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var selected = value is true;
        return selected ? Brush.Parse("#3D6AF2") : Brush.Parse("#2B3448");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
