using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Services.Games;

namespace OpenGameHUB.Tests;

public sealed class GameInstallOrchestratorTests
{
    private readonly GameInstallOrchestrator _orchestrator = new();

    [Fact]
    public async Task TryStartInstallAsync_returns_not_applicable_when_installed()
    {
        var game = CreateGame(Platform.Epic, isInstalled: true);

        var result = await _orchestrator.TryStartInstallAsync(game);

        Assert.Equal(GameInstallOutcome.NotApplicable, result.Outcome);
    }

    [Fact]
    public async Task TryStartInstallAsync_returns_manual_notice_for_ea()
    {
        var game = CreateGame(Platform.Ea, isInstalled: false);

        var result = await _orchestrator.TryStartInstallAsync(game);

        Assert.Equal(GameInstallOutcome.ManualInstallNotice, result.Outcome);
    }

    [Fact]
    public async Task TryStartInstallAsync_returns_not_applicable_for_unknown_platform()
    {
        var game = CreateGame(Platform.Steam, isInstalled: false);

        var result = await _orchestrator.TryStartInstallAsync(game);

        Assert.Equal(GameInstallOutcome.NotApplicable, result.Outcome);
    }

    private static UnifiedGame CreateGame(Platform platform, bool isInstalled)
    {
        return new UnifiedGame
        {
            Id = $"{platform}:test",
            Platform = platform,
            PlatformGameId = "test-id",
            Title = "Test Game",
            IsInstalled = isInstalled,
            LaunchSpec = LaunchSpec.Protocol("com.epicgames.launcher://test")
        };
    }
}
