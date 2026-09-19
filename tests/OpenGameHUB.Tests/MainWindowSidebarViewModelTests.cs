using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Services.Games;
using OpenGameHUB.ViewModels;
using OpenGameHUB.ViewModels.MainWindow;

namespace OpenGameHUB.Tests;

public sealed class MainWindowSidebarViewModelTests
{
    [Fact]
    public void RebuildPlatformFilters_preserves_selected_platform()
    {
        var library = new GameLibraryService();
        var sidebar = CreateSidebar(library, static () => { });

        sidebar.RebuildAll([
            CreateGame("steam:1", Platform.Steam, "Alpha"),
            CreateGame("epic:1", Platform.Epic, "Beta")
        ]);

        var epicFilter = sidebar.PlatformFilters.First(f => f.Platform == Platform.Epic);
        sidebar.SelectedPlatformFilter = epicFilter;

        sidebar.RebuildAll([
            CreateGame("steam:1", Platform.Steam, "Alpha"),
            CreateGame("epic:1", Platform.Epic, "Beta"),
            CreateGame("epic:2", Platform.Epic, "Gamma")
        ]);

        Assert.Equal(Platform.Epic, sidebar.SelectedPlatformFilter?.Platform);
        Assert.Contains("(2)", sidebar.PlatformFilters.First(f => f.Platform == Platform.Epic).Label);
    }

    [Fact]
    public void RebuildLibraryCollections_updates_counts_and_preserves_selection()
    {
        var library = new GameLibraryService();
        var sidebar = CreateSidebar(library, static () => { });

        sidebar.RebuildAll([
            CreateGame("steam:1", Platform.Steam, "Alpha", isFavorite: true, isInstalled: true),
            CreateGame("steam:2", Platform.Steam, "Beta", isInstalled: false)
        ]);

        var favorites = sidebar.LibraryCollections.First(c => c.Kind == LibraryViewKind.Favorites);
        sidebar.SelectedLibraryCollection = favorites;

        sidebar.RebuildAll([
            CreateGame("steam:1", Platform.Steam, "Alpha", isFavorite: true, isInstalled: true),
            CreateGame("steam:2", Platform.Steam, "Beta", isFavorite: true, isInstalled: true),
            CreateGame("steam:3", Platform.Steam, "Gamma", isInstalled: false)
        ]);

        Assert.Equal(LibraryViewKind.Favorites, sidebar.SelectedLibraryCollection?.Kind);
        Assert.Contains("2", sidebar.LibraryCollections.First(c => c.Kind == LibraryViewKind.Favorites).Label);
        Assert.Contains("2", sidebar.LibraryCollections.First(c => c.Kind == LibraryViewKind.Installed).Label);
    }

    private static MainWindowSidebarViewModel CreateSidebar(
        GameLibraryService library,
        Action onFilterChanged)
    {
        return new MainWindowSidebarViewModel(
            library,
            () => throw new InvalidOperationException("Not used in tests"),
            _ => Task.CompletedTask,
            onFilterChanged,
            _ => { },
            _ => { },
            () => null,
            _ => { },
            _ => { });
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
