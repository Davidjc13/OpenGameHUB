using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using OpenGameHUB.Models;

namespace OpenGameHUB.Services;

public sealed class SteamGridDbClient
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    public bool IsConfigured(AppSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.SteamGridDbApiKey);

    public async Task<string?> FindCoverUrlAsync(
        UnifiedGame game,
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured(settings))
            return null;

        var searchTitle = MetadataSearchHelper.NormalizeTitle(game.Title);
        var gameId = await SearchGameIdAsync(searchTitle, settings.SteamGridDbApiKey, cancellationToken);
        if (gameId is null)
            return null;

        return await GetGridUrlAsync(gameId.Value, settings.SteamGridDbApiKey, cancellationToken);
    }

    private async Task<int?> SearchGameIdAsync(string title, string apiKey, CancellationToken cancellationToken)
    {
        var url = $"https://www.steamgriddb.com/api/v2/search/autocomplete/{Uri.EscapeDataString(title)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<SteamGridDbSearchResponse>(cancellationToken);
        return payload?.Data?.FirstOrDefault()?.Id;
    }

    private async Task<string?> GetGridUrlAsync(int gameId, string apiKey, CancellationToken cancellationToken)
    {
        var endpoints = new[]
        {
            $"https://www.steamgriddb.com/api/v2/grids/game/{gameId}/steam/600x900",
            $"https://www.steamgriddb.com/api/v2/grids/game/{gameId}/steam",
            $"https://www.steamgriddb.com/api/v2/grids/game/{gameId}"
        };

        foreach (var url in endpoints)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                continue;

            var payload = await response.Content.ReadFromJsonAsync<SteamGridDbGridsResponse>(cancellationToken);
            var imageUrl = payload?.Data?
                .Select(item => item.Url)
                .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));

            if (!string.IsNullOrWhiteSpace(imageUrl))
                return imageUrl;
        }

        return null;
    }

    private sealed class SteamGridDbSearchResponse
    {
        [JsonPropertyName("data")]
        public List<SteamGridDbGame>? Data { get; set; }
    }

    private sealed class SteamGridDbGame
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    private sealed class SteamGridDbGridsResponse
    {
        [JsonPropertyName("data")]
        public List<SteamGridDbGrid>? Data { get; set; }
    }

    private sealed class SteamGridDbGrid
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
