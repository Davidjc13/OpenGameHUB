using System.Globalization;
using System.Text.Json;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Providers.Xbox;

internal sealed class XboxGamePassCatalogClient
{
    private const string PcGamePassSiglId = "fdd9e2a7-0fee-49f6-ad69-4354098401ff";
    private const int ProductBatchSize = 25;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public XboxGamePassCatalogClient()
        : this(CreateHttpClient())
    {
    }

    internal XboxGamePassCatalogClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("OpenGameHUB/1.0");
        return httpClient;
    }

    public async Task<IReadOnlyList<XboxCatalogEntry>> GetInstallablePcGamesAsync(
        CancellationToken cancellationToken = default)
    {
        var (market, language) = XboxCatalogMarket.Resolve();
        return await GetInstallablePcGamesAsync(market, language, cancellationToken);
    }

    internal async Task<IReadOnlyList<XboxCatalogEntry>> GetInstallablePcGamesAsync(
        string market,
        string language,
        CancellationToken cancellationToken = default)
    {
        var (resolvedMarket, resolvedLanguage, productIds) =
            await ResolveProductIdsWithFallbackAsync(market, language, cancellationToken);

        if (productIds.Count == 0)
            return [];

        var entries = new List<XboxCatalogEntry>();
        foreach (var batch in productIds.Chunk(ProductBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var products = await GetProductsAsync(batch, resolvedMarket, resolvedLanguage, cancellationToken);
            entries.AddRange(products);
        }

        return entries
            .GroupBy(entry => entry.StoreProductId ?? entry.Pfn, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    internal async Task<(string Market, string Language, IReadOnlyList<string> ProductIds)>
        ResolveProductIdsWithFallbackAsync(
            string market,
            string language,
            CancellationToken cancellationToken = default)
    {
        var productIds = await GetPcGamePassProductIdsAsync(market, language, cancellationToken);
        if (productIds.Count == 0
            && !string.Equals(market, "US", StringComparison.OrdinalIgnoreCase))
        {
            var usProductIds = await GetPcGamePassProductIdsAsync("US", "en-us", cancellationToken);
            if (usProductIds.Count > 0)
                return ("US", "en-us", usProductIds);
        }

        return (market, language, productIds);
    }

    internal static async Task<IReadOnlyList<string>> GetPcGamePassProductIdsAsync(
        HttpClient httpClient,
        string market,
        string language,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"https://catalog.gamepass.com/sigls/v2?id={PcGamePassSiglId}&language={Uri.EscapeDataString(language)}&market={Uri.EscapeDataString(market)}";

        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            AppDiagnostics.ReportError(
                area: nameof(XboxGamePassCatalogClient),
                operation: "GetPcGamePassProductIdsAsync",
                exception: new HttpRequestException(
                    $"SIGL request failed with status {(int)response.StatusCode} ({response.StatusCode})."),
                platform: Platform.GamePass,
                details: $"market={market};language={language}");
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        var ids = new List<string>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.TryGetProperty("id", out var idElement)
                && !string.IsNullOrWhiteSpace(idElement.GetString()))
            {
                ids.Add(idElement.GetString()!);
            }
        }

        return ids;
    }

    private async Task<IReadOnlyList<string>> GetPcGamePassProductIdsAsync(
        string market,
        string language,
        CancellationToken cancellationToken) =>
        await GetPcGamePassProductIdsAsync(_httpClient, market, language, cancellationToken);

    private async Task<IReadOnlyList<XboxCatalogEntry>> GetProductsAsync(
        IReadOnlyList<string> productIds,
        string market,
        string language,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
            return [];

        var url =
            "https://displaycatalog.mp.microsoft.com/v7.0/products?" +
            $"bigIds={Uri.EscapeDataString(string.Join(',', productIds))}" +
            $"&market={Uri.EscapeDataString(market)}" +
            $"&languages={Uri.EscapeDataString(language)}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            AppDiagnostics.ReportError(
                area: nameof(XboxGamePassCatalogClient),
                operation: "GetProductsAsync",
                exception: new HttpRequestException(
                    $"Display catalog request failed with status {(int)response.StatusCode} ({response.StatusCode})."),
                platform: Platform.GamePass,
                details: $"market={market};language={language};batchSize={productIds.Count}");
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("Products", out var products)
            || products.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var entries = new List<XboxCatalogEntry>();
        foreach (var product in products.EnumerateArray())
        {
            if (!XboxGamePassProductFilter.IsInstallableOnPc(product))
                continue;

            var entry = MapProduct(product);
            if (entry is not null)
                entries.Add(entry);
        }

        return entries;
    }

    internal static XboxCatalogEntry? MapProduct(JsonElement product)
    {
        var storeProductId = product.TryGetProperty("ProductId", out var productIdElement)
            ? productIdElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(storeProductId))
            return null;

        var title = ReadTitle(product);
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var pfn = ReadPackageFamilyName(product);
        return new XboxCatalogEntry
        {
            Pfn = pfn ?? string.Empty,
            Title = NormalizeTitle(title),
            StoreProductId = storeProductId
        };
    }

    private static string? ReadPackageFamilyName(JsonElement product)
    {
        if (product.TryGetProperty("Properties", out var properties)
            && properties.TryGetProperty("PackageFamilyName", out var pfnElement))
        {
            var pfn = pfnElement.GetString();
            if (!string.IsNullOrWhiteSpace(pfn))
                return pfn.Trim();
        }

        if (!product.TryGetProperty("DisplaySkuAvailabilities", out var skuAvailabilities))
            return null;

        foreach (var skuAvailability in skuAvailabilities.EnumerateArray())
        {
            if (!skuAvailability.TryGetProperty("Sku", out var sku)
                || !sku.TryGetProperty("Properties", out var skuProperties)
                || !skuProperties.TryGetProperty("Packages", out var packages)
                || packages.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var package in packages.EnumerateArray())
            {
                if (package.TryGetProperty("PackageFamilyName", out var packagePfn)
                    && !string.IsNullOrWhiteSpace(packagePfn.GetString()))
                {
                    return packagePfn.GetString()!.Trim();
                }
            }
        }

        return null;
    }

    private static string ReadTitle(JsonElement product)
    {
        if (!product.TryGetProperty("LocalizedProperties", out var localized)
            || localized.ValueKind != JsonValueKind.Array
            || localized.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        return localized[0].TryGetProperty("ProductTitle", out var title)
            ? title.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string NormalizeTitle(string title) =>
        title
            .Replace("(PC)", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("(Windows)", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("(BATTLEMODE)", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("for Windows 10", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("- Windows 10", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
}
