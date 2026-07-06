using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Services.Games;

/// <summary>
/// Lists owned-but-uninstalled games and provides install launch strategies per platform.
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
