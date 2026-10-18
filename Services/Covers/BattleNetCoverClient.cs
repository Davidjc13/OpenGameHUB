using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Providers.BattleNet;

namespace OpenGameHUB.Services.Covers;

public sealed class BattleNetCoverClient
{
    public Task<IReadOnlyList<string>> FindCoverUrlsAsync(
        UnifiedGame game,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (game.Platform != Platform.BattleNet)
            return Task.FromResult<IReadOnlyList<string>>([]);

        return Task.FromResult(BattleNetCoverUrls.GetCoverUrls(game.PlatformGameId, game.Title));
    }
}
