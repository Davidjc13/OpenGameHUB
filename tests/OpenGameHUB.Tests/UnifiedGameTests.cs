using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Tests;

public sealed class UnifiedGameTests
{
    [Fact]
    public void PlatformLabel_delegates_to_platform_labels()
    {
        var game = TestGames.Create("steam:1", Platform.BattleNet, "Game");
        Assert.Equal("Battle.net", game.PlatformLabel);
    }
}
