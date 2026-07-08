using System.Diagnostics;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Providers.Gog;

namespace OpenGameHUB.Providers.Gog;

public sealed class GogCloudLibraryProvider : ICloudLibraryProvider
{
    public Platform Platform => Platform.Gog;

    public bool IsAvailable() => GogCatalogReader.IsCloudLibraryAvailable();

    public IReadOnlyList<UnifiedGame> GetUninstalledLibraryGames(
        IReadOnlyList<UnifiedGame> currentGames,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsAvailable())
            return [];

        var installedGogGames = currentGames
            .Where(g => g.Platform == Platform.Gog)
            .ToList();

        var clientExe = GogCatalogReader.FindGalaxyClientExecutable();
        var results = new List<UnifiedGame>();

        IReadOnlyList<GogCatalogEntry> entries;
        try
        {
            entries = GogCatalogReader.ReadLibraryEntries();
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(GogCloudLibraryProvider),
                operation: "GetUninstalledLibraryGames.ReadLibraryEntries",
                exception: ex,
                platform: Platform.Gog);
            return [];
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (installedGogGames.Any(game => GogCatalogReader.MatchesInstalledGame(game, entry)))
                continue;

            if (!string.IsNullOrWhiteSpace(entry.InstallPath) && Directory.Exists(entry.InstallPath))
                continue;

            var protocolUrl = GogCatalogReader.BuildInstallProtocolUrl(entry.ReleaseKey);
            var launchArgs = GogCatalogReader.BuildLaunchArguments(entry.GogId, install: true);
            var launchSpec = clientExe is not null
                ? LaunchSpec.LauncherArgs(clientExe, launchArgs)
                : LaunchSpec.Protocol(protocolUrl);

            var game = new UnifiedGame
            {
                Id = $"gog:catalog:{entry.GogId}@{entry.ReleaseKey}",
                Platform = Platform.Gog,
                PlatformGameId = entry.GogId.ToString(),
                Title = entry.Title,
                IsInstalled = false,
                LaunchSpec = launchSpec
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

        if (string.IsNullOrWhiteSpace(game.PlatformGameId))
            yield break;

        if (!long.TryParse(game.PlatformGameId, out var gogId))
            yield break;

        var releaseKey = TryGetReleaseKey(game.Id) ?? $"gog_{gogId}";
        yield return () => ProtocolLauncher.Start(GogCatalogReader.BuildInstallProtocolUrl(releaseKey));

        var clientExe = GogCatalogReader.FindGalaxyClientExecutable();
        if (clientExe is not null)
        {
            var installArgs = GogCatalogReader.BuildLaunchArguments(gogId, install: true);
            yield return () => StartLauncherArgs(clientExe, installArgs);
        }
    }

    private static string? TryGetReleaseKey(string id)
    {
        const string prefix = "gog:catalog:";
        if (!id.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var payload = id[prefix.Length..];
        var separator = payload.IndexOf('@');
        return separator >= 0 && separator < payload.Length - 1
            ? payload[(separator + 1)..]
            : null;
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

    private static void StartProcess(
        string fileName,
        string? arguments,
        string? workingDirectory,
        bool useShellExecute)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? string.Empty,
            WorkingDirectory = workingDirectory ?? string.Empty,
            UseShellExecute = useShellExecute
        };

        if (Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("ProcessStartFailed", fileName));
    }
}
