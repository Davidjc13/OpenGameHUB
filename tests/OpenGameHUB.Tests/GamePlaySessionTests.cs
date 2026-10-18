using System.Diagnostics;
using OpenGameHUB.Services.Games;

namespace OpenGameHUB.Tests;

public sealed class GamePlaySessionTests
{
    [Fact]
    public void Describe_null_process_is_null() =>
        Assert.Null(GamePlaySession.Describe(null));

    [Fact]
    public void Describe_current_process_has_id_and_name()
    {
        var process = Process.GetCurrentProcess();
        var info = GamePlaySession.Describe(process);
        Assert.NotNull(info);
        Assert.Equal(process.Id, info.Value.Id);
        Assert.False(string.IsNullOrWhiteSpace(info.Value.ProcessName));
    }

    [Fact]
    public void Cancel_and_dispose_are_safe_before_start()
    {
        using var session = new GamePlaySession(new FakeCatalog(), new FakeSteam(), TimeSpan.FromMilliseconds(10));
        session.Cancel();
        session.Dispose();
        Assert.False(session.IsActive);
    }

    private sealed class FakeCatalog : IGameProcessCatalog
    {
        public IReadOnlyList<GameProcessInfo> Snapshot() => [];
    }

    private sealed class FakeSteam : ISteamRunningReader
    {
        public bool IsAppRunning(string appId) => false;
    }
}
