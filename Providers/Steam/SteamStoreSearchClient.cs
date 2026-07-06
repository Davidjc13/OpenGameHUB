using System.Net.Http.Json;
using System.Text.Json.Serialization;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Providers.Steam;

public sealed class SteamStoreSearchClient
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    public SteamStoreSearchClient()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("OpenGameHUB/1.0");
    }

    public async Task<IReadOnlyList<string>> FindCoverUrlsAsync(
        UnifiedGame game,
        CancellationToken cancellationToken = default) =>
        await FindCoverUrlsByTitleAsync(game.Title, cancellationToken);

    public async Task<IReadOnlyList<string>> FindCoverUrlsByTitleAsync(
        string title,
        CancellationToken cancellationToken = default)
    {
        var appId = await FindBestAppIdAsync(title, cancellationToken);
        if (appId is null)
            return [];

        return
        [
            $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg",
            $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg"
        ];
    }

    private async Task<int?> FindBestAppIdAsync(string title, CancellationToken cancellationToken)
    {
        var searchTitle = MetadataSearchHelper.NormalizeTitle(title);
        if (string.IsNullOrWhiteSpace(searchTitle))
            return null;

        var url =
            $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(searchTitle)}&l=english&cc=US";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<SteamStoreSearchResponse>(cancellationToken);
        if (payload?.Items is null || payload.Items.Count == 0)
            return null;

        return PickBestAppId(searchTitle, payload.Items);
    }

    private static int? PickBestAppId(string searchTitle, IReadOnlyList<SteamStoreSearchItem> items)
    {
        var normalizedSearch = NormalizeForMatch(searchTitle);

        foreach (var item in items)
        {
            if (NormalizeForMatch(item.Name) == normalizedSearch)
                return item.Id;
        }

        foreach (var item in items)
        {
            var normalizedName = NormalizeForMatch(item.Name);
            if (normalizedName.StartsWith(normalizedSearch, StringComparison.Ordinal))
                return item.Id;
        }

        return null;
    }

    private static string NormalizeForMatch(string value)
    {
        var normalized = MetadataSearchHelper.NormalizeTitle(value).ToLowerInvariant();
        foreach (var ch in new[] { ':', '-', '–', '—', '|', '(', ')', '[', ']', '.', ',', '!', '?', '\'', '"', '™', '®' })
            normalized = normalized.Replace(ch, ' ');

        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed class SteamStoreSearchResponse
    {
        [JsonPropertyName("items")]
        public List<SteamStoreSearchItem>? Items { get; set; }
    }

    private sealed class SteamStoreSearchItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
