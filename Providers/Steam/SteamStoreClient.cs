using System.Net.Http.Json;
using System.Text.Json.Serialization;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Providers.Steam;

public sealed class SteamStoreClient
{
    private const int BatchSize = 20;

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public async Task<IReadOnlyDictionary<int, string>> GetAppNamesAsync(
        IEnumerable<int> appIds,
        CancellationToken cancellationToken = default)
    {
        var names = new Dictionary<int, string>();
        foreach (var chunk in appIds.Distinct().Chunk(BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ids = string.Join(',', chunk);
            var url =
                $"https://store.steampowered.com/api/appdetails?appids={ids}&l=english&filters=basic";

            try
            {
                var response = await _httpClient.GetFromJsonAsync<Dictionary<string, SteamStoreAppResponse>>(
                    url,
                    cancellationToken);

                if (response is null)
                    continue;

                foreach (var (key, value) in response)
                {
                    if (!int.TryParse(key, out var appId))
                        continue;

                    if (value.Success && !string.IsNullOrWhiteSpace(value.Data?.Name))
                        names[appId] = value.Data.Name.Trim();
                }
            }
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(SteamStoreClient),
                    operation: "GetAppNamesAsync",
                    exception: ex,
                    platform: Platform.Steam,
                    details: $"batchSize={chunk.Length}");
            }

            if (chunk.Length == BatchSize)
                await Task.Delay(250, cancellationToken);
        }

        return names;
    }

    public async Task<IReadOnlyList<string>> GetCoverUrlsAsync(
        int appId,
        CancellationToken cancellationToken = default)
    {
        if (appId <= 0)
            return [];

        var url =
            $"https://store.steampowered.com/api/appdetails?appids={appId}&l=english&filters=basic";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<Dictionary<string, SteamStoreAppResponse>>(
                url,
                cancellationToken);

            if (response is null
                || !response.TryGetValue(appId.ToString(), out var entry)
                || !entry.Success
                || entry.Data is null)
            {
                return [];
            }

            return CollectStoreImageUrls(entry.Data);
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(SteamStoreClient),
                operation: "GetCoverUrlsAsync",
                exception: ex,
                platform: Platform.Steam,
                details: $"appId={appId}");
            return [];
        }
    }

    private static IReadOnlyList<string> CollectStoreImageUrls(SteamStoreAppData data)
    {
        var urls = new List<string>();
        AddUrl(urls, data.HeaderImage);
        AddUrl(urls, data.BackgroundRaw);
        AddUrl(urls, data.CapsuleImage);
        return urls;
    }

    private static void AddUrl(List<string> urls, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var normalized = NormalizeStoreAssetUrl(value);
        if (!urls.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            urls.Add(normalized);
    }

    internal static string NormalizeStoreAssetUrl(string url)
    {
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url["http://".Length..];

        var queryIndex = url.IndexOf('?', StringComparison.Ordinal);
        return queryIndex >= 0 ? url[..queryIndex] : url;
    }

    private sealed class SteamStoreAppResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public SteamStoreAppData? Data { get; set; }
    }

    private sealed class SteamStoreAppData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("header_image")]
        public string? HeaderImage { get; set; }

        [JsonPropertyName("background_raw")]
        public string? BackgroundRaw { get; set; }

        [JsonPropertyName("capsule_image")]
        public string? CapsuleImage { get; set; }
    }
}
