using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Services.Games;

internal static class GameEntryFilter
{
    private static readonly HashSet<string> ExcludedSteamAppIds = new(StringComparer.Ordinal)
    {
        "228980" // Steamworks Common Redistributables
    };

    public static bool IsExcluded(UnifiedGame game) =>
        IsExcluded(game.Title, game.Platform, game.PlatformGameId, game.InstallPath);

    public static bool IsExcluded(string title, Platform platform, string? platformGameId = null, string? installPath = null)
    {
        if (platform == Platform.Riot)
        {
            if (!string.IsNullOrWhiteSpace(platformGameId) && platformGameId.Contains('.', StringComparison.Ordinal))
                return true;

            if (title.Contains(".live", StringComparison.OrdinalIgnoreCase)
                || title.Contains(".pbe", StringComparison.OrdinalIgnoreCase)
                || title.Contains(".game_patch", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (platform == Platform.Steam && !string.IsNullOrWhiteSpace(platformGameId)
            && ExcludedSteamAppIds.Contains(platformGameId))
        {
            return true;
        }

        if (ContainsUtilityKeyword(title))
            return true;

        return !string.IsNullOrWhiteSpace(installPath)
               && installPath.Contains("steamworks shared", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsUtilityKeyword(string title)
    {
        return title.Contains("steamworks common", StringComparison.OrdinalIgnoreCase)
               || title.Contains("steamworks redistribut", StringComparison.OrdinalIgnoreCase)
               || title.Contains("provisioning", StringComparison.OrdinalIgnoreCase)
               || title.Contains("redistributable", StringComparison.OrdinalIgnoreCase);
    }
}
