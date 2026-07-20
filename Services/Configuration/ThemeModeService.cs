using Avalonia;
using Avalonia.Styling;
using OpenGameHUB.Domain.Enums;

namespace OpenGameHUB.Services.Configuration;

internal static class ThemeModeService
{
    public static ThemeMode Normalize(ThemeMode mode) =>
        Enum.IsDefined(mode) ? mode : ThemeMode.System;

    public static ThemeVariant ToThemeVariant(ThemeMode mode) => Normalize(mode) switch
    {
        ThemeMode.Light => ThemeVariant.Light,
        ThemeMode.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default
    };

    public static void Apply(ThemeMode mode)
    {
        if (Application.Current is null)
            return;

        Application.Current.RequestedThemeVariant = ToThemeVariant(mode);
    }
}
