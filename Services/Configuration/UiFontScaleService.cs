using Avalonia;
using OpenGameHUB.Domain.Enums;

namespace OpenGameHUB.Services.Configuration;

internal static class UiFontScaleService
{
    private static readonly (string Key, double Base)[] FontSizes =
    [
        ("FontSizeBadge", 10),
        ("FontSizeListMeta", 11),
        ("FontSizeCaption", 12),
        ("FontSizeLabel", 13),
        ("FontSizeBody", 14),
        ("FontSizeSubtitle", 16),
        ("FontSizeIcon", 16),
        ("FontSizeTitle", 18),
        ("FontSizeHeadingMd", 22),
        ("FontSizeHeadingLg", 24),
        ("FontSizeHeadingXl", 28)
    ];

    private static readonly (string Key, double Base)[] LineHeights =
    [
        ("LineHeightCaption", 16),
        ("LineHeightLabel", 18),
        ("LineHeightBody", 20),
        ("LineHeightBodyLoose", 21),
        ("LineHeightTitle", 24),
        ("LineHeightHeadingXl", 34)
    ];

    public static double GetFactor(UiFontScale scale) => Normalize(scale) switch
    {
        UiFontScale.ExtraSmall => 0.8,
        UiFontScale.Small => 0.9,
        UiFontScale.Large => 1.15,
        UiFontScale.ExtraLarge => 1.3,
        _ => 1.0
    };

    public static UiFontScale Normalize(UiFontScale scale) =>
        Enum.IsDefined(scale) ? scale : UiFontScale.Normal;

    public static void Apply(UiFontScale scale)
    {
        var resources = Application.Current?.Resources;
        if (resources is null)
            return;

        var factor = GetFactor(scale);
        foreach (var (key, bas) in FontSizes)
            resources[key] = Scale(bas, factor);

        foreach (var (key, bas) in LineHeights)
            resources[key] = Scale(bas, factor);
    }

    private static double Scale(double value, double factor) =>
        Math.Round(value * factor, 1, MidpointRounding.AwayFromZero);
}
