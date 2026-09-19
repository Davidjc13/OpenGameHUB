using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Providers.Epic;
using OpenGameHUB.Providers.Rockstar;
using OpenGameHUB.Providers.Xbox;

namespace OpenGameHUB.Services.Games;

public enum GameInstallOutcome
{
    NotApplicable,
    InstallStarted,
    ManualInstallNotice,
    Failed
}

public sealed class GameInstallResult
{
    public GameInstallOutcome Outcome { get; init; } = GameInstallOutcome.NotApplicable;
    public bool CancelScheduledStatusClear { get; init; }
    public string? PreInstallStatusKey { get; init; }
    public string? StatusKey { get; init; }
    public object[]? StatusArgs { get; init; }
}

public sealed class GameInstallOrchestrator
{
    public async Task<GameInstallResult> TryStartInstallAsync(
        UnifiedGame game,
        CancellationToken cancellationToken = default)
    {
        if (game.IsInstalled)
            return new GameInstallResult { Outcome = GameInstallOutcome.NotApplicable };

        if (game.Platform == Platform.Epic
            && game.LaunchSpec.Kind == "protocol"
            && !string.IsNullOrWhiteSpace(game.LaunchSpec.Value)
            && LegendaryClient.IsEpicLauncherInstalled())
        {
            var preInstallStatusKey = EpicLauncherClient.IsEpicLauncherRunning()
                ? null
                : "EpicLauncherInstallWaiting";

            await EpicLauncherClient.StartInstallAsync(game.LaunchSpec.Value);
            return new GameInstallResult
            {
                Outcome = GameInstallOutcome.InstallStarted,
                CancelScheduledStatusClear = true,
                PreInstallStatusKey = preInstallStatusKey,
                StatusKey = "EpicLauncherInstallStarted",
                StatusArgs = [game.Title]
            };
        }

        if (game.Platform == Platform.Riot)
        {
            RiotLauncherClient.StartInstall(game);
            return new GameInstallResult
            {
                Outcome = GameInstallOutcome.InstallStarted,
                StatusKey = "RiotClientInstallStarted",
                StatusArgs = [game.Title]
            };
        }

        if (game.Platform == Platform.Rockstar)
        {
            RockstarLauncherClient.StartInstall(game.PlatformGameId
                ?? throw new InvalidOperationException(Loc.T("NoLaunchMethod")));
            return new GameInstallResult
            {
                Outcome = GameInstallOutcome.InstallStarted,
                StatusKey = "RockstarLauncherInstallStarted",
                StatusArgs = [game.Title]
            };
        }

        if (game.Platform == Platform.GamePass)
        {
            var pfn = game.PlatformGameId;
            var storeProductId = await XboxInstallClient.ResolveStoreProductIdAsync(pfn);
            cancellationToken.ThrowIfCancellationRequested();
            XboxInstallClient.StartInstall(storeProductId, pfn);
            return new GameInstallResult
            {
                Outcome = GameInstallOutcome.InstallStarted,
                StatusKey = "XboxInstallStarted",
                StatusArgs = [game.Title]
            };
        }

        if (game.Platform == Platform.Ea)
        {
            return new GameInstallResult { Outcome = GameInstallOutcome.ManualInstallNotice };
        }

        return new GameInstallResult { Outcome = GameInstallOutcome.NotApplicable };
    }
}
