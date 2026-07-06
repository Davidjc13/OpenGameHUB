using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Services.Games;

namespace OpenGameHUB.Tests;

public sealed class GameLibraryMergerTests
{
    [Fact]
    public void Deduplicate_prefers_riot_over_epic_for_same_title()
    {
        var games = new List<UnifiedGame>
        {
            TestGames.Create("epic:path:valorant", Platform.Epic, "VALORANT", installed: true),
            TestGames.Create("riot:path:valorant", Platform.Riot, "VALORANT", installed: true)
        };

        var merged = GameLibraryMerger.Deduplicate(games);

        Assert.Single(merged);
        Assert.Equal(Platform.Riot, merged[0].Platform);
    }

    [Fact]
    public void Deduplicate_prefers_riot_over_epic_for_same_install_path()
    {
        var installPath = Path.Combine(Path.GetTempPath(), "OpenGameHUB-test-valorant");
        Directory.CreateDirectory(installPath);

        try
        {
            var games = new List<UnifiedGame>
            {
                TestGames.Create("epic:legendary:valorant", Platform.Epic, "VALORANT", installed: true, installPath),
                TestGames.Create("riot:catalog:valorant@live", Platform.Riot, "VALORANT", installed: true, installPath)
            };

            var merged = GameLibraryMerger.Deduplicate(games);

            Assert.Single(merged);
            Assert.Equal(Platform.Riot, merged[0].Platform);
        }
        finally
        {
            Directory.Delete(installPath, recursive: true);
        }
    }

    [Fact]
    public void Deduplicate_keeps_distinct_catalog_ids()
    {
        var games = new List<UnifiedGame>
        {
            TestGames.Create("steam:store:570", Platform.Steam, "Dota 2"),
            TestGames.Create("steam:store:730", Platform.Steam, "Counter-Strike 2")
        };

        var merged = GameLibraryMerger.Deduplicate(games);

        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void PreserveCatalogEntriesForFailedProviders_keeps_stale_cloud_rows_on_sync_failure()
    {
        var scanned = new List<UnifiedGame>
        {
            TestGames.Create("steam:path:abc", Platform.Steam, "Installed Game", installed: true)
        };

        var existing = new List<UnifiedGame>
        {
            TestGames.Create("ea:catalog:offer-1", Platform.Ea, "Battlefield", installed: false)
        };

        var result = GameLibraryMerger.PreserveCatalogEntriesForFailedProviders(
            scanned,
            existing,
            new HashSet<Platform> { Platform.Ea });

        Assert.Equal(2, result.Count);
        Assert.Contains(result, g => g.Id == "ea:catalog:offer-1");
    }

    [Fact]
    public void PreserveCatalogEntriesForFailedProviders_does_nothing_when_no_failures()
    {
        var scanned = new List<UnifiedGame>
        {
            TestGames.Create("gog:catalog:1", Platform.Gog, "Game")
        };

        var existing = new List<UnifiedGame>
        {
            TestGames.Create("ea:catalog:offer-1", Platform.Ea, "Battlefield", installed: false)
        };

        var result = GameLibraryMerger.PreserveCatalogEntriesForFailedProviders(
            scanned,
            existing,
            new HashSet<Platform>());

        Assert.Single(result);
    }
}
