using OpenGameHUB.Models;
using OpenGameHUB.Services;
using OpenGameHUB.Services.Epic;

namespace OpenGameHUB.Services.LibraryProviders;

public sealed class EpicCloudLibraryProvider : ICloudLibraryProvider
{
    public Platform Platform => Platform.Epic;

    public bool IsAvailable() => LegendaryClient.IsAvailable();

    public IReadOnlyList<UnifiedGame> GetUninstalledLibraryGames(
        IReadOnlyList<UnifiedGame> currentGames,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegendaryClient.IsAvailable())
            return [];

        var installedIds = new HashSet<string>(
            EpicManifestScanner.GetInstalledAppNames(),
            StringComparer.OrdinalIgnoreCase);
        foreach (var id in currentGames
                     .Where(g => g.Platform == Platform.Epic)
                     .Select(g => g.PlatformGameId)
                     .Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            installedIds.Add(id);
        }

        var installedTitles = currentGames
            .Where(g => g.Platform == Platform.Epic && g.IsInstalled)
            .Select(g => MetadataSearchHelper.NormalizeTitle(g.Title).ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        var results = new List<UnifiedGame>();

        foreach (var entry in LegendaryClient.ListCatalogEntries(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var installUrl = entry.BuildInstallProtocolUrl();
            if (string.IsNullOrWhiteSpace(installUrl))
                continue;

            if (installedIds.Contains(entry.AppName))
                continue;

            var titleKey = MetadataSearchHelper.NormalizeTitle(entry.AppTitle).ToLowerInvariant();
            if (installedTitles.Contains(titleKey))
                continue;

            var game = new UnifiedGame
            {
                Id = $"epic:legendary:{entry.AppName}",
                Platform = Platform.Epic,
                PlatformGameId = entry.AppName,
                Title = entry.AppTitle,
                IsInstalled = false,
                LaunchSpec = LaunchSpec.Protocol(installUrl)
            };

            if (!GameEntryFilter.IsExcluded(game))
                results.Add(game);
        }

        return results;
    }

    public IEnumerable<Action> GetInstallLaunchAttempts(UnifiedGame game)
    {
        if (game.Platform != Platform.Epic || game.IsInstalled)
            yield break;

        if (game.LaunchSpec.Kind != "protocol" || string.IsNullOrWhiteSpace(game.LaunchSpec.Value))
            yield break;

        var installUrl = game.LaunchSpec.Value;
        yield return () => EpicLauncherClient.StartInstall(installUrl);
    }
}
