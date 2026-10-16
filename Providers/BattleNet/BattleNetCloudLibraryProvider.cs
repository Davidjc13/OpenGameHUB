using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Providers.BattleNet;

public sealed class BattleNetCloudLibraryProvider : ICloudLibraryProvider
{
    public Platform Platform => Platform.BattleNet;

    public bool IsAvailable() => BattleNetCatalogReader.IsCloudLibraryAvailable();

    public IReadOnlyList<UnifiedGame> GetUninstalledLibraryGames(
        IReadOnlyList<UnifiedGame> currentGames,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsAvailable())
            return [];

        var installedBattleNetGames = currentGames
            .Where(g => g.Platform == Platform.BattleNet)
            .ToList();

        var launcherExe = BattleNetCatalogReader.FindLauncherExecutable();
        if (launcherExe is null)
            return [];

        var results = new List<UnifiedGame>();

        IReadOnlyList<BattleNetCatalogEntry> entries;
        try
        {
            entries = BattleNetCatalogReader.ReadMergedCloudEntries();
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(BattleNetCloudLibraryProvider),
                operation: "GetUninstalledLibraryGames.ReadMergedCloudEntries",
                exception: ex,
                platform: Platform.BattleNet);
            return [];
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (installedBattleNetGames.Any(game => BattleNetCatalogReader.MatchesInstalledGame(game, entry)))
                continue;

            if (!entry.Playable && !entry.Installed)
                continue;

            var launchArgs = BattleNetCatalogReader.BuildLaunchArguments(entry.ProductId);
            var game = new UnifiedGame
            {
                Id = $"battlenet:catalog:{entry.ProductId}",
                Platform = Platform.BattleNet,
                PlatformGameId = entry.ProductId,
                Title = entry.Title,
                IsInstalled = entry.Installed,
                InstallPath = entry.Installed ? entry.InstallPath : null,
                LaunchSpec = LaunchSpec.LauncherArgs(launcherExe, launchArgs)
            };

            if (!GameEntryFilter.IsExcluded(game))
                results.Add(game);
        }

        return results;
    }

    public IEnumerable<Action> GetInstallLaunchAttempts(UnifiedGame game)
    {
        if (game.Platform != Platform.BattleNet || game.IsInstalled)
            yield break;

        if (string.IsNullOrWhiteSpace(game.PlatformGameId))
            yield break;

        yield return () => BattleNetLauncherClient.StartInstall(game.PlatformGameId);
        yield return () => BattleNetLauncherClient.StartLaunch(game.PlatformGameId);
    }
}
