using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Providers.Riot;

namespace OpenGameHUB.Tests;

public sealed class RiotLauncherClientTests
{
    [Fact]
    public void ResolveProductId_uses_platform_game_id_when_valid()
    {
        var game = TestGames.Create(
            "riot:catalog:valorant@live",
            Platform.Riot,
            "VALORANT",
            platformGameId: "valorant");

        Assert.Equal("valorant", RiotLauncherClient.ResolveProductId(game));
    }

    [Fact]
    public void ResolveProductId_falls_back_to_catalog_id_when_platform_game_id_has_dots()
    {
        var game = TestGames.Create(
            "riot:catalog:valorant@live",
            Platform.Riot,
            "VALORANT",
            platformGameId: "valorant.live");

        Assert.Equal("valorant", RiotLauncherClient.ResolveProductId(game));
    }

    [Fact]
    public void ResolvePatchline_reads_suffix_from_catalog_id()
    {
        var game = TestGames.Create("riot:catalog:valorant@pbe", Platform.Riot, "VALORANT");

        Assert.Equal("pbe", RiotLauncherClient.ResolvePatchline(game));
    }
}
