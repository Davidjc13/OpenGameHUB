using System.Runtime.InteropServices;
using OpenGameHUB.Services.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace OpenGameHUB.Infrastructure.Media;

public sealed class WindowsSystemMediaController : ISystemMediaController
{
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly object _gate = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private Timer? _timer;
    private SynchronizationContext? _ui;
    private SystemMediaSnapshot _current = SystemMediaSnapshot.None;
    private readonly SpotifyPlaybackTracker _spotify = new();
    private int _active;
    private int _epoch;
    private int _refreshGate;
    private int _loggedFailure;

    public SystemMediaSnapshot Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    public event EventHandler? Changed;

    public void Start()
    {
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
            return;

        _ui = SynchronizationContext.Current;
        var epoch = Interlocked.Increment(ref _epoch);
        _ = StartCoreAsync(epoch);
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _active, 0) == 0)
            return;

        var epoch = Interlocked.Increment(ref _epoch);
        _ = StopCoreAsync(epoch);
    }

    public ValueTask TogglePlayPauseAsync() => TransportAsync(MediaTransport.Toggle);

    public ValueTask NextAsync() => TransportAsync(MediaTransport.Next);

    public ValueTask PreviousAsync() => TransportAsync(MediaTransport.Previous);

    public async ValueTask<byte[]?> GetArtworkAsync()
    {
        var session = Session;
        if (session is null)
            return null;

        try
        {
            var properties = await session.TryGetMediaPropertiesAsync();
            var thumbnail = properties?.Thumbnail;
            if (thumbnail is null)
                return null;

            return await ReadThumbnailAsync(thumbnail);
        }
        catch (Exception ex) when (IsMediaFailure(ex))
        {
            return null;
        }
    }

    private async Task StartCoreAsync(int epoch)
    {
        await _lifecycle.WaitAsync();
        try
        {
            if (!IsCurrent(epoch))
                return;

            GlobalSystemMediaTransportControlsSessionManager? manager = null;
            try
            {
                manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            }
            catch (Exception ex) when (IsMediaFailure(ex))
            {
                LogOnce(ex);
            }

            if (!IsCurrent(epoch))
                return;

            if (manager is not null)
            {
                _manager = manager;
                manager.CurrentSessionChanged += OnCurrentSessionChanged;
                Bind(ChooseSession());
            }
            else
            {
                RequestRefresh();
            }

            _timer = new Timer(OnTimer, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private async Task StopCoreAsync(int epoch)
    {
        await _lifecycle.WaitAsync();
        try
        {
            _timer?.Dispose();
            _timer = null;
            if (_manager is not null)
            {
                _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
                _manager = null;
            }

            Bind(null);
            Publish(SystemMediaSnapshot.None, epoch);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private async ValueTask TransportAsync(MediaTransport transport)
    {
        if (Volatile.Read(ref _active) == 0)
            return;

        var epoch = Volatile.Read(ref _epoch);
        var session = Session;
        var handled = false;
        if (session is not null && !IsSpotify(session.SourceAppUserModelId))
        {
            try
            {
                handled = await SendToSessionAsync(session, transport);
            }
            catch (Exception ex) when (IsMediaFailure(ex))
            {
                LogOnce(ex);
            }
        }

        if (!handled)
        {
            if (!SpotifyWindowMedia.TrySend(transport))
                MediaKeySender.Tap(transport);

            if (transport == MediaTransport.Toggle)
                _spotify.NoteToggle();
            else
                _spotify.NoteSkipped();
        }

        if (epoch == Volatile.Read(ref _epoch))
            RequestRefresh();
    }

    private static async Task<bool> SendToSessionAsync(
        GlobalSystemMediaTransportControlsSession session,
        MediaTransport transport)
    {
        switch (transport)
        {
            case MediaTransport.Next:
                return await session.TrySkipNextAsync();
            case MediaTransport.Previous:
                return await session.TrySkipPreviousAsync();
            default:
                var playing = SafeStatus(session) == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                var changed = playing
                    ? await session.TryPauseAsync()
                    : await session.TryPlayAsync();
                if (changed)
                    return true;

                return await session.TryTogglePlayPauseAsync();
        }
    }

    private static bool IsSpotify(string? sourceAppId) =>
        sourceAppId?.Contains("spotify", StringComparison.OrdinalIgnoreCase) == true;

    private void OnTimer(object? state) => RequestRefresh();

    private void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args) =>
        Invoke(BindCurrent);

    private void OnPlaybackChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args) =>
        RequestRefresh();

    private void OnPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args) =>
        RequestRefresh();

    private void BindCurrent() => Bind(ChooseSession());

    private void RequestRefresh() => Invoke(QueueRefresh);

    private void QueueRefresh()
    {
        var state = Interlocked.CompareExchange(ref _refreshGate, 1, 0);
        if (state == 0)
            _ = RefreshLoopAsync();
        else
            Interlocked.CompareExchange(ref _refreshGate, 2, 1);
    }

    private async Task RefreshLoopAsync()
    {
        var epoch = Volatile.Read(ref _epoch);
        try
        {
            while (true)
            {
                if (!IsCurrent(epoch))
                {
                    FinishRefresh(requeueIfActive: true);
                    return;
                }

                var snapshot = await ReadAsync();
                if (!IsCurrent(epoch))
                {
                    FinishRefresh(requeueIfActive: true);
                    return;
                }

                Publish(snapshot, epoch);
                if (Interlocked.CompareExchange(ref _refreshGate, 0, 1) == 1)
                    return;

                Interlocked.Exchange(ref _refreshGate, 1);
                epoch = Volatile.Read(ref _epoch);
            }
        }
        catch (Exception ex)
        {
            LogOnce(ex);
            Interlocked.Exchange(ref _refreshGate, 0);
        }
    }

    private void FinishRefresh(bool requeueIfActive)
    {
        Interlocked.Exchange(ref _refreshGate, 0);
        if (requeueIfActive && Volatile.Read(ref _active) == 1)
            QueueRefresh();
    }

    private async Task<SystemMediaSnapshot> ReadAsync()
    {
        var session = Session;
        if (session is null)
            return ReadSpotify();

        try
        {
            var snapshot = await ReadSessionAsync(session);
            if (snapshot.IsAvailable)
                return snapshot;
        }
        catch (Exception ex) when (IsMediaFailure(ex))
        {
            LogOnce(ex);
        }

        return ReadSpotify();
    }

    private SystemMediaSnapshot ReadSpotify()
    {
        SpotifyWindowMedia.TryGetTitle(out var title);
        return _spotify.Observe(title);
    }

    private async Task<SystemMediaSnapshot> ReadSessionAsync(GlobalSystemMediaTransportControlsSession session)
    {
        var playback = session.GetPlaybackInfo();
        var status = playback.PlaybackStatus;
        if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed)
            return SystemMediaSnapshot.None;

        string title;
        string artist;
        string album;
        var hasArtwork = false;
        try
        {
            var properties = await session.TryGetMediaPropertiesAsync();
            title = properties?.Title?.Trim() ?? string.Empty;
            artist = properties?.Artist?.Trim() ?? string.Empty;
            album = properties?.AlbumTitle?.Trim() ?? string.Empty;
            hasArtwork = properties?.Thumbnail is not null;
        }
        catch (Exception ex) when (IsMediaFailure(ex))
        {
            title = string.Empty;
            artist = string.Empty;
            album = string.Empty;
        }

        playback = session.GetPlaybackInfo();
        status = playback.PlaybackStatus;
        if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed)
            return SystemMediaSnapshot.None;

        if (string.IsNullOrWhiteSpace(title))
            title = SystemMediaSourceNames.Format(session.SourceAppUserModelId);

        if (string.IsNullOrWhiteSpace(title))
            return SystemMediaSnapshot.None;

        var controls = playback.Controls;
        var playing = status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        return new SystemMediaSnapshot(
            IsAvailable: true,
            Title: title,
            Artist: artist,
            IsPlaying: playing,
            CanToggle: true,
            CanNext: controls.IsNextEnabled,
            CanPrevious: controls.IsPreviousEnabled,
            ArtworkKey: hasArtwork
                ? $"{session.SourceAppUserModelId}|{title}|{artist}|{album}"
                : string.Empty,
            SourceAppId: session.SourceAppUserModelId ?? string.Empty);
    }

    private GlobalSystemMediaTransportControlsSession? ChooseSession()
    {
        var manager = _manager;
        if (manager is null)
            return null;

        try
        {
            var current = manager.GetCurrentSession();
            if (current is not null && SafeStatus(current) != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed)
                return current;

            GlobalSystemMediaTransportControlsSession? paused = null;
            foreach (var session in manager.GetSessions())
            {
                var status = SafeStatus(session);
                if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    return session;

                if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
                    paused ??= session;
            }

            return paused;
        }
        catch (Exception ex) when (IsMediaFailure(ex))
        {
            return null;
        }
    }

    private void Bind(GlobalSystemMediaTransportControlsSession? session)
    {
        var previous = Session;
        if (ReferenceEquals(previous, session))
        {
            RequestRefresh();
            return;
        }

        if (previous is not null)
        {
            previous.PlaybackInfoChanged -= OnPlaybackChanged;
            previous.MediaPropertiesChanged -= OnPropertiesChanged;
        }

        lock (_gate)
            _session = session;

        if (session is not null)
        {
            session.PlaybackInfoChanged += OnPlaybackChanged;
            session.MediaPropertiesChanged += OnPropertiesChanged;
        }

        RequestRefresh();
    }

    private GlobalSystemMediaTransportControlsSession? Session
    {
        get
        {
            lock (_gate)
                return _session;
        }
    }

    private void Publish(SystemMediaSnapshot snapshot, int epoch)
    {
        var changed = false;
        lock (_gate)
        {
            if (epoch != _epoch)
                return;

            if (_current != snapshot)
            {
                _current = snapshot;
                changed = true;
            }
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Invoke(Action action)
    {
        var ui = _ui;
        if (ui is null || SynchronizationContext.Current == ui)
            action();
        else
            ui.Post(_ => action(), null);
    }

    private bool IsCurrent(int epoch) =>
        Volatile.Read(ref _active) == 1 && epoch == Volatile.Read(ref _epoch);

    private static GlobalSystemMediaTransportControlsSessionPlaybackStatus SafeStatus(
        GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            return session.GetPlaybackInfo().PlaybackStatus;
        }
        catch (Exception ex) when (IsMediaFailure(ex))
        {
            return GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed;
        }
    }

    private static async Task<byte[]?> ReadThumbnailAsync(IRandomAccessStreamReference reference)
    {
        using var stream = await reference.OpenReadAsync();
        var size = stream.Size;
        if (size == 0 || size > 8 * 1024 * 1024)
            return null;

        var length = (uint)size;
        using var reader = new DataReader(stream);
        await reader.LoadAsync(length);
        var bytes = new byte[length];
        reader.ReadBytes(bytes);
        reader.DetachStream();
        return bytes;
    }

    private void LogOnce(Exception exception)
    {
        if (Interlocked.Exchange(ref _loggedFailure, 1) == 0)
            AppLog.Error($"System media controls are unavailable: {exception.Message}");
    }

    private static bool IsMediaFailure(Exception exception) =>
        exception is COMException or InvalidOperationException or UnauthorizedAccessException
            or IOException or ObjectDisposedException or ArgumentException;
}
