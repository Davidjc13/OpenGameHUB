using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Tests;

public sealed class PlatformLabelsTests
{
    [Theory]
    [InlineData(Platform.Ea, "EA")]
    [InlineData(Platform.BattleNet, "Battle.net")]
    [InlineData(Platform.GamePass, "Game Pass")]
    [InlineData(Platform.Steam, "Steam")]
    public void Get_returns_expected_label(Platform platform, string expected)
    {
        Assert.Equal(expected, PlatformLabels.Get(platform));
    }
}
