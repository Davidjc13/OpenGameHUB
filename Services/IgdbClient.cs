using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using OpenGameHUB.Models;

namespace OpenGameHUB.Services;

public sealed class IgdbClient
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(12);

    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private string? _accessToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;

    public bool IsConfigured(AppSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.IgdbClientId) &&
        !string.IsNullOrWhiteSpace(settings.IgdbClientSecret);

    public async Task<string?> FindCoverUrlAsync(
        UnifiedGame game,
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured(settings))
            return null;

        var token = await GetAccessTokenAsync(settings, cancellationToken);
        if (token is null)
            return null;

        var searchTitle = MetadataSearchHelper.NormalizeTitle(game.Title);
        var query = $"search \"{EscapeQuotes(searchTitle)}\"; fields name,cover.image_id; limit 1;";
        var results = await PostAsync<List<IgdbGameResult>>(token, settings.IgdbClientId, "games", query, cancellationToken);
        var imageId = results?.FirstOrDefault()?.Cover?.ImageId;
        return imageId is null ? null : $"https://images.igdb.com/igdb/image/upload/t_cover_big/{imageId}.jpg";
    }

    private async Task<string?> GetAccessTokenAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTime.UtcNow < _tokenExpiresAt)
            return _accessToken;

        var url =
            $"https://id.twitch.tv/oauth2/token?client_id={Uri.EscapeDataString(settings.IgdbClientId)}" +
            $"&client_secret={Uri.EscapeDataString(settings.IgdbClientSecret)}" +
            "&grant_type=client_credentials";

        var response = await _httpClient.PostAsync(url, null, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<TwitchTokenResponse>(cancellationToken);
        if (string.IsNullOrWhiteSpace(payload?.AccessToken))
            return null;

        _accessToken = payload.AccessToken;
        _tokenExpiresAt = DateTime.UtcNow.Add(TokenLifetime);
        return _accessToken;
    }

    private async Task<T?> PostAsync<T>(
        string token,
        string clientId,
        string endpoint,
        string body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.igdb.com/v4/{endpoint}")
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Client-ID", clientId);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return default;

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private static string EscapeQuotes(string value) => value.Replace("\"", "\\\"");

    private sealed class TwitchTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }

    private sealed class IgdbGameResult
    {
        [JsonPropertyName("cover")]
        public IgdbCover? Cover { get; set; }
    }

    private sealed class IgdbCover
    {
        [JsonPropertyName("image_id")]
        public string? ImageId { get; set; }
    }
}
