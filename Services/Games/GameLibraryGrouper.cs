using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Services.Covers;

namespace OpenGameHUB.Services.Games;

/// <summary>
/// Groups the same game across different store platforms (Steam, GOG, Epic, Ubisoft, EA, etc.)
/// by normalized title. Same-platform duplicates are left untouched.
/// </summary>
internal static class GameLibraryGrouper
{
    public static List<UnifiedGame> GroupByTitle(IReadOnlyList<UnifiedGame> games)
    {
        if (games.Count == 0)
            return [];

        var titleGroups = games
            .Where(g => g.Platform != Platform.Custom)
            .GroupBy(g => GetTitleGroupKey(g), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var groupedPrimaries = new Dictionary<string, UnifiedGame>(StringComparer.OrdinalIgnoreCase);
        var consumedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (titleKey, members) in titleGroups)
        {
            if (members.Select(g => g.Platform).Distinct().Count() <= 1)
                continue;

            var primary = GameLibraryMerger.PickPreferred(members);
            var alternates = members
                .Where(g => !string.Equals(g.Id, primary.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(g => PlatformLabels.Get(g.Platform), StringComparer.OrdinalIgnoreCase)
                .ToList();

            groupedPrimaries[titleKey] = AttachAlternates(primary, alternates);
            foreach (var member in members)
                consumedIds.Add(member.Id);
        }

        var result = new List<UnifiedGame>(games.Count - consumedIds.Count + groupedPrimaries.Count);
        foreach (var game in games)
        {
            if (consumedIds.Contains(game.Id))
            {
                var titleKey = GetTitleGroupKey(game);
                if (groupedPrimaries.TryGetValue(titleKey, out var primary)
                    && string.Equals(primary.Id, game.Id, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(primary);
                    groupedPrimaries.Remove(titleKey);
                }

                continue;
            }

            result.Add(game);
        }

        return result
            .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetTitleGroupKey(UnifiedGame game) =>
        MetadataSearchHelper.NormalizeTitle(game.Title).ToLowerInvariant();

    private static UnifiedGame AttachAlternates(UnifiedGame primary, IReadOnlyList<UnifiedGame> alternates)
    {
        if (alternates.Count == 0)
            return primary;

        return new UnifiedGame
        {
            Id = primary.Id,
            Platform = primary.Platform,
            PlatformGameId = primary.PlatformGameId,
            Title = primary.Title,
            IsInstalled = primary.IsInstalled,
            InstallPath = primary.InstallPath,
            CoverPath = primary.CoverPath,
            HasCustomCover = primary.HasCustomCover,
            CatalogCoverUrl = primary.CatalogCoverUrl,
            PlaytimeMinutes = primary.PlaytimeMinutes,
            LastPlayed = primary.LastPlayed,
            IsFavorite = primary.IsFavorite,
            LaunchSpec = primary.LaunchSpec,
            AlternateListings = alternates
        };
    }
}
