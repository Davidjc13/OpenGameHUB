using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Providers.Riot;

public sealed class RiotCloudLibraryProvider : ICloudLibraryProvider
{
    public Platform Platform => Platform.Riot;

    public bool IsAvailable() => RiotCatalogReader.IsLauncherInstalled();

    public IReadOnlyList<UnifiedGame> GetUninstalledLibraryGames(
        IReadOnlyList<UnifiedGame> currentGames,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsAvailable())
            return [];

        var installedRiotGames = currentGames
            .Where(g => g.Platform == Platform.Riot)
            .ToList();

        var clientExe = RiotCatalogReader.FindClientServicesExecutable()
            ?? throw new InvalidOperationException(Loc.T("RiotClientNotInstalled"));

        var results = new List<UnifiedGame>();

        IReadOnlyList<RiotCatalogEntry> entries;
        try
        {
            entries = RiotCatalogReader.ReadLibraryEntries();
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(RiotCloudLibraryProvider),
                operation: "GetUninstalledLibraryGames.ReadLibraryEntries",
                exception: ex,
                platform: Platform.Riot);
            return [];
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (installedRiotGames.Any(game => RiotCatalogReader.MatchesInstalledGame(game, entry)))
                continue;

            if (RiotCatalogReader.IsProductInstalled(entry.ProductId, entry.Patchline))
                continue;

            var launchArgs = RiotCatalogReader.BuildLaunchArguments(entry.ProductId, entry.Patchline, install: true);
            var game = new UnifiedGame
            {
                Id = $"riot:catalog:{entry.ProductId}@{entry.Patchline}",
                Platform = Platform.Riot,
                PlatformGameId = entry.ProductId,
                Title = entry.Title,
                IsInstalled = false,
                LaunchSpec = LaunchSpec.LauncherArgs(clientExe, launchArgs)
            };

            if (!GameEntryFilter.IsExcluded(game))
                results.Add(game);
        }

        return results;
    }

    public IEnumerable<Action> GetInstallLaunchAttempts(UnifiedGame game)
    {
        if (game.Platform != Platform.Riot || game.IsInstalled)
            yield break;

        if (RiotLauncherClient.ResolveProductId(game) is null)
            yield break;

        yield return () => RiotLauncherClient.StartInstall(game);
        yield return () => RiotLauncherClient.StartLaunch(
            RiotLauncherClient.ResolveProductId(game)!,
            RiotLauncherClient.ResolvePatchline(game) ?? "live");
    }
}
