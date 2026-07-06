using OpenGameHUB.Models;
using OpenGameHUB.Services.Gog;

namespace OpenGameHUB.Services.LibraryProviders;

public sealed class GogCloudLibraryProvider : ICloudLibraryProvider
{
    public Platform Platform => Platform.Gog;

    public bool IsAvailable() => GogCatalogReader.IsCloudLibraryAvailable();

    public IReadOnlyList<UnifiedGame> GetUninstalledLibraryGames(
        IReadOnlyList<UnifiedGame> currentGames,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var installedGogGames = currentGames
            .Where(g => g.Platform == Platform.Gog && g.IsInstalled)
            .ToList();

        var entries = GogCatalogReader.ReadLibraryEntries();
        var results = new List<UnifiedGame>();

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (installedGogGames.Any(installed => GogCatalogReader.MatchesInstalledGame(installed, entry)))
                continue;

            var installUrl = $"goggalaxy://openGameView/{entry.ReleaseKey}";
            var game = new UnifiedGame
            {
                Id = $"gog:catalog:{entry.ReleaseKey}",
                Platform = Platform.Gog,
                PlatformGameId = entry.ProductId,
                Title = entry.Title,
                IsInstalled = false,
                CatalogCoverUrl = entry.CoverUrl,
                LaunchSpec = LaunchSpec.Protocol(installUrl)
            };

            if (!GameEntryFilter.IsExcluded(game))
                results.Add(game);
        }

        return results;
    }

    public IEnumerable<Action> GetInstallLaunchAttempts(UnifiedGame game)
    {
        if (game.Platform != Platform.Gog || game.IsInstalled)
            yield break;

        var releaseKey = TryGetReleaseKey(game);
        if (string.IsNullOrWhiteSpace(releaseKey))
            yield break;

        yield return () => GogLauncherClient.StartInstall(releaseKey, game.PlatformGameId);

        var launcherExe = GogCatalogReader.FindLauncherExecutable();
        if (launcherExe is not null)
        {
            var installUrl = $"goggalaxy://openGameView/{releaseKey}";
            yield return () => StartLauncherArgs(launcherExe, installUrl);
        }
    }

    private static string? TryGetReleaseKey(UnifiedGame game)
    {
        const string prefix = "gog:catalog:";
        if (game.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return game.Id[prefix.Length..];

        if (!string.IsNullOrWhiteSpace(game.PlatformGameId))
            return $"gog_{game.PlatformGameId}";

        return null;
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
}
