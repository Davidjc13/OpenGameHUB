using System.Diagnostics;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Providers.Epic;

namespace OpenGameHUB.Providers.Epic;

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

        var installedIds = currentGames
            .Where(g => g.Platform == Platform.Epic)
            .Select(g => g.PlatformGameId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var installedTitles = currentGames
            .Where(g => g.Platform == Platform.Epic && g.IsInstalled)
            .Select(g => MetadataSearchHelper.NormalizeTitle(g.Title).ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        var results = new List<UnifiedGame>();

        foreach (var entry in LegendaryClient.ListCatalogEntries(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (installedIds.Contains(entry.AppName))
                continue;

            var titleKey = MetadataSearchHelper.NormalizeTitle(entry.AppTitle).ToLowerInvariant();
            if (installedTitles.Contains(titleKey))
                continue;

            var protocolUrl = entry.BuildInstallProtocolUrl()
                ?? $"com.epicgames.launcher://apps/{entry.AppName}?action=install";

            var coverUrl = EpicCatalogReader.GetCoverUrl(entry.AppName, entry.AppTitle);
            var game = new UnifiedGame
            {
                Id = $"epic:legendary:{entry.AppName}",
                Platform = Platform.Epic,
                PlatformGameId = entry.AppName,
                Title = entry.AppTitle,
                IsInstalled = false,
                CatalogCoverUrl = coverUrl,
                LaunchSpec = LaunchSpec.Protocol(protocolUrl)
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

        if (string.IsNullOrWhiteSpace(game.PlatformGameId))
            yield break;

        var appName = game.PlatformGameId;
        yield return () => LegendaryClient.RunInstall(appName);
        yield return () => StartProtocol($"com.epicgames.launcher://apps/{appName}?action=install");

        var epicLauncher = LegendaryClient.FindEpicLauncherExecutable();
        if (epicLauncher is not null)
            yield return () => StartLauncherArgs(epicLauncher, $"com.epicgames.launcher://apps/{appName}?action=install");

        yield return () => LegendaryClient.RunLaunch(appName);
    }

    private static void StartProtocol(string url)
    {
        try
        {
            StartProcess(url, null, null, useShellExecute: true);
        }
        catch
        {
            StartProcess("cmd.exe", $"/c start \"\" \"{url}\"", null, useShellExecute: false);
        }
    }

    private static void StartLauncherArgs(string launcherExe, string protocolUrl)
    {
        var psi = new ProcessStartInfo
        {
            FileName = launcherExe,
            Arguments = protocolUrl,
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
