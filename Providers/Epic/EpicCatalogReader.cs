using System.Collections.Concurrent;
using System.Text.Json;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Providers.Epic;

internal sealed record EpicCatalogAsset(string AppName, string Title, string CoverUrl);

internal static class EpicCatalogReader
{
    private static readonly ConcurrentDictionary<string, EpicCatalogAsset> AssetsByAppName =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, string> CoverUrlByTitle =
        new(StringComparer.OrdinalIgnoreCase);

    private static DateTime _cacheBuiltAt = DateTime.MinValue;
    private static string? _cachedMetadataDirectory;

    public static void EnrichCatalogCoverUrls(IReadOnlyList<UnifiedGame> games)
    {
        EnsureCache();

        foreach (var game in games)
        {
            if (game.Platform != Platform.Epic || !string.IsNullOrWhiteSpace(game.CoverPath))
                continue;

            if (!string.IsNullOrWhiteSpace(game.CatalogCoverUrl))
                continue;

            var coverUrl = ResolveCoverUrl(game.PlatformGameId, game.Title);
            if (!string.IsNullOrWhiteSpace(coverUrl))
                game.CatalogCoverUrl = coverUrl;
        }
    }

    public static string? GetCoverUrl(string? appName, string? title = null) =>
        ResolveCoverUrl(appName, title);

    private static string? ResolveCoverUrl(string? appName, string? title)
    {
        EnsureCache();

        if (!string.IsNullOrWhiteSpace(appName)
            && AssetsByAppName.TryGetValue(appName.Trim(), out var asset))
        {
            return asset.CoverUrl;
        }

        if (string.IsNullOrWhiteSpace(title))
            return null;

        var titleKey = MetadataSearchHelper.NormalizeTitle(title).ToLowerInvariant();
        return CoverUrlByTitle.TryGetValue(titleKey, out var coverUrl) ? coverUrl : null;
    }

    private static void EnsureCache()
    {
        var metadataDirectory = LegendaryClient.GetMetadataDirectory();
        if (!Directory.Exists(metadataDirectory))
        {
            ClearCache(metadataDirectory);
            return;
        }

        var latestWrite = Directory
            .EnumerateFiles(metadataDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path =>
            {
                try
                {
                    return File.GetLastWriteTimeUtc(path);
                }
                catch
                {
                    return DateTime.MinValue;
                }
            })
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();

        if (string.Equals(_cachedMetadataDirectory, metadataDirectory, StringComparison.OrdinalIgnoreCase)
            && latestWrite <= _cacheBuiltAt
            && !AssetsByAppName.IsEmpty)
        {
            return;
        }

        RebuildCache(metadataDirectory, latestWrite);
    }

    private static void RebuildCache(string metadataDirectory, DateTime latestWrite)
    {
        AssetsByAppName.Clear();
        CoverUrlByTitle.Clear();

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(metadataDirectory, "*.json", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(EpicCatalogReader),
                operation: "RebuildCache.EnumerateFiles",
                exception: ex,
                platform: Platform.Epic,
                details: metadataDirectory);
            ClearCache(metadataDirectory);
            return;
        }

        foreach (var file in files)
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                TryRegisterAsset(document.RootElement);
            }
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(EpicCatalogReader),
                    operation: "RebuildCache.ParseMetadataFile",
                    exception: ex,
                    platform: Platform.Epic,
                    details: Path.GetFileName(file));
            }
        }

        _cachedMetadataDirectory = metadataDirectory;
        _cacheBuiltAt = latestWrite;
    }

    private static void TryRegisterAsset(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return;

        var appName = ReadString(root, "app_name");
        if (string.IsNullOrWhiteSpace(appName))
            return;

        var title = ReadString(root, "app_title") ?? appName;
        var coverUrl = ExtractCoverUrl(root);
        if (string.IsNullOrWhiteSpace(coverUrl))
            return;

        var asset = new EpicCatalogAsset(appName.Trim(), title.Trim(), coverUrl);
        AssetsByAppName[asset.AppName] = asset;

        var titleKey = MetadataSearchHelper.NormalizeTitle(asset.Title).ToLowerInvariant();
        CoverUrlByTitle.TryAdd(titleKey, coverUrl);
    }

    private static string? ExtractCoverUrl(JsonElement root)
    {
        if (root.TryGetProperty("metadata", out var metadata)
            && metadata.ValueKind == JsonValueKind.Object
            && metadata.TryGetProperty("keyImages", out var metadataImages))
        {
            var cover = EpicKeyImageHelper.PickBestCoverUrl(metadataImages);
            if (!string.IsNullOrWhiteSpace(cover))
                return cover;
        }

        if (root.TryGetProperty("keyImages", out var rootImages))
        {
            var cover = EpicKeyImageHelper.PickBestCoverUrl(rootImages);
            if (!string.IsNullOrWhiteSpace(cover))
                return cover;
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;

    private static void ClearCache(string metadataDirectory)
    {
        AssetsByAppName.Clear();
        CoverUrlByTitle.Clear();
        _cachedMetadataDirectory = metadataDirectory;
        _cacheBuiltAt = DateTime.UtcNow;
    }
}
