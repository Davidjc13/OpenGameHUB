using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace OpenGameHUB.Styles;

/// <summary>Theme color helpers. Brushes resolve from Tokens.axaml ThemeDictionaries.</summary>
public static class ThemeColors
{
    public const string Accent = "#3D6AF2";
    public const string BorderDefault = "#2B3448";

    public static IBrush ParseBrush(string hex) => Brush.Parse(hex);

    public static IBrush GetBrush(string resourceKey, string fallbackHex)
    {
        var app = Application.Current;
        if (app is not null
            && app.TryGetResource(resourceKey, app.ActualThemeVariant, out var resource)
            && resource is IBrush brush)
        {
            return brush;
        }

        return ParseBrush(fallbackHex);
    }

    public static IBrush AccentBrush => GetBrush("BrushAccent", Accent);
    public static IBrush BorderDefaultBrush => GetBrush("BrushBorder", BorderDefault);
    public static IBrush SelectedBorderBrush => AccentBrush;
    public static IBrush UnselectedBorderBrush => BorderDefaultBrush;
}
