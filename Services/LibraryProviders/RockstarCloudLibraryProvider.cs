using System.Diagnostics;
using OpenGameHUB.Models;
using OpenGameHUB.Services.Rockstar;

namespace OpenGameHUB.Services.LibraryProviders;

public sealed class RockstarCloudLibraryProvider : ICloudLibraryProvider
{
    public Platform Platform => Platform.Rockstar;

    public bool IsAvailable() => RockstarCatalogReader.IsCloudLibraryAvailable();

    public IReadOnlyList<UnifiedGame> GetUninstalledLibraryGames(
        IReadOnlyList<UnifiedGame> currentGames,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsAvailable())
            return [];

        var installedRockstarGames = currentGames
            .Where(g => g.Platform == Platform.Rockstar)
            .ToList();

        var launcherExe = RockstarCatalogReader.FindLauncherExecutable()
            ?? throw new InvalidOperationException(Loc.T("RockstarLauncherNotInstalled"));

        var results = new List<UnifiedGame>();

        foreach (var entry in RockstarCatalogReader.ReadLibraryEntries())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (installedRockstarGames.Any(game => RockstarCatalogReader.MatchesInstalledGame(game, entry)))
                continue;

            if (RockstarCatalogReader.IsTitleInstalled(entry.TitleId, entry.InstallPath))
                continue;

            var installArgs = RockstarCatalogReader.BuildInstallArguments(entry.TitleId);
            var game = new UnifiedGame
            {
                Id = $"rockstar:catalog:{entry.TitleId}",
                Platform = Platform.Rockstar,
                PlatformGameId = entry.TitleId,
                Title = entry.Title,
                IsInstalled = false,
                CatalogCoverUrl = RockstarCoverUrls.GetOfficialCoverUrl(entry.TitleId),
                LaunchSpec = LaunchSpec.LauncherArgs(launcherExe, installArgs)
            };

            if (!GameEntryFilter.IsExcluded(game))
                results.Add(game);
        }

        return results;
    }

    public IEnumerable<Action> GetInstallLaunchAttempts(UnifiedGame game)
    {
        if (game.Platform != Platform.Rockstar || game.IsInstalled)
            yield break;

        if (string.IsNullOrWhiteSpace(game.PlatformGameId))
            yield break;

        var launcherExe = RockstarCatalogReader.FindLauncherExecutable();
        if (launcherExe is null)
            yield break;

        yield return () => RockstarLauncherClient.StartInstall(game.PlatformGameId);

        if (game.LaunchSpec.Kind == "launcher-args")
            yield return () => StartFromLaunchSpec(game.LaunchSpec);
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
