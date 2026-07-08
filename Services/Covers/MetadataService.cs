using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Infrastructure.Database;
using OpenGameHUB.Providers.Ea;
using OpenGameHUB.Providers.Epic;

namespace OpenGameHUB.Services.Covers;

public sealed class MetadataService
{
    private readonly GameDatabase _database;
    private readonly SettingsService _settingsService;
    private readonly IgdbClient _igdbClient = new();
    private readonly SteamGridDbClient _steamGridDbClient = new();
    private readonly SteamStoreSearchClient _steamStoreSearchClient = new();
    private readonly WikipediaCoverClient _wikipediaCoverClient = new();
    private readonly RiotCoverClient _riotCoverClient = new();
    private readonly RockstarCoverClient _rockstarCoverClient = new();
    private readonly EpicCoverClient _epicCoverClient = new();
    private readonly EaCoverClient _eaCoverClient = new();
    private readonly HttpClient _httpClient = new();
    private readonly SafeImageDownloader _safeImageDownloader;

    public MetadataService(GameDatabase database, SettingsService settingsService)
    {
        _database = database;
        _settingsService = settingsService;
        Directory.CreateDirectory(CoverPathHelper.CacheDirectory);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("OpenGameHUB/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(8);
        _safeImageDownloader = new SafeImageDownloader(_httpClient);
    }

    public void ReconcileCachedCovers(IReadOnlyList<UnifiedGame> games)
    {
        foreach (var game in games)
            TryRegisterExistingCover(game);
    }

    public async Task EnrichCoversAsync(
        IReadOnlyList<UnifiedGame> games,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default,
        int maxCovers = int.MaxValue)
    {
        var settings = _settingsService.Current;
        ReconcileCachedCovers(games);

        var candidates = games
            .Where(ShouldDownloadCover)
            .Where(game => TryRegisterExistingCover(game) == false)
            .ToList();

        if (maxCovers > 0 && candidates.Count > maxCovers)
            candidates = candidates.Take(maxCovers).ToList();

        if (candidates.Count == 0)
            return;

        progress?.Report(Loc.T("DownloadingCovers"));

        for (var i = 0; i < candidates.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var game = candidates[i];

            if (i == 0 || i == candidates.Count - 1 || (i + 1) % 8 == 0)
                progress?.Report(Loc.T("DownloadingCoversProgress", i + 1, candidates.Count));

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(12));

                var coverPath = await DownloadCoverAsync(game, settings, timeoutCts.Token);
                if (coverPath is null)
                    continue;

                game.CoverPath = coverPath;
                _database.UpdateCoverPath(game.Id, coverPath);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Per-cover timeout; continue with the rest.
            }
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(MetadataService),
                    operation: "EnrichCoversAsync.DownloadCover",
                    exception: ex,
                    platform: game.Platform,
                    details: $"gameId={game.Id} | title={game.Title}");
            }
        }
    }

    public async Task<string?> EnsureCoverAsync(
        UnifiedGame game,
        CancellationToken cancellationToken = default)
    {
        if (game.HasCustomCover && TryRegisterExistingCover(game))
            return game.CoverPath;

        if (TryRegisterExistingCover(game))
            return game.CoverPath;

        if (game.HasCustomCover)
            return null;

        var path = await DownloadCoverAsync(game, _settingsService.Current, cancellationToken);
        if (path is null)
            return null;

        game.CoverPath = path;
        _database.UpdateCoverPath(game.Id, path);
        return path;
    }

    public bool TrySetCustomCover(UnifiedGame game, string sourceImagePath)
    {
        var cachePath = CoverPathHelper.GetCachePath(game.Id);
        if (!CoverImageProcessor.TryResizeToCacheFile(sourceImagePath, cachePath))
            return false;

        game.CoverPath = cachePath;
        game.HasCustomCover = true;
        _database.UpdateCoverPath(game.Id, cachePath);
        _database.SetCustomCover(game.Id, true);
        return true;
    }

    public async Task<string?> TryResetCustomCoverAsync(
        UnifiedGame game,
        CancellationToken cancellationToken = default)
    {
        game.HasCustomCover = false;
        _database.SetCustomCover(game.Id, false);

        var cachePath = CoverPathHelper.GetCachePath(game.Id);
        try
        {
            if (File.Exists(cachePath))
                File.Delete(cachePath);
        }
        catch
        {
            // optional
        }

        game.CoverPath = null;
        _database.UpdateCoverPath(game.Id, string.Empty);

        var path = await DownloadCoverAsync(game, _settingsService.Current, cancellationToken);
        if (path is null)
            return null;

        game.CoverPath = path;
        _database.UpdateCoverPath(game.Id, path);
        return path;
    }

    private bool TryRegisterExistingCover(UnifiedGame game)
    {
        var existingPath = CoverPathHelper.ResolveExistingPath(game);
        if (existingPath is null)
        {
            if (!string.IsNullOrWhiteSpace(game.CoverPath))
            {
                game.CoverPath = null;
                _database.UpdateCoverPath(game.Id, string.Empty);
            }

            return false;
        }

        if (!string.Equals(game.CoverPath, existingPath, StringComparison.OrdinalIgnoreCase))
        {
            game.CoverPath = existingPath;
            _database.UpdateCoverPath(game.Id, existingPath);
        }

        return true;
    }

    private async Task<string?> DownloadCoverAsync(
        UnifiedGame game,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        var cachePath = CoverPathHelper.GetCachePath(game.Id);

        var localCover = LocalCoverScanner.FindCover(game.InstallPath);
        if (localCover is not null && TryCopyValidatedImage(localCover, cachePath))
            return cachePath;

        if (TryCopyCatalogCoverFile(game.CatalogCoverUrl, cachePath))
            return cachePath;

        foreach (var url in await ResolveCoverUrlsAsync(game, settings, cancellationToken))
        {
            if (await TryDownloadAsync(url, cachePath, cancellationToken))
                return cachePath;
        }

        return null;
    }

    private static bool TryCopyCatalogCoverFile(string? catalogCoverUrl, string cachePath)
    {
        if (string.IsNullOrWhiteSpace(catalogCoverUrl)
            || catalogCoverUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return File.Exists(catalogCoverUrl) && TryCopyValidatedImage(catalogCoverUrl, cachePath);
    }

    private static bool TryCopyValidatedImage(string sourcePath, string destinationPath)
    {
        if (!SafeImageValidator.IsValidImageFile(sourcePath))
            return false;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: true);
            return SafeImageValidator.IsValidImageFile(destinationPath);
        }
        catch
        {
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);
            return false;
        }
    }

    private async Task<IReadOnlyList<string>> ResolveCoverUrlsAsync(
        UnifiedGame game,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        var urls = new List<string>();

        if (!string.IsNullOrWhiteSpace(game.CatalogCoverUrl)
            && game.CatalogCoverUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            urls.Add(NormalizeCoverUrl(game.CatalogCoverUrl));
        }

        if (game.Platform == Platform.Steam && int.TryParse(game.PlatformGameId, out var steamAppId))
        {
            urls.Add(SteamWebApiService.GetCoverUrl(steamAppId));
            urls.Add($"https://cdn.cloudflare.steamstatic.com/steam/apps/{steamAppId}/header.jpg");
        }
        else if (game.Platform == Platform.Epic)
        {
            urls.AddRange(await _epicCoverClient.FindCoverUrlsAsync(game, cancellationToken));
        }
        else if (game.Platform == Platform.Ea)
        {
            urls.AddRange(await _eaCoverClient.FindCoverUrlsAsync(game, cancellationToken));
        }
        else if (game.Platform == Platform.Riot)
        {
            urls.AddRange(await _riotCoverClient.FindCoverUrlsAsync(game, cancellationToken));
        }
        else if (game.Platform == Platform.Rockstar)
        {
            urls.AddRange(await _rockstarCoverClient.FindCoverUrlsAsync(game, cancellationToken));
        }
        else
        {
            urls.AddRange(await _steamStoreSearchClient.FindCoverUrlsAsync(game, cancellationToken));
        }

        if (_steamGridDbClient.IsConfigured(settings))
        {
            var steamGridUrl = await _steamGridDbClient.FindCoverUrlAsync(game, settings, cancellationToken);
            if (!string.IsNullOrWhiteSpace(steamGridUrl))
                urls.Add(steamGridUrl);
        }

        if (_igdbClient.IsConfigured(settings))
        {
            var igdbUrl = await _igdbClient.FindCoverUrlAsync(game, settings, cancellationToken);
            if (!string.IsNullOrWhiteSpace(igdbUrl))
                urls.Add(igdbUrl);
        }

        var wikipediaUrl = await _wikipediaCoverClient.FindCoverUrlAsync(game, cancellationToken);
        if (!string.IsNullOrWhiteSpace(wikipediaUrl))
            urls.Add(wikipediaUrl);

        return urls;
    }

    private static string NormalizeCoverUrl(string url)
    {
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return "https://" + url["http://".Length..];

        return url;
    }

    private static bool ShouldDownloadCover(UnifiedGame game) =>
        !game.HasCustomCover && !GameEntryFilter.IsExcluded(game);

    private Task<bool> TryDownloadAsync(string url, string cachePath, CancellationToken cancellationToken) =>
        _safeImageDownloader.DownloadAsync(url, cachePath, cancellationToken);
}
