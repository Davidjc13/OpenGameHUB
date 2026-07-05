using Avalonia.Data.Converters;
using Avalonia.Media;
using OpenGameHUB.Models;
using System.Globalization;

namespace OpenGameHUB.Converters;

public static class PlatformBrushes
{
    public static IBrush For(Platform platform) => platform switch
    {
        Platform.Steam => Brush.Parse("#1B6FAD"),
        Platform.Epic => Brush.Parse("#2F2F2F"),
        Platform.Gog => Brush.Parse("#86328A"),
        Platform.Ubisoft => Brush.Parse("#0070FF"),
        Platform.Ea => Brush.Parse("#FF4747"),
        Platform.BattleNet => Brush.Parse("#148EFF"),
        Platform.Rockstar => Brush.Parse("#FCAF17"),
        Platform.Riot => Brush.Parse("#D13639"),
        Platform.GamePass => Brush.Parse("#107C10"),
        _ => Brush.Parse("#5C5C5C")
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
