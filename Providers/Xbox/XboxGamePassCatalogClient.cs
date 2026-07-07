using System.Globalization;
using System.Text.Json;

namespace OpenGameHUB.Providers.Xbox;

internal sealed class XboxGamePassCatalogClient
{
    private const string PcGamePassSiglId = "fdd9e2a7-0fee-49f6-ad69-4354098401ff";
    private const int ProductBatchSize = 25;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(2)
    };

    public async Task<IReadOnlyList<XboxCatalogEntry>> GetInstallablePcGamesAsync(
        CancellationToken cancellationToken = default)
    {
        var (market, language) = XboxCatalogMarket.Resolve();
        var productIds = await GetPcGamePassProductIdsAsync(market, language, cancellationToken);
        if (productIds.Count == 0)
            return [];

        var entries = new List<XboxCatalogEntry>();
        foreach (var batch in productIds.Chunk(ProductBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var products = await GetProductsAsync(batch, market, language, cancellationToken);
            entries.AddRange(products);
        }

        return entries
            .GroupBy(entry => entry.StoreProductId ?? entry.Pfn, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
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
            return [];

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
            return [];

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
