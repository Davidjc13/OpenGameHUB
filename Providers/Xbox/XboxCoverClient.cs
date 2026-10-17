using System.Collections.Concurrent;
using System.Text.Json;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Providers.Xbox;

internal sealed class XboxCoverClient
{
    private static readonly ConcurrentDictionary<string, string?> PosterByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    public async Task<IReadOnlyList<string>> FindCoverUrlsAsync(
        UnifiedGame game,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (game.Platform != Platform.GamePass)
            return [];

        if (!string.IsNullOrWhiteSpace(game.CatalogCoverUrl)
            && game.CatalogCoverUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var posterUrl = await FindPosterUrlAsync(game.PlatformGameId, cancellationToken);
        return string.IsNullOrWhiteSpace(posterUrl) ? [] : [posterUrl];
    }

    private async Task<string?> FindPosterUrlAsync(string? key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        if (PosterByKey.TryGetValue(key, out var cached))
            return cached;

        var posterUrl = await LookupPosterUrlAsync(key, cancellationToken);
        PosterByKey[key] = posterUrl;
        return posterUrl;
    }

    private async Task<string?> LookupPosterUrlAsync(string key, CancellationToken cancellationToken)
    {
        var (market, language) = XboxCatalogMarket.Resolve();
        var posterUrl = await RequestPosterUrlAsync(key, market, language, cancellationToken);
        if (!string.IsNullOrWhiteSpace(posterUrl)
            || string.Equals(market, "US", StringComparison.OrdinalIgnoreCase))
        {
            return posterUrl;
        }

        return await RequestPosterUrlAsync(key, "US", "en-us", cancellationToken);
    }

    private async Task<string?> RequestPosterUrlAsync(
        string key,
        string market,
        string language,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(BuildLookupUrl(key, market, language), cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("Products", out var products)
                || products.ValueKind != JsonValueKind.Array
                || products.GetArrayLength() == 0)
            {
                return null;
            }

            return XboxCoverUrls.SelectPosterUrl(products[0]);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildLookupUrl(string key, string market, string language)
    {
        var marketQuery =
            $"market={Uri.EscapeDataString(market)}&languages={Uri.EscapeDataString(language)}";

        if (key.Contains('_', StringComparison.Ordinal))
        {
            return "https://displaycatalog.mp.microsoft.com/v7.0/products/lookup?" +
                   marketQuery +
                   $"&alternateId=PackageFamilyName&value={Uri.EscapeDataString(key)}";
        }

        return "https://displaycatalog.mp.microsoft.com/v7.0/products?" +
               $"bigIds={Uri.EscapeDataString(key)}&{marketQuery}";
    }
}
