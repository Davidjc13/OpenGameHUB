using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using OpenGameHUB.Styles;

namespace OpenGameHUB.Converters;

public sealed class SelectionBorderConverter : IValueConverter
{
    public static readonly SelectionBorderConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var selected = value is true;
        return selected ? ThemeColors.SelectedBorderBrush : ThemeColors.UnselectedBorderBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
