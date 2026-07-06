using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Providers.Ea;

internal sealed class EaCoverClient
{
    private readonly SteamStoreSearchClient _steamStoreSearchClient = new();

    public async Task<IReadOnlyList<string>> FindCoverUrlsAsync(
        UnifiedGame game,
        CancellationToken cancellationToken = default)
    {
        var urls = new List<string>();
        urls.AddRange(await _steamStoreSearchClient.FindCoverUrlsAsync(game, cancellationToken));

        if (TryGetBaseSlug(game.Id, out var slug))
        {
            var slugTitle = EaCatalogReader.SlugToTitle(slug);
            if (!string.Equals(slugTitle, game.Title, StringComparison.OrdinalIgnoreCase))
            {
                urls.AddRange(await _steamStoreSearchClient.FindCoverUrlsByTitleAsync(
                    slugTitle,
                    cancellationToken));
            }
        }

        return urls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryGetBaseSlug(string id, out string slug)
    {
        slug = string.Empty;
        const string prefix = "ea:catalog:";
        if (!id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var payload = id[prefix.Length..];
        var separator = payload.IndexOf('@');
        if (separator <= 0 || separator >= payload.Length - 1)
            return false;

        slug = payload[(separator + 1)..];
        return !string.IsNullOrWhiteSpace(slug);
    }
}
