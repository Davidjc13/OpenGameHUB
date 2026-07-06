using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Providers.Rockstar;

namespace OpenGameHUB.Services.Covers;

public sealed class RockstarCoverClient
{
    private readonly WikipediaCoverClient _wikipediaCoverClient = new();
    private readonly SteamStoreSearchClient _steamStoreSearchClient = new();

    public async Task<IReadOnlyList<string>> FindCoverUrlsAsync(
        UnifiedGame game,
        CancellationToken cancellationToken = default)
    {
        var urls = new List<string>();

        var officialUrl = RockstarCoverUrls.GetCoverUrl(game.PlatformGameId, game.Title);
        if (!string.IsNullOrWhiteSpace(officialUrl))
            urls.Add(officialUrl);

        urls.AddRange(RockstarCoverUrls.GetIgdbCoverUrls(game.PlatformGameId, game.Title));

        var wikipediaUrl = await _wikipediaCoverClient.FindCoverUrlAsync(game, cancellationToken);
        if (!string.IsNullOrWhiteSpace(wikipediaUrl))
            urls.Add(wikipediaUrl);

        urls.AddRange(await _steamStoreSearchClient.FindCoverUrlsAsync(game, cancellationToken));
        return urls;
    }
}
