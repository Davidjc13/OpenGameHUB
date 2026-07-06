using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
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

        var entries = EaCatalogReader.ReadLibraryEntries();
        var results = new List<UnifiedGame>();

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (installedEaGames.Any(installed => EaCatalogReader.MatchesInstalledGame(installed, entry)))
                continue;

            var installUrl = BuildInstallUrl(entry.SoftwareId);
            var game = new UnifiedGame
            {
                Id = $"ea:catalog:{entry.SoftwareId}@{entry.BaseSlug}",
                Platform = Platform.Ea,
                PlatformGameId = entry.SoftwareId,
                Title = entry.Title,
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
        if (game.Platform != Platform.Ea || game.IsInstalled)
            yield break;

        if (string.IsNullOrWhiteSpace(game.PlatformGameId))
            yield break;

        var originUrl = BuildInstallUrl(game.PlatformGameId);
        yield return () => StartProtocol(originUrl);

        if (TryGetEaCatalogSlug(game.Id, out var slug))
        {
            var slugUrl = $"origin2://game/launch/?offerIds={Uri.EscapeDataString(slug)}";
            yield return () => StartProtocol(slugUrl);
        }

        var link2EaUrl = $"link2ea://launchgame/contentids/{game.PlatformGameId}";
        yield return () => StartProtocol(link2EaUrl);

        var eaDesktop = EaCatalogReader.FindEaDesktopExecutable();
        if (eaDesktop is not null)
            yield return () => StartLauncherArgs(eaDesktop, originUrl);
    }

    private static string BuildInstallUrl(string softwareId) =>
        $"origin2://game/launch/?offerIds={Uri.EscapeDataString(softwareId)}";

    private static bool TryGetEaCatalogSlug(string id, out string slug)
    {
        slug = string.Empty;
        const string prefix = "ea:catalog:";
        if (!id.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var payload = id[prefix.Length..];
        var separator = payload.IndexOf('@');
        if (separator <= 0 || separator >= payload.Length - 1)
            return false;

        slug = payload[(separator + 1)..];
        return !string.IsNullOrWhiteSpace(slug);
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
