using System.Text.RegularExpressions;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Services.Covers;

public sealed class RiotCoverClient
{
    private static readonly Regex LaunchProductRegex = new(
        @"--launch-product=(?<slug>[a-z0-9_]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly WikipediaCoverClient _wikipediaCoverClient = new();
    private readonly SteamStoreSearchClient _steamStoreSearchClient = new();

    public async Task<IReadOnlyList<string>> FindCoverUrlsAsync(
        UnifiedGame game,
        CancellationToken cancellationToken = default)
    {
        var urls = new List<string>();

        if (TryGetProductSlug(game, out var productSlug) &&
            ProductCoverIds.TryGetValue(productSlug, out var productCoverIds))
        {
            urls.AddRange(ToIgdbUrls(productCoverIds));
        }

        var title = MetadataSearchHelper.NormalizeTitle(game.Title);
        if (TitleCoverIds.TryGetValue(title, out var titleCoverIds))
            urls.AddRange(ToIgdbUrls(titleCoverIds));

        var wikipediaUrl = await _wikipediaCoverClient.FindCoverUrlAsync(game, cancellationToken);
        if (!string.IsNullOrWhiteSpace(wikipediaUrl))
            urls.Add(wikipediaUrl);

        urls.AddRange(await _steamStoreSearchClient.FindCoverUrlsAsync(game, cancellationToken));
        return urls;
    }

    private static bool TryGetProductSlug(UnifiedGame game, out string slug)
    {
        slug = string.Empty;
        if (game.LaunchSpec.Kind != "launcher-args")
            return false;

        var parts = game.LaunchSpec.Value.Split('|', 2);
        if (parts.Length < 2)
            return false;

        var match = LaunchProductRegex.Match(parts[1]);
        if (!match.Success)
            return false;

        slug = match.Groups["slug"].Value;
        return slug.Length > 0;
    }

    private static IEnumerable<string> ToIgdbUrls(IEnumerable<string> imageIds) =>
        imageIds.Select(id => $"https://images.igdb.com/igdb/image/upload/t_cover_big/{id}.jpg");

    private static readonly Dictionary<string, string[]> ProductCoverIds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["league_of_legends"] = ["co49wj", "cobpn7"],
            ["bacon"] = ["co3wnv"],
            ["valorant"] = ["cobtjo"],
            ["lion"] = ["cobwkh"],
            ["tft"] = ["co8jux"]
        };

    private static readonly Dictionary<string, string[]> TitleCoverIds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["League of Legends"] = ["co49wj", "cobpn7"],
            ["Legends of Runeterra"] = ["co3wnv"],
            ["VALORANT"] = ["cobtjo"],
            ["Valorant"] = ["cobtjo"],
            ["Teamfight Tactics"] = ["co8jux"],
            ["TFT"] = ["co8jux"],
            ["2XKO"] = ["cobwkh"]
        };
}
