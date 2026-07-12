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

    private static GameItemViewModel CreateGame(
        string id,
        Platform platform,
        string title,
        bool isFavorite = false,
        bool isInstalled = false)
    {
        return new GameItemViewModel(new UnifiedGame
        {
            Id = id,
            Platform = platform,
            PlatformGameId = id,
            Title = title,
            IsFavorite = isFavorite,
            IsInstalled = isInstalled,
            LaunchSpec = LaunchSpec.Executable("game.exe")
        });
    }
}
