using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Providers.Ubisoft;

namespace OpenGameHUB.Providers.Ubisoft;

public sealed class UbisoftCloudLibraryProvider : ICloudLibraryProvider
{
    public Platform Platform => Platform.Ubisoft;

    public bool IsAvailable() =>
        UbisoftCatalogReader.IsLauncherInstalled()
        && UbisoftCatalogReader.FindConfigurationsCachePath() is not null;

    public IReadOnlyList<UnifiedGame> GetUninstalledLibraryGames(
        IReadOnlyList<UnifiedGame> currentGames,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var installedIds = currentGames
            .Where(g => g.Platform == Platform.Ubisoft)
            .Select(g => g.PlatformGameId)
            .ToHashSet(StringComparer.Ordinal);

        var entries = UbisoftCatalogReader.ReadLibraryEntries();
        var results = new List<UnifiedGame>();

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var gameId = entry.UplayId.ToString();
            if (installedIds.Contains(gameId))
                continue;

            var game = new UnifiedGame
            {
                Id = $"ubisoft:catalog:{entry.UplayId}",
                Platform = Platform.Ubisoft,
                PlatformGameId = gameId,
                Title = entry.Title,
                IsInstalled = false,
                CatalogCoverUrl = entry.ThumbImageUrl,
                LaunchSpec = LaunchSpec.Protocol($"uplay://install/{entry.UplayId}")
            };

            if (!GameEntryFilter.IsExcluded(game))
                results.Add(game);
        }

        return results;
    }

    public IEnumerable<Action> GetInstallLaunchAttempts(UnifiedGame game)
    {
        if (game.Platform != Platform.Ubisoft || game.IsInstalled)
            yield break;

        if (string.IsNullOrWhiteSpace(game.PlatformGameId))
            yield break;

        var installUrl = $"uplay://install/{game.PlatformGameId}";
        yield return () => ProtocolLauncher.Start(installUrl);

        var launcherExe = UbisoftCatalogReader.FindLauncherExecutable();
        if (launcherExe is not null)
            yield return () => StartLauncherArgs(launcherExe, installUrl);
    }

    private static void StartLauncherArgs(string launcherExe, string protocolUrl)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = launcherExe,
            Arguments = protocolUrl,
            WorkingDirectory = Path.GetDirectoryName(launcherExe) ?? string.Empty,
            UseShellExecute = false
        };

        if (System.Diagnostics.Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("ProcessStartFailed", launcherExe));
    }

    private static void StartProcess(
        string fileName,
        string? arguments,
        string? workingDirectory,
        bool useShellExecute)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? string.Empty,
            WorkingDirectory = workingDirectory ?? string.Empty,
            UseShellExecute = useShellExecute
        };

        if (System.Diagnostics.Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("ProcessStartFailed", fileName));
    }
}
