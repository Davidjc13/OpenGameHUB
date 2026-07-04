using System.Net.Http.Json;
using System.Text.Json.Serialization;
using OpenGameHUB.Models;

namespace OpenGameHUB.Services;

public sealed class WikipediaCoverClient
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(8) };

    public async Task<string?> FindCoverUrlAsync(
        UnifiedGame game,
        CancellationToken cancellationToken = default)
    {
        var title = MetadataSearchHelper.NormalizeTitle(game.Title);
        if (string.IsNullOrWhiteSpace(title))
            return null;

        foreach (var pageTitle in BuildPageTitleCandidates(title))
        {
            var thumbnail = await TryGetSummaryThumbnailAsync(pageTitle, cancellationToken);
            if (IsUsableImageUrl(thumbnail))
                return thumbnail;
        }

        return await SearchThumbnailAsync(title, cancellationToken);
    }

    private static bool IsUsableImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return !uri.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> BuildPageTitleCandidates(string title)
    {
        var underscored = title.Replace(' ', '_');
        yield return underscored;
        yield return $"{underscored}_(video_game)";
        yield return $"{underscored}_(game)";
    }

    private async Task<string?> TryGetSummaryThumbnailAsync(string pageTitle, CancellationToken cancellationToken)
    {
        var url = $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(pageTitle)}";

        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadFromJsonAsync<WikipediaSummaryResponse>(cancellationToken);
            return payload?.Thumbnail?.Source;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> SearchThumbnailAsync(string title, CancellationToken cancellationToken)
    {
        var query =
            $"https://en.wikipedia.org/w/api.php?action=query&generator=search" +
            $"&gsrsearch={Uri.EscapeDataString($"{title} video game")}" +
            "&gsrlimit=5&prop=pageimages&piprop=thumbnail&pithumbsize=600&format=json";

        using var response = await _httpClient.GetAsync(query, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<WikipediaSearchResponse>(cancellationToken);
        var pages = payload?.Query?.Pages;
        if (pages is null)
            return null;

        var normalizedTitle = MetadataSearchHelper.NormalizeTitle(title).ToLowerInvariant();

        foreach (var page in pages.Values.OrderBy(page => page.Index))
        {
            if (page.Title is null || page.Thumbnail?.Source is null)
                continue;

            if (page.Title.Contains(normalizedTitle, StringComparison.OrdinalIgnoreCase)
                || normalizedTitle.Contains(page.Title.Split('(')[0].Trim(), StringComparison.OrdinalIgnoreCase))
            {
                if (IsUsableImageUrl(page.Thumbnail.Source))
                    return page.Thumbnail.Source;
            }
        }

        return pages.Values
            .Select(page => page.Thumbnail?.Source)
            .FirstOrDefault(source => IsUsableImageUrl(source));
    }

    private sealed class WikipediaSummaryResponse
    {
        [JsonPropertyName("thumbnail")]
        public WikipediaThumbnail? Thumbnail { get; set; }
    }

    private sealed class WikipediaSearchResponse
    {
        [JsonPropertyName("query")]
        public WikipediaSearchQuery? Query { get; set; }
    }

    private sealed class WikipediaSearchQuery
    {
        [JsonPropertyName("pages")]
        public Dictionary<string, WikipediaSearchPage>? Pages { get; set; }
    }

    private sealed class WikipediaSearchPage
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("thumbnail")]
        public WikipediaThumbnail? Thumbnail { get; set; }
    }

    private sealed class WikipediaThumbnail
    {
        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }
}
