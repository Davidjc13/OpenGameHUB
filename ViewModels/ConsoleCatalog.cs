using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Services.Games;

namespace OpenGameHUB.ViewModels;

public enum ConsoleCategoryKind
{
    All,
    Installed,
    Favorites,
    Platform
}

public readonly record struct ConsoleCategory(ConsoleCategoryKind Kind, Platform? Platform);

public static class ConsoleCatalog
{
    public static IReadOnlyList<ConsoleCategory> Build(IEnumerable<GameItemViewModel> games)
    {
        var categories = new List<ConsoleCategory>
        {
            new(ConsoleCategoryKind.All, null),
            new(ConsoleCategoryKind.Installed, null),
            new(ConsoleCategoryKind.Favorites, null)
        };

        var platforms = games
            .SelectMany(PlatformsOf)
            .Where(platform => platform != Platform.Unknown)
            .Distinct()
            .OrderBy(platform => PlatformLabels.Get(platform), StringComparer.OrdinalIgnoreCase);

        foreach (var platform in platforms)
            categories.Add(new ConsoleCategory(ConsoleCategoryKind.Platform, platform));

        return categories;
    }

    public static List<GameItemViewModel> Filter(
        IReadOnlyList<GameItemViewModel> games,
        ConsoleCategoryKind kind,
        Platform? platform,
        string? search)
    {
        IEnumerable<GameItemViewModel> filtered = kind switch
        {
            ConsoleCategoryKind.Installed => games.Where(game => game.Source.IsInstalled),
            ConsoleCategoryKind.Favorites => games.Where(game => game.IsFavorite),
            ConsoleCategoryKind.Platform when platform is Platform selected =>
                games.Where(game => game.MatchesPlatform(selected)),
            _ => games
        };

        var query = search?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(query))
            filtered = filtered.Where(game => GameSearchHelper.MatchesTitle(game.Title, query));

        return filtered
            .OrderByDescending(game => game.Source.LastPlayed)
            .ThenBy(game => game.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<Platform> PlatformsOf(GameItemViewModel game)
    {
        yield return game.Platform;
        foreach (var alternate in game.Source.AlternateListings)
            yield return alternate.Platform;
    }
}
