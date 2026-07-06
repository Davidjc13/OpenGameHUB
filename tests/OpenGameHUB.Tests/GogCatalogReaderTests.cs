using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Providers.Gog;

namespace OpenGameHUB.Tests;

public sealed class GogCatalogReaderTests
{
    [Fact]
    public void BuildLaunchArguments_includes_install_path_when_running()
    {
        var args = GogCatalogReader.BuildLaunchArguments(1207658924, @"C:\Games\Witcher3", install: false);
        Assert.Contains("/command=runGame", args);
        Assert.Contains("1207658924", args);
        Assert.Contains(@"C:\Games\Witcher3", args);
    }

    [Fact]
    public void BuildInstallProtocolUrl_uses_galaxy_scheme()
    {
        Assert.Equal("goggalaxy://openGameView/abc123", GogCatalogReader.BuildInstallProtocolUrl("abc123"));
    }

    [Fact]
    public void MatchesInstalledGame_matches_by_gog_id()
    {
        var game = TestGames.Create("gog:1", Platform.Gog, "Game", platformGameId: "42");
        var entry = new GogCatalogEntry(42, "release", "Game");

        Assert.True(GogCatalogReader.MatchesInstalledGame(game, entry));
    }
}
