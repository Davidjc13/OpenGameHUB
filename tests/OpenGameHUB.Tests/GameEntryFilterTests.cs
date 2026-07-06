using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Services.Games;

namespace OpenGameHUB.Tests;

public sealed class GameEntryFilterTests
{
    [Fact]
    public void IsExcluded_filters_steamworks_redistributable_app_id()
    {
        Assert.True(GameEntryFilter.IsExcluded(
            "Steamworks Common Redistributables",
            Platform.Steam,
            platformGameId: "228980"));
    }

    [Fact]
    public void IsExcluded_filters_riot_metadata_product_ids_with_dots()
    {
        Assert.True(GameEntryFilter.IsExcluded(
            "league_of_legends.live",
            Platform.Riot,
            platformGameId: "league_of_legends.live"));
    }

    [Fact]
    public void IsExcluded_allows_normal_riot_catalog_entry()
    {
        var game = TestGames.Create("riot:catalog:valorant@live", Platform.Riot, "VALORANT");
        Assert.False(GameEntryFilter.IsExcluded(game));
    }

    [Fact]
    public void IsExcluded_filters_utility_title_keywords()
    {
        Assert.True(GameEntryFilter.IsExcluded(
            "Steamworks Common Redistributables",
            Platform.Steam,
            platformGameId: "123"));
    }

    [Fact]
    public void IsExcluded_allows_regular_steam_game()
    {
        Assert.False(GameEntryFilter.IsExcluded(
            "Portal 2",
            Platform.Steam,
            platformGameId: "620"));
    }

    [Fact]
    public void IsExcluded_filters_riot_pbe_title_suffix()
    {
        Assert.True(GameEntryFilter.IsExcluded(
            "valorant.pbe",
            Platform.Riot,
            platformGameId: "valorant"));
    }

    [Fact]
    public void IsExcluded_filters_provisioning_utilities()
    {
        Assert.True(GameEntryFilter.IsExcluded(
            "Provisioning Tool",
            Platform.Steam,
            platformGameId: "999"));
    }

    [Fact]
    public void IsExcluded_filters_steamworks_shared_install_path()
    {
        Assert.True(GameEntryFilter.IsExcluded(
            "Some Tool",
            Platform.Steam,
            installPath: @"C:\Program Files (x86)\Steam\steamworks shared\tool"));
    }
}
