using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Services.Covers;

namespace OpenGameHUB.Services.Games;

internal static class GameLibraryMerger
{
    public static List<UnifiedGame> Deduplicate(List<UnifiedGame> games)
    {
        var pathMerged = new List<UnifiedGame>();
        var withoutPath = new List<UnifiedGame>();

        foreach (var group in games.GroupBy(g => NormalizeInstallPath(g.InstallPath) ?? string.Empty))
        {
            if (string.IsNullOrEmpty(group.Key))
                withoutPath.AddRange(group);
            else
                pathMerged.Add(PickPreferredDuplicate(group));
        }

        return pathMerged
            .Concat(withoutPath)
            .GroupBy(GetDedupKey, StringComparer.OrdinalIgnoreCase)
            .Select(PickPreferredDuplicate)
            .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<UnifiedGame> PreserveCatalogEntriesForFailedProviders(
        List<UnifiedGame> scanned,
        IReadOnlyList<UnifiedGame> existing,
        IReadOnlySet<Platform> failedCloudPlatforms)
    {
        if (failedCloudPlatforms.Count == 0)
            return scanned;

        var scannedIds = scanned.Select(g => g.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var game in existing)
        {
            if (!IsCatalogGameId(game.Id)
                || !failedCloudPlatforms.Contains(game.Platform)
                || scannedIds.Contains(game.Id))
            {
                continue;
            }

            scanned.Add(game);
            scannedIds.Add(game.Id);
        }

        return scanned;
    }

    public static string? NormalizeInstallPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return null;

        try
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToLowerInvariant();
        }
        catch
        {
            return path.ToLowerInvariant();
        }
    }

    private static bool IsCatalogGameId(string id) =>
        id.Contains(":catalog:", StringComparison.OrdinalIgnoreCase)
        || id.StartsWith("steam:store:", StringComparison.OrdinalIgnoreCase)
        || id.StartsWith("epic:legendary:", StringComparison.OrdinalIgnoreCase);

    private static string GetDedupKey(UnifiedGame game)
    {
        var installPath = NormalizeInstallPath(game.InstallPath);
        if (!string.IsNullOrEmpty(installPath))
            return installPath;

        if (game.Id.StartsWith("ea:catalog:", StringComparison.OrdinalIgnoreCase)
            || game.Id.StartsWith("ubisoft:catalog:", StringComparison.OrdinalIgnoreCase)
            || game.Id.StartsWith("gog:catalog:", StringComparison.OrdinalIgnoreCase)
            || game.Id.StartsWith("steam:", StringComparison.OrdinalIgnoreCase)
            || game.Id.StartsWith("epic:legendary:", StringComparison.OrdinalIgnoreCase)
            || game.Id.StartsWith("epic:manifest:", StringComparison.OrdinalIgnoreCase)
            || game.Id.StartsWith("gamepass:catalog:", StringComparison.OrdinalIgnoreCase)
            || game.Id.StartsWith("rockstar:catalog:", StringComparison.OrdinalIgnoreCase))
        {
            return game.Id.ToLowerInvariant();
        }

        return MetadataSearchHelper.NormalizeTitle(game.Title).ToLowerInvariant();
    }

    private static UnifiedGame PickPreferredDuplicate(IEnumerable<UnifiedGame> group) =>
        group
            .OrderByDescending(g => g.IsInstalled)
            .ThenByDescending(g => GetPlatformPriority(g.Platform))
            .ThenBy(g => g.Id, StringComparer.Ordinal)
            .First();

    private static int GetPlatformPriority(Platform platform) => platform switch
    {
        Platform.Riot => 100,
        Platform.Steam => 95,
        Platform.Ubisoft => 90,
        Platform.Ea => 88,
        Platform.Gog => 80,
        Platform.BattleNet => 75,
        Platform.Rockstar => 70,
        Platform.GamePass => 68,
        Platform.Epic => 60,
        _ => 0
    };
}
