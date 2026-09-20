using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure.Database;
using OpenGameHUB.Services.Games;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Tests;

public sealed class LibraryFilterPipelineTests
{
    [Fact]
    public void Apply_filters_favorites_with_platform_and_search()
    {
        var games = new[]
        {
            CreateGame("steam:1", Platform.Steam, "Alpha", isFavorite: true, isInstalled: true),
            CreateGame("steam:2", Platform.Steam, "Beta", isFavorite: false, isInstalled: true),
            CreateGame("epic:1", Platform.Epic, "Alpha Epic", isFavorite: true, isInstalled: false)
        };

        var result = LibraryFilterPipeline.Apply(
            games,
            new LibraryViewState(LibraryViewKind.Favorites),
            Platform.Steam,
            "alpha",
            SortOption.TitleAsc);

        Assert.Single(result);
        Assert.Equal("steam:1", result[0].Source.Id);
    }

    [Fact]
    public void Apply_filters_user_collection_membership()
    {
        var games = new[]
        {
            CreateGame("steam:1", Platform.Steam, "Alpha"),
            CreateGame("steam:2", Platform.Steam, "Beta")
        };

        var membership = new HashSet<string>(StringComparer.Ordinal) { "steam:1" };

        var result = LibraryFilterPipeline.Apply(
            games,
            new LibraryViewState(LibraryViewKind.UserCollection, "collection-1"),
            null,
            string.Empty,
            SortOption.TitleAsc,
            membership);

        Assert.Single(result);
        Assert.Equal("Alpha", result[0].Title);
    }

    [Fact]
    public void Apply_preserves_sort_order()
    {
        var games = new[]
        {
            CreateGame("steam:2", Platform.Steam, "Zulu"),
            CreateGame("steam:1", Platform.Steam, "Alpha")
        };

        var result = LibraryFilterPipeline.Apply(
            games,
            new LibraryViewState(LibraryViewKind.All),
            null,
            string.Empty,
            SortOption.TitleDesc);

        Assert.Equal("Zulu", result[0].Title);
        Assert.Equal("Alpha", result[1].Title);
    }

    [Fact]
    public void Apply_sorts_by_last_played_descending_with_nulls_last()
    {
        var recent = DateTime.UtcNow.AddDays(-1);
        var older = DateTime.UtcNow.AddDays(-30);
        var games = new[]
        {
            CreateGame("steam:1", Platform.Steam, "No Play"),
            CreateGame("steam:2", Platform.Steam, "Older", lastPlayed: older),
            CreateGame("steam:3", Platform.Steam, "Recent", lastPlayed: recent)
        };

        var result = LibraryFilterPipeline.Apply(
            games,
            new LibraryViewState(LibraryViewKind.All),
            null,
            string.Empty,
            SortOption.LastPlayedDesc);

        Assert.Equal("Recent", result[0].Title);
        Assert.Equal("Older", result[1].Title);
        Assert.Equal("No Play", result[2].Title);
    }

    [Fact]
    public void ResolveDefaultSort_uses_last_played_when_any_game_has_it()
    {
        var games = new[]
        {
            CreateGame("steam:1", Platform.Steam, "Alpha"),
            CreateGame("steam:2", Platform.Steam, "Beta", lastPlayed: DateTime.UtcNow)
        };

        Assert.Equal(SortOption.LastPlayedDesc, LibraryFilterPipeline.ResolveDefaultSort(games));
    }

    [Fact]
    public void ResolveDefaultSort_falls_back_to_title_when_no_last_played()
    {
        var games = new[]
        {
            CreateGame("steam:1", Platform.Steam, "Alpha"),
            CreateGame("steam:2", Platform.Steam, "Beta")
        };

        Assert.Equal(SortOption.TitleAsc, LibraryFilterPipeline.ResolveDefaultSort(games));
    }

    private static GameItemViewModel CreateGame(
        string id,
        Platform platform,
        string title,
        bool isFavorite = false,
        bool isInstalled = false,
        DateTime? lastPlayed = null)
    {
        return new GameItemViewModel(new UnifiedGame
        {
            Id = id,
            Platform = platform,
            PlatformGameId = id,
            Title = title,
            IsFavorite = isFavorite,
            IsInstalled = isInstalled,
            LastPlayed = lastPlayed,
            LaunchSpec = LaunchSpec.Executable("game.exe")
        });
    }
}
