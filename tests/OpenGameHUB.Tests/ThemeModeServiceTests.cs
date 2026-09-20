using Avalonia.Styling;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Services.Configuration;

namespace OpenGameHUB.Tests;

public sealed class ThemeModeServiceTests
{
    [Theory]
    [InlineData(ThemeMode.System, true)]
    [InlineData(ThemeMode.Light, true)]
    [InlineData(ThemeMode.Dark, true)]
    public void Normalize_keeps_defined_modes(ThemeMode mode, bool _) =>
        Assert.Equal(mode, ThemeModeService.Normalize(mode));

    [Fact]
    public void Normalize_maps_undefined_values_to_system() =>
        Assert.Equal(ThemeMode.System, ThemeModeService.Normalize((ThemeMode)99));

    [Theory]
    [InlineData(ThemeMode.System)]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    public void ToThemeVariant_maps_expected_variants(ThemeMode mode)
    {
        var variant = ThemeModeService.ToThemeVariant(mode);
        var expected = mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
        Assert.Equal(expected, variant);
    }
}
