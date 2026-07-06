using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Providers.Epic;

public sealed class EpicCoverClient
{
    public Task<IReadOnlyList<string>> FindCoverUrlsAsync(
        UnifiedGame game,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (game.Platform != Platform.Epic)
            return Task.FromResult<IReadOnlyList<string>>([]);

        var coverUrl = EpicCatalogReader.GetCoverUrl(game.PlatformGameId, game.Title);
        if (string.IsNullOrWhiteSpace(coverUrl))
            return Task.FromResult<IReadOnlyList<string>>([]);

        return Task.FromResult<IReadOnlyList<string>>([coverUrl]);
    }
}
