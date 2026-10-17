using System.Diagnostics;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure.Windows;

namespace OpenGameHUB.Services.Games;

internal sealed class GamePlaySession : IDisposable
{
    private readonly IGameProcessCatalog _catalog;
    private readonly ISteamRunningReader _steam;
    private readonly TimeSpan _pollInterval;
    private CancellationTokenSource? _cts;
    private int _generation;

    public GamePlaySession()
        : this(new RunningGameProcessCatalog(), new SteamRunningAppReader(), GameProcessMonitor.PollInterval)
    {
    }

    public GamePlaySession(
        IGameProcessCatalog catalog,
        ISteamRunningReader steam,
        TimeSpan? pollInterval = null)
    {
        _catalog = catalog;
        _steam = steam;
        _pollInterval = pollInterval ?? GameProcessMonitor.PollInterval;
    }

    public bool IsActive { get; private set; }

    public event EventHandler? Ended;

    public void Start(UnifiedGame game, Process? launched)
    {
        CancelWatch();
        var generation = Interlocked.Increment(ref _generation);
        IsActive = true;
        var cts = new CancellationTokenSource();
        _cts = cts;
        _ = Task.Run(() => WatchAsync(generation, game, launched, cts.Token));
    }

    public void Cancel() => CancelWatch();

    public void Dispose()
    {
        CancelWatch();
        _cts?.Dispose();
        _cts = null;
    }

    private void CancelWatch()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task WatchAsync(
        int generation,
        UnifiedGame game,
        Process? launched,
        CancellationToken cancellationToken)
    {
        try
        {
            var monitor = new GameProcessMonitor(
                game,
                Describe(launched),
                _catalog,
                _steam);

            while (!cancellationToken.IsCancellationRequested)
            {
                if (monitor.Evaluate() == GameMonitorPhase.Ended)
                    break;

                await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
            }

            if (!cancellationToken.IsCancellationRequested)
                RaiseEnded(generation);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            RaiseEnded(generation);
        }
    }

    private void RaiseEnded(int generation)
    {
        if (generation != Volatile.Read(ref _generation))
            return;

        IsActive = false;
        Ended?.Invoke(this, EventArgs.Empty);
    }

    internal static GameProcessInfo? Describe(Process? process)
    {
        if (process is null)
            return null;

        try
        {
            if (process.HasExited)
                return null;

            return new GameProcessInfo(process.Id, ProcessImagePath.TryGet(process), process.ProcessName);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
