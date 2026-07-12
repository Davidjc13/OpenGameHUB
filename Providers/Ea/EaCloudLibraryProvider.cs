using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Providers.Ea;

namespace OpenGameHUB.Providers.Ea;

public sealed class EaCloudLibraryProvider : ICloudLibraryProvider
{
    public Platform Platform => Platform.Ea;

    public bool IsAvailable() => EaCatalogReader.IsCloudLibraryAvailable();

    public IReadOnlyList<UnifiedGame> GetUninstalledLibraryGames(
        IReadOnlyList<UnifiedGame> currentGames,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var installedEaGames = currentGames
            .Where(g => g.Platform == Platform.Ea && g.IsInstalled)
            .ToList();

        IReadOnlyList<EaCatalogEntry> entries;
        try
        {
            entries = EaCatalogReader.ReadLibraryEntries();
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(EaCloudLibraryProvider),
                operation: "GetUninstalledLibraryGames.ReadLibraryEntries",
                exception: ex,
                platform: Platform.Ea);
            return [];
        }
        var results = new List<UnifiedGame>();

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (installedEaGames.Any(installed => EaCatalogReader.MatchesInstalledGame(installed, entry)))
                continue;

            var game = new UnifiedGame
            {
                Id = $"ea:catalog:{entry.SoftwareId}@{entry.BaseSlug}",
                Platform = Platform.Ea,
                PlatformGameId = entry.SoftwareId,
                Title = entry.Title,
                IsInstalled = false,
                LaunchSpec = LaunchSpec.None
            };

            if (!GameEntryFilter.IsExcluded(game))
                results.Add(game);
        }

        return results;
    }

    public IEnumerable<Action> GetInstallLaunchAttempts(UnifiedGame game)
    {
        // Uninstalled EA games can't be installed or deep-linked reliably from here;
        // the UI shows a notice telling the user to install them from the EA App.
        yield break;
    }
}
