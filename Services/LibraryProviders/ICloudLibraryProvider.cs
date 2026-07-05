using OpenGameHUB.Models;

namespace OpenGameHUB.Services.LibraryProviders;

/// <summary>
/// Lists owned-but-uninstalled games and provides install launch strategies per platform.
/// TODO: migrate SteamWebApiService and LegendaryClient to this interface.
/// </summary>
public interface ICloudLibraryProvider
{
    Platform Platform { get; }

    bool IsAvailable();

    IReadOnlyList<UnifiedGame> GetUninstalledLibraryGames(
        IReadOnlyList<UnifiedGame> currentGames,
        CancellationToken cancellationToken = default);

    IEnumerable<Action> GetInstallLaunchAttempts(UnifiedGame game);
}
