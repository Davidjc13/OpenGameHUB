using System.Diagnostics;
using OpenGameHUB.Models;
using OpenGameHUB.Services.Riot;

namespace OpenGameHUB.Services.LibraryProviders;

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

        foreach (var entry in RiotCatalogReader.ReadLibraryEntries())
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

        if (string.IsNullOrWhiteSpace(game.PlatformGameId))
            yield break;

        var clientExe = RiotCatalogReader.FindClientServicesExecutable();
        if (clientExe is null)
            yield break;

        var patchline = TryGetPatchline(game.Id) ?? "live";
        var installArgs = RiotCatalogReader.BuildLaunchArguments(game.PlatformGameId, patchline, install: true);
        yield return () => StartLauncherArgs(clientExe, installArgs);

        var launchArgs = RiotCatalogReader.BuildLaunchArguments(game.PlatformGameId, patchline, install: false);
        yield return () => StartLauncherArgs(clientExe, launchArgs);

        if (game.LaunchSpec.Kind == "launcher-args")
            yield return () => StartFromLaunchSpec(game.LaunchSpec);
    }

    private static string? TryGetPatchline(string id)
    {
        const string prefix = "riot:catalog:";
        if (!id.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var payload = id[prefix.Length..];
        var separator = payload.IndexOf('@');
        return separator >= 0 && separator < payload.Length - 1
            ? payload[(separator + 1)..]
            : null;
    }

    private static void StartFromLaunchSpec(LaunchSpec launchSpec)
    {
        var parts = launchSpec.Value.Split('|', 2);
        if (parts.Length < 2)
            throw new InvalidOperationException(Loc.T("NoLaunchMethod"));

        StartLauncherArgs(parts[0], parts[1]);
    }

    private static void StartLauncherArgs(string launcherExe, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = launcherExe,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(launcherExe) ?? string.Empty,
            UseShellExecute = false
        };

        if (Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("ProcessStartFailed", launcherExe));
    }
}
