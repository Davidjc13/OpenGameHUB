using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Styles;

namespace OpenGameHUB.Converters;

public static class PlatformBrushes
{
    public static IBrush For(Platform platform) => platform switch
    {
        Platform.Steam => ThemeColors.ParseBrush("#1B6FAD"),
        Platform.Epic => ThemeColors.ParseBrush("#2F2F2F"),
        Platform.Gog => ThemeColors.ParseBrush("#86328A"),
        Platform.Ubisoft => ThemeColors.ParseBrush("#0070FF"),
        Platform.Ea => ThemeColors.ParseBrush("#FF4747"),
        Platform.BattleNet => ThemeColors.ParseBrush("#148EFF"),
        Platform.Rockstar => ThemeColors.ParseBrush("#FCAF17"),
        Platform.Riot => ThemeColors.ParseBrush("#D13639"),
        Platform.GamePass => ThemeColors.ParseBrush("#107C10"),
        _ => ThemeColors.ParseBrush("#5C5C5C")
    };
}

public sealed class PlatformBrushConverter : IValueConverter
{
    public static readonly PlatformBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Platform platform ? PlatformBrushes.For(platform) : PlatformBrushes.For(Platform.Unknown);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
