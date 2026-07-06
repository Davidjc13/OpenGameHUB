using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Providers.Xbox;

namespace OpenGameHUB.Providers.Xbox;

public sealed class XboxCloudLibraryProvider : ICloudLibraryProvider
{
    private readonly XboxAccountClient _accountClient = new();
    private IReadOnlyList<XboxCatalogEntry> _cachedEntries = [];

    public Platform Platform => Platform.GamePass;

    public bool IsAvailable() => XboxCatalogReader.IsCloudLibraryAvailable();

    public async Task LoadLibraryAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable())
        {
            _cachedEntries = [];
            return;
        }

        _cachedEntries = await _accountClient.GetPcLibraryEntriesAsync(cancellationToken);
    }

    public IReadOnlyList<UnifiedGame> GetUninstalledLibraryGames(
        IReadOnlyList<UnifiedGame> currentGames,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsAvailable() || _cachedEntries.Count == 0)
            return [];

        var installedGamePass = currentGames
            .Where(game => game.Platform == Platform.GamePass)
            .ToList();

        var results = new List<UnifiedGame>();

        foreach (var entry in _cachedEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (installedGamePass.Any(game => XboxCatalogReader.MatchesInstalledGame(game, entry)))
                continue;

            var game = new UnifiedGame
            {
                Id = $"gamepass:catalog:{entry.Pfn.ToLowerInvariant()}",
                Platform = Platform.GamePass,
                PlatformGameId = entry.Pfn,
                Title = entry.Title,
                IsInstalled = false,
                PlaytimeMinutes = entry.PlaytimeMinutes ?? 0,
                LastPlayed = entry.LastPlayed,
                LaunchSpec = XboxCatalogReader.BuildInstallLaunchSpec(entry)
            };

            if (!GameEntryFilter.IsExcluded(game))
                results.Add(game);
        }

        return results;
    }

    public IEnumerable<Action> GetInstallLaunchAttempts(UnifiedGame game)
    {
        if (game.Platform != Platform.GamePass || game.IsInstalled)
            yield break;

        var pfn = game.PlatformGameId;
        string? storeProductId = null;
        if (game.LaunchSpec.Kind == "protocol"
            && game.LaunchSpec.Value.StartsWith("msxbox://game/?productId=", StringComparison.OrdinalIgnoreCase))
        {
            storeProductId = game.LaunchSpec.Value["msxbox://game/?productId=".Length..];
        }

        foreach (var attempt in XboxInstallClient.BuildInstallAttempts(storeProductId, pfn))
            yield return attempt;

        if (!string.IsNullOrWhiteSpace(game.LaunchSpec.Kind)
            && !string.IsNullOrWhiteSpace(game.LaunchSpec.Value)
            && game.LaunchSpec.Kind == "protocol")
        {
            yield return () => StartProtocol(game.LaunchSpec.Value);
        }
    }

    private static void StartProtocol(string url)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };

        if (System.Diagnostics.Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("ProcessStartFailed", url));
    }
}
