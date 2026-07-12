using OpenGameHUB.Domain.Enums;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Services.Games;

public sealed record LibraryViewState(
    LibraryViewKind Kind,
    string? UserCollectionId = null);

public static class LibraryFilterPipeline
{
    public static List<GameItemViewModel> Apply(
        IEnumerable<GameItemViewModel> games,
        LibraryViewState view,
        Platform? platform,
        string searchText,
        SortOption sort,
        IReadOnlySet<string>? userCollectionGameIds = null)
    {
        IEnumerable<GameItemViewModel> filtered = games;

        if (platform is Platform selectedPlatform)
            filtered = filtered.Where(g => g.Platform == selectedPlatform);

        filtered = view.Kind switch
        {
            LibraryViewKind.Favorites => filtered.Where(g => g.IsFavorite),
            LibraryViewKind.Installed => filtered.Where(g => g.Source.IsInstalled),
            LibraryViewKind.UserCollection when userCollectionGameIds is not null =>
                filtered.Where(g => userCollectionGameIds.Contains(g.Source.Id)),
            _ => filtered
        };

        var query = searchText.Trim();
        if (!string.IsNullOrWhiteSpace(query))
            filtered = filtered.Where(g => GameSearchHelper.MatchesTitle(g.Title, query));

        return (sort switch
        {
            SortOption.TitleDesc => filtered.OrderByDescending(g => g.Title, StringComparer.OrdinalIgnoreCase),
            SortOption.Platform => filtered.OrderBy(g => g.Platform).ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase),
            SortOption.InstalledFirst => filtered.OrderByDescending(g => g.Source.IsInstalled).ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase),
            SortOption.PlaytimeDesc => filtered.OrderByDescending(g => g.Source.PlaytimeMinutes).ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
        }).ToList();
    }
}
