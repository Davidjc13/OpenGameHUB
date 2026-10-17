using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Services.Games;

internal sealed class GameProcessMonitor
{
    public static readonly TimeSpan AcquireTimeout = TimeSpan.FromSeconds(90);
    public static readonly TimeSpan EmptyDebounce = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly UnifiedGame _game;
    private readonly GameProcessInfo? _launched;
    private readonly IGameProcessCatalog _catalog;
    private readonly ISteamRunningReader _steam;
    private readonly Func<DateTime> _utcNow;
    private readonly DateTime _startedAt;
    private bool _seenGame;
    private DateTime _lastSeenAt;

    public GameProcessMonitor(
        UnifiedGame game,
        GameProcessInfo? launchedProcess,
        IGameProcessCatalog catalog,
        ISteamRunningReader steamRunning,
        Func<DateTime>? utcNow = null)
    {
        _game = game;
        _launched = launchedProcess;
        _catalog = catalog;
        _steam = steamRunning;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _startedAt = _utcNow();
    }

    public GameMonitorPhase Evaluate()
    {
        var now = _utcNow();
        if (IsGameRunning())
        {
            _seenGame = true;
            _lastSeenAt = now;
            return GameMonitorPhase.Playing;
        }

        if (!_seenGame)
        {
            return now - _startedAt >= AcquireTimeout
                ? GameMonitorPhase.Ended
                : GameMonitorPhase.Acquiring;
        }

        return now - _lastSeenAt >= EmptyDebounce
            ? GameMonitorPhase.Ended
            : GameMonitorPhase.Playing;
    }

    public bool IsGameRunning()
    {
        if (IsLaunchedGameStillRunning())
            return true;

        if (_game.Platform == Platform.Steam && _steam.IsAppRunning(_game.PlatformGameId))
            return true;

        if (string.IsNullOrWhiteSpace(_game.InstallPath))
            return false;

        foreach (var process in _catalog.Snapshot())
        {
            if (GameProcessMatcher.IsGameProcess(process, _game.InstallPath))
                return true;
        }

        return false;
    }

    private bool IsLaunchedGameStillRunning()
    {
        if (_launched is not { } launched || !GameProcessMatcher.IsGameProcess(launched, _game.InstallPath))
            return false;

        foreach (var process in _catalog.Snapshot())
        {
            if (process.Id == launched.Id)
                return true;
        }

        return false;
    }
}
