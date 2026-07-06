using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Providers.Rockstar;

namespace OpenGameHUB.Tests;

public sealed class RockstarCatalogReaderTests
{
    [Theory]
    [InlineData("gta5", "Grand Theft Auto V", "gta5")]
    [InlineData(null, "Grand Theft Auto V Enhanced", "gta5_gen9")]
    public void TryResolveTitleId_resolves_known_titles(string? platformId, string title, string expected)
    {
        Assert.Equal(expected, RockstarCatalogReader.TryResolveTitleId(platformId, title));
    }

    [Fact]
    public void BuildInstallArguments_includes_title_id()
    {
        Assert.Equal("-enableFullMode -install=gta5", RockstarCatalogReader.BuildInstallArguments("gta5"));
    }

    [Fact]
    public void MatchesInstalledGame_matches_known_alias()
    {
        var game = TestGames.Create("rockstar:1", Platform.Rockstar, "Grand Theft Auto V", platformGameId: "gta5");
        var entry = new RockstarCatalogEntry("gta5", "Grand Theft Auto V");

        Assert.True(RockstarCatalogReader.MatchesInstalledGame(game, entry));
    }
}
