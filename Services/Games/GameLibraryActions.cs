using System.Diagnostics.CodeAnalysis;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Providers.Gog;

namespace OpenGameHUB.Services.Games;

internal static class GameLibraryActions
{
    public static bool IsCustom(UnifiedGame game) =>
        game.Platform == Platform.Custom
        || game.Id.StartsWith("custom:", StringComparison.Ordinal);

    public static bool CanRemoveFromLibrary(UnifiedGame game) => IsCustom(game);

    public static bool CanOpenInstallFolder(UnifiedGame game) =>
        TryGetInstallFolder(game, out _);

    public static bool TryGetInstallFolder(UnifiedGame game, [NotNullWhen(true)] out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(game.InstallPath))
            return false;

        var candidate = Path.GetFullPath(game.InstallPath);
        if (!Directory.Exists(candidate))
            return false;

        path = candidate;
        return true;
    }

    public static bool CanViewInStore(UnifiedGame game) => TryGetStoreUrl(game, out _);

    public static bool TryGetStoreUrl(UnifiedGame game, [NotNullWhen(true)] out string url)
    {
        url = string.Empty;
        return game.Platform switch
        {
            Platform.Steam => int.TryParse(game.PlatformGameId, out var appId)
                && ProtocolUri.TrySteamStore(appId, out url),
            Platform.Epic => ProtocolUri.TryEpicStore(game.PlatformGameId, out url),
            Platform.Gog => ProtocolUri.TryGogOpenGameView(GogCatalogReader.TryGetReleaseKey(game), out url),
            _ => false
        };
    }

    public static bool CanUninstall(UnifiedGame game) =>
        game.IsInstalled && !IsCustom(game) && TryGetUninstallSpec(game, out _);

    public static bool TryGetUninstallSpec(UnifiedGame game, [NotNullWhen(true)] out LaunchSpec spec)
    {
        spec = LaunchSpec.None;
        if (!game.IsInstalled || IsCustom(game))
            return false;

        switch (game.Platform)
        {
            case Platform.Steam:
                if (!int.TryParse(game.PlatformGameId, out var appId)
                    || !ProtocolUri.TrySteamUninstall(appId, out var steamUrl))
                    return false;
                spec = LaunchSpec.Protocol(steamUrl);
                return true;
            case Platform.Epic:
                if (!ProtocolUri.TryEpicUninstall(game.PlatformGameId, out var epicUrl))
                    return false;
                spec = LaunchSpec.Protocol(epicUrl);
                return true;
            case Platform.Gog:
                if (!long.TryParse(game.PlatformGameId, out var gogId) || gogId <= 0)
                    return false;
                var galaxy = GogCatalogReader.FindGalaxyClientExecutable();
                if (galaxy is null)
                    return false;
                spec = LaunchSpec.LauncherArgs(galaxy, GogCatalogReader.BuildUninstallArguments(gogId));
                return true;
            case Platform.Ubisoft:
                if (!ProtocolUri.TryUplayUninstall(game.PlatformGameId, out var uplayUrl))
                    return false;
                spec = LaunchSpec.Protocol(uplayUrl);
                return true;
            default:
                return false;
        }
    }
}
