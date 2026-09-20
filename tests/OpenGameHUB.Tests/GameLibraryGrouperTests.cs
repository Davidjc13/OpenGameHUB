using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Services.Games;

namespace OpenGameHUB.Tests;

public sealed class GameLibraryGrouperTests
{
    [Fact]
    public void GroupByTitle_merges_steam_and_gog_with_same_title()
    {
        var games = new List<UnifiedGame>
        {
            TestGames.Create("steam:730", Platform.Steam, "Counter-Strike 2"),
            TestGames.Create("gog:catalog:1207658930", Platform.Gog, "Counter-Strike 2")
        };

        var grouped = GameLibraryGrouper.GroupByTitle(games);

        Assert.Single(grouped);
        Assert.Equal(Platform.Steam, grouped[0].Platform);
        Assert.Single(grouped[0].AlternateListings);
        Assert.Equal(Platform.Gog, grouped[0].AlternateListings[0].Platform);
    }

    [Fact]
    public void GroupByTitle_keeps_distinct_titles_separate()
    {
        var games = new List<UnifiedGame>
        {
            TestGames.Create("steam:570", Platform.Steam, "Dota 2"),
            TestGames.Create("steam:730", Platform.Steam, "Counter-Strike 2")
        };

        var grouped = GameLibraryGrouper.GroupByTitle(games);

        Assert.Equal(2, grouped.Count);
    }

    [Fact]
    public void GroupByTitle_does_not_group_same_platform_entries()
    {
        var games = new List<UnifiedGame>
        {
            TestGames.Create("steam:570", Platform.Steam, "Dota 2"),
            TestGames.Create("steam:730", Platform.Steam, "Dota 2")
        };

        var grouped = GameLibraryGrouper.GroupByTitle(games);

        Assert.Equal(2, grouped.Count);
    }

    [Fact]
    public void GroupByTitle_prefers_installed_listing_as_primary()
    {
        var games = new List<UnifiedGame>
        {
            TestGames.Create("steam:123", Platform.Steam, "Hollow Knight", installed: false),
            TestGames.Create("gog:catalog:999", Platform.Gog, "Hollow Knight", installed: true, installPath: CreateTempInstallPath())
        };

        try
        {
            var grouped = GameLibraryGrouper.GroupByTitle(games);

            Assert.Single(grouped);
            Assert.Equal(Platform.Gog, grouped[0].Platform);
            Assert.True(grouped[0].IsInstalled);
            Assert.Single(grouped[0].AlternateListings);
            Assert.Equal(Platform.Steam, grouped[0].AlternateListings[0].Platform);
        }
        finally
        {
            CleanupInstallPath(games[1].InstallPath);
        }
    }

    [Fact]
    public void GroupByTitle_normalizes_title_suffixes()
    {
        var games = new List<UnifiedGame>
        {
            TestGames.Create("steam:123", Platform.Steam, "Hollow Knight™"),
            TestGames.Create("gog:catalog:999", Platform.Gog, "Hollow Knight - PC")
        };

        var grouped = GameLibraryGrouper.GroupByTitle(games);

        Assert.Single(grouped);
        Assert.Single(grouped[0].AlternateListings);
    }

    [Theory]
    [InlineData(Platform.Steam, Platform.Epic)]
    [InlineData(Platform.Steam, Platform.Ubisoft)]
    [InlineData(Platform.Steam, Platform.Ea)]
    [InlineData(Platform.Steam, Platform.GamePass)]
    [InlineData(Platform.Steam, Platform.Rockstar)]
    [InlineData(Platform.Epic, Platform.Gog)]
    [InlineData(Platform.Ubisoft, Platform.Gog)]
    [InlineData(Platform.Ea, Platform.Epic)]
    public void GroupByTitle_merges_any_cross_platform_pair_with_same_title(Platform first, Platform second)
    {
        var games = new List<UnifiedGame>
        {
            TestGames.Create($"{first.ToString().ToLowerInvariant()}:1", first, "Control"),
            TestGames.Create($"{second.ToString().ToLowerInvariant()}:catalog:1", second, "Control")
        };

        var grouped = GameLibraryGrouper.GroupByTitle(games);

        Assert.Single(grouped);
        Assert.Single(grouped[0].AlternateListings);
        var platforms = new[] { grouped[0].Platform, grouped[0].AlternateListings[0].Platform };
        Assert.Contains(first, platforms);
        Assert.Contains(second, platforms);
    }

    [Fact]
    public void GroupByTitle_merges_three_platform_listings()
    {
        var games = new List<UnifiedGame>
        {
            TestGames.Create("steam:123", Platform.Steam, "Hades"),
            TestGames.Create("gog:catalog:456", Platform.Gog, "Hades"),
            TestGames.Create("epic:legendary:hades", Platform.Epic, "Hades")
        };

        var grouped = GameLibraryGrouper.GroupByTitle(games);

        Assert.Single(grouped);
        Assert.Equal(Platform.Steam, grouped[0].Platform);
        Assert.Equal(2, grouped[0].AlternateListings.Count);
        Assert.Contains(grouped[0].AlternateListings, g => g.Platform == Platform.Gog);
        Assert.Contains(grouped[0].AlternateListings, g => g.Platform == Platform.Epic);
    }

    [Fact]
    public void GroupByTitle_survives_deduplicate_before_grouping()
    {
        var games = new List<UnifiedGame>
        {
            TestGames.Create("steam:620", Platform.Steam, "Portal 2"),
            TestGames.Create("epic:legendary:portal2", Platform.Epic, "Portal 2"),
            TestGames.Create("ubisoft:catalog:portal2", Platform.Ubisoft, "Portal 2")
        };

        var deduped = GameLibraryMerger.Deduplicate(games);
        var grouped = GameLibraryGrouper.GroupByTitle(deduped);

        Assert.Single(grouped);
        Assert.Equal(2, grouped[0].AlternateListings.Count);
    }

    [Fact]
    public void GroupByTitle_leaves_custom_games_ungrouped()
    {
        var games = new List<UnifiedGame>
        {
            TestGames.Create("custom:1", Platform.Custom, "My Game"),
            TestGames.Create("steam:123", Platform.Steam, "My Game")
        };

        var grouped = GameLibraryGrouper.GroupByTitle(games);

        Assert.Equal(2, grouped.Count);
        Assert.DoesNotContain(grouped, g => g.HasAlternateListings);
    }

    private static string CreateTempInstallPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ogh-group-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CleanupInstallPath(string? installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
            return;

        Directory.Delete(installPath, recursive: true);
    }
}
