using System.Diagnostics;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Providers.Xbox;

namespace OpenGameHUB.Providers.Xbox;

public sealed class XboxCloudLibraryProvider : ICloudLibraryProvider
{
    private readonly Func<CancellationToken, Task<IReadOnlyList<XboxCatalogEntry>>> _getCatalogAsync;
    private readonly Func<CancellationToken, Task<IReadOnlyList<XboxCatalogEntry>>> _getHistoryAsync;
    private readonly Func<bool> _isAvailable;
    private IReadOnlyList<XboxCatalogEntry> _cachedEntries = [];

    public XboxCloudLibraryProvider()
        : this(null, null, null)
    {
    }

    internal XboxCloudLibraryProvider(
        Func<CancellationToken, Task<IReadOnlyList<XboxCatalogEntry>>>? getCatalogAsync,
        Func<CancellationToken, Task<IReadOnlyList<XboxCatalogEntry>>>? getHistoryAsync,
        Func<bool>? isAvailable)
    {
        var catalogClient = new XboxGamePassCatalogClient();
        var accountClient = new XboxAccountClient();
        _getCatalogAsync = getCatalogAsync ?? (ct => catalogClient.GetInstallablePcGamesAsync(ct));
        _getHistoryAsync = getHistoryAsync ?? (ct => accountClient.GetPcLibraryEntriesAsync(ct));
        _isAvailable = isAvailable ?? XboxCatalogReader.IsCloudLibraryAvailable;
    }

    public Platform Platform => Platform.GamePass;

    public bool IsAvailable() => _isAvailable();

    public async Task LoadLibraryAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable())
        {
            _cachedEntries = [];
            return;
        }

        IReadOnlyList<XboxCatalogEntry> catalog;
        try
        {
            catalog = await _getCatalogAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(XboxCloudLibraryProvider),
                operation: "LoadLibraryAsync.GetInstallablePcGames",
                exception: ex,
                platform: Platform.GamePass);
            _cachedEntries = [];
            return;
        }

        if (catalog.Count == 0)
        {
            Trace.TraceWarning(
                "[{0}] operation=LoadLibraryAsync catalogCount=0 platform={1}",
                nameof(XboxCloudLibraryProvider),
                Platform.GamePass);
            _cachedEntries = [];
            return;
        }

        try
        {
            var history = await _getHistoryAsync(cancellationToken);
            _cachedEntries = XboxCatalogMerger.Merge(catalog, history, includeHistoryOnly: false);
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(XboxCloudLibraryProvider),
                operation: "LoadLibraryAsync.EnrichHistory",
                exception: ex,
                platform: Platform.GamePass,
                details: $"catalogCount={catalog.Count}");
            _cachedEntries = catalog;
        }

        Trace.TraceInformation(
            "[{0}] operation=LoadLibraryAsync catalogCount={1} mergedCount={2} platform={3}",
            nameof(XboxCloudLibraryProvider),
            catalog.Count,
            _cachedEntries.Count,
            Platform.GamePass);
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

            var catalogKey = !string.IsNullOrWhiteSpace(entry.Pfn)
                ? entry.Pfn
                : entry.StoreProductId ?? entry.Title;

            var game = new UnifiedGame
            {
                Id = $"gamepass:catalog:{catalogKey.ToLowerInvariant()}",
                Platform = Platform.GamePass,
                PlatformGameId = !string.IsNullOrWhiteSpace(entry.Pfn)
                    ? entry.Pfn
                    : entry.StoreProductId ?? string.Empty,
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
