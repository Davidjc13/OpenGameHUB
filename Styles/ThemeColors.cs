using Avalonia.Media;

namespace OpenGameHUB.Styles;

/// <summary>Single source of truth for theme colors (mirrored in Styles/Tokens.axaml).</summary>
public static class ThemeColors
{
    public const string BgApp = "#10131A";
    public const string BgPanel = "#161B27";
    public const string BgCard = "#1E2535";
    public const string BgCover = "#141A28";
    public const string BgInput = "#1A2030";
    public const string BgBanner = "#1A2438";
    public const string BgDevPanel = "#1A1218";

    public const string BorderDefault = "#2B3448";
    public const string BorderBanner = "#3D6FB8";
    public const string BorderDev = "#5C3040";

    public const string Accent = "#3D6AF2";
    public const string AccentHover = "#4F7BFF";

    public const string TextPrimary = "#F4F7FF";
    public const string TextSecondary = "#9AA6BF";
    public const string TextTertiary = "#7E8AA3";
    public const string TextMuted = "#6B7A96";
    public const string TextLabel = "#D7DFF1";
    public const string TextSidebar = "#C5CEE0";
    public const string TextStatus = "#8B97B0";
    public const string TextBanner = "#E8F0FF";
    public const string TextDev = "#F2B8C6";

    public const string OverlayDim = "#66484848";

    public static IBrush ParseBrush(string hex) => global::Avalonia.Media.Brush.Parse(hex);

    public static IBrush AccentBrush => ParseBrush(Accent);
    public static IBrush BorderDefaultBrush => ParseBrush(BorderDefault);
    public static IBrush SelectedBorderBrush => AccentBrush;
    public static IBrush UnselectedBorderBrush => BorderDefaultBrush;
}
