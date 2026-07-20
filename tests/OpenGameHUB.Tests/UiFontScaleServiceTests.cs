using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Services.Configuration;

namespace OpenGameHUB.Tests;

public sealed class UiFontScaleServiceTests
{
    [Theory]
    [InlineData(UiFontScale.ExtraSmall, 0.8)]
    [InlineData(UiFontScale.Small, 0.9)]
    [InlineData(UiFontScale.Normal, 1.0)]
    [InlineData(UiFontScale.Large, 1.15)]
    [InlineData(UiFontScale.ExtraLarge, 1.3)]
    public void GetFactor_matches_expected_scale(UiFontScale scale, double expected) =>
        Assert.Equal(expected, UiFontScaleService.GetFactor(scale));

    [Fact]
    public void Normalize_maps_undefined_values_to_normal() =>
        Assert.Equal(UiFontScale.Normal, UiFontScaleService.Normalize((UiFontScale)99));
}
