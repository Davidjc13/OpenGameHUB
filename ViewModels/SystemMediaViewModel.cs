using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Services.Media;

namespace OpenGameHUB.ViewModels;

public partial class SystemMediaViewModel : ViewModelBase
{
    public const int VolumeStep = 5;

    private static readonly TimeSpan OsdDuration = TimeSpan.FromSeconds(2);

    private readonly ISystemMediaController _media;
    private readonly IMediaVolumeController _volumeController;
    private readonly Action<Action> _invoke;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private string _artworkKey = string.Empty;
    private string _sourceAppId = string.Empty;
    private string? _volumeProcess;
    private int _artworkRequest;
    private bool _subscribed;
    private bool _suppressVolume;
    private double _levelBeforeMute = 100;
    private CancellationTokenSource? _osdHide;

    public SystemMediaViewModel(
        ISystemMediaController media,
        Action<Action>? invoke = null,
        IMediaVolumeController? volume = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _media = media;
        _volumeController = volume ?? DisabledMediaVolume.Instance;
        _invoke = invoke ?? InvokeOnUiThread;
        _delay = delay ?? ((duration, token) => Task.Delay(duration, token));
    }

    [ObservableProperty]
    private bool _isAvailable;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _artist = string.Empty;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _canToggle;

    [ObservableProperty]
    private bool _canNext;

    [ObservableProperty]
    private bool _canPrevious;

    [ObservableProperty]
    private Bitmap? _artwork;

    [ObservableProperty]
    private double _volume = 100;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _canControlVolume;

    [ObservableProperty]
    private bool _osdVisible;

    [ObservableProperty]
    private double _osdVolume = 100;

    [ObservableProperty]
    private bool _osdMuted;

    [ObservableProperty]
    private string _osdHint = string.Empty;

    public bool HasArtist => !string.IsNullOrWhiteSpace(Artist);

    public bool HasArtwork => Artwork is not null;

    public string ToggleGlyph => IsPlaying ? "\uE769" : "\uE768";

    public string ToggleToolTip => IsPlaying ? Loc.T("MediaPause") : Loc.T("MediaPlay");

    public string PreviousToolTip => Loc.T("MediaPrevious");

    public string NextToolTip => Loc.T("MediaNext");

    public string VolumeGlyph => IsMuted || Volume <= 0.5 ? "\uE74F" : "\uE767";

    public string VolumeToolTip =>
        !CanControlVolume
            ? Loc.T("MediaVolumeUnavailable")
            : IsMuted || Volume <= 0.5
                ? Loc.T("MediaMuted")
                : Loc.T("MediaVolume");

    public void Start()
    {
        if (!_subscribed)
        {
            _media.Changed += OnChanged;
            _subscribed = true;
        }

        _media.Start();
        _invoke(() => Apply(_media.Current));
    }

    public void Stop()
    {
        HideOsd();
        _media.Stop();
    }

    public void ApplyLocalization()
    {
        OnPropertyChanged(nameof(ToggleToolTip));
        OnPropertyChanged(nameof(PreviousToolTip));
        OnPropertyChanged(nameof(NextToolTip));
        NotifyVolumeChrome();
        if (OsdVisible)
            RefreshOsdHint();
    }

    public void BumpVolume(int deltaPercent)
    {
        EnsureVolumeSession();
        if (CanControlVolume)
        {
            if (IsMuted)
            {
                var restored = _levelBeforeMute > 0.5 ? _levelBeforeMute : 100;
                SetMutedState(false);
                ApplyVolume(restored, muted: false);
            }

            ApplyVolume(Math.Clamp(Volume + deltaPercent, 0, 100), muted: false);
        }

        PulseOsd();
    }

    public void ToggleMute()
    {
        EnsureVolumeSession();
        if (CanControlVolume)
        {
            if (IsMuted)
            {
                var restored = _levelBeforeMute > 0.5 ? _levelBeforeMute : 100;
                SetMutedState(false);
                ApplyVolume(restored, muted: false);
            }
            else
            {
                _levelBeforeMute = Volume > 0.5 ? Volume : 100;
                SetMutedState(true);
                ApplySessionVolume(Volume, muted: true);
            }
        }

        PulseOsd();
    }

    [RelayCommand(CanExecute = nameof(CanToggle))]
    private async Task ToggleAsync()
    {
        IsPlaying = !IsPlaying;
        await _media.TogglePlayPauseAsync();
    }

    [RelayCommand(CanExecute = nameof(CanPrevious))]
    private Task PreviousAsync() => _media.PreviousAsync().AsTask();

    [RelayCommand(CanExecute = nameof(CanNext))]
    private Task NextAsync() => _media.NextAsync().AsTask();

    partial void OnIsPlayingChanged(bool value)
    {
        OnPropertyChanged(nameof(ToggleGlyph));
        OnPropertyChanged(nameof(ToggleToolTip));
    }

    partial void OnArtistChanged(string value) => OnPropertyChanged(nameof(HasArtist));

    partial void OnArtworkChanged(Bitmap? value) => OnPropertyChanged(nameof(HasArtwork));

    partial void OnCanToggleChanged(bool value) => ToggleCommand.NotifyCanExecuteChanged();

    partial void OnCanPreviousChanged(bool value) => PreviousCommand.NotifyCanExecuteChanged();

    partial void OnCanNextChanged(bool value) => NextCommand.NotifyCanExecuteChanged();

    partial void OnVolumeChanged(double value)
    {
        if (_suppressVolume)
            return;

        var level = (float)(Math.Clamp(value, 0, 100) / 100d);
        var muted = level <= 0.001f;
        if (IsMuted != muted)
        {
            _suppressVolume = true;
            IsMuted = muted;
            _suppressVolume = false;
        }

        ApplySessionVolume(value, muted);
        if (!muted)
            _levelBeforeMute = value;
        NotifyVolumeChrome();
    }

    partial void OnIsMutedChanged(bool value)
    {
        if (!_suppressVolume)
            NotifyVolumeChrome();
    }

    partial void OnCanControlVolumeChanged(bool value) => NotifyVolumeChrome();

    private void OnChanged(object? sender, EventArgs e) => _invoke(() => Apply(_media.Current));

    private void Apply(SystemMediaSnapshot snapshot)
    {
        IsAvailable = snapshot.IsAvailable;
        Title = snapshot.Title;
        Artist = snapshot.Artist;
        IsPlaying = snapshot.IsPlaying;
        CanToggle = snapshot.IsAvailable && snapshot.CanToggle;
        CanNext = snapshot.IsAvailable && snapshot.CanNext;
        CanPrevious = snapshot.IsAvailable && snapshot.CanPrevious;
        SyncVolume(snapshot.IsAvailable ? snapshot.SourceAppId : string.Empty);

        if (!snapshot.IsAvailable || string.IsNullOrEmpty(snapshot.ArtworkKey))
        {
            _artworkKey = string.Empty;
            _artworkRequest++;
            SetArtwork(null);
            return;
        }

        if (snapshot.ArtworkKey == _artworkKey)
            return;

        _artworkKey = snapshot.ArtworkKey;
        var request = ++_artworkRequest;
        _ = LoadArtworkAsync(request, snapshot.ArtworkKey);
    }

    private async Task LoadArtworkAsync(int request, string key)
    {
        byte[]? bytes = null;
        try
        {
            bytes = await _media.GetArtworkAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            bytes = null;
        }

        _invoke(() =>
        {
            if (request != _artworkRequest || !string.Equals(key, _artworkKey, StringComparison.Ordinal))
                return;

            SetArtwork(DecodeArtwork(bytes));
        });
    }

    private void SetArtwork(Bitmap? bitmap)
    {
        var previous = Artwork;
        if (ReferenceEquals(previous, bitmap))
            return;

        Artwork = bitmap;
        previous?.Dispose();
    }

    private static Bitmap? DecodeArtwork(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
            return null;

        try
        {
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void SyncVolume(string sourceAppId)
    {
        var sameSource = string.Equals(sourceAppId, _sourceAppId, StringComparison.Ordinal);
        if (sameSource && CanControlVolume)
            return;

        _sourceAppId = sourceAppId;
        _volumeProcess = MediaVolumeTarget.ProcessName(sourceAppId);
        ApplyResolvedVolume(_volumeController.TryGet(_volumeProcess, out var level) ? level : null);
    }

    private void EnsureVolumeSession()
    {
        if (CanControlVolume)
            return;

        if (!_volumeController.TryGet(_volumeProcess, out var level))
            return;

        ApplyResolvedVolume(level);
    }

    private void ApplyResolvedVolume(MediaVolumeLevel? resolved)
    {
        _suppressVolume = true;
        if (resolved is { } level)
        {
            CanControlVolume = true;
            Volume = Math.Clamp(Math.Round(level.Level * 100), 0, 100);
            IsMuted = level.Muted || level.Level <= 0.001f;
            if (!IsMuted && Volume > 0.5)
                _levelBeforeMute = Volume;
        }
        else
        {
            CanControlVolume = false;
            Volume = 100;
            IsMuted = false;
        }

        _suppressVolume = false;
        NotifyVolumeChrome();
    }

    private void ApplyVolume(double percent, bool muted)
    {
        var clamped = Math.Clamp(percent, 0, 100);
        _suppressVolume = true;
        Volume = clamped;
        IsMuted = muted || clamped <= 0.001;
        _suppressVolume = false;
        ApplySessionVolume(clamped, IsMuted);
        NotifyVolumeChrome();
    }

    private void ApplySessionVolume(double percent, bool muted)
    {
        if (_volumeProcess is null)
            return;

        var level = (float)(Math.Clamp(percent, 0, 100) / 100d);
        _volumeController.TrySet(_volumeProcess, level, muted);
    }

    private void SetMutedState(bool muted)
    {
        _suppressVolume = true;
        IsMuted = muted;
        _suppressVolume = false;
        NotifyVolumeChrome();
    }

    private void PulseOsd()
    {
        OsdVolume = Volume;
        OsdMuted = IsMuted || Volume <= 0.5;
        RefreshOsdHint();
        OsdVisible = true;
        _osdHide?.Cancel();
        _osdHide?.Dispose();
        var cts = new CancellationTokenSource();
        _osdHide = cts;
        _ = HideOsdAfterDelayAsync(cts.Token);
    }

    private async Task HideOsdAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await _delay(OsdDuration, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
            return;

        _invoke(HideOsd);
    }

    private void HideOsd()
    {
        _osdHide?.Cancel();
        _osdHide?.Dispose();
        _osdHide = null;
        OsdVisible = false;
    }

    private void RefreshOsdHint()
    {
        OsdHint = !CanControlVolume
            ? Loc.T("MediaVolumeUnavailable")
            : OsdMuted
                ? Loc.T("MediaMuted")
                : Loc.T("MediaVolume");
    }

    private void NotifyVolumeChrome()
    {
        OnPropertyChanged(nameof(VolumeGlyph));
        OnPropertyChanged(nameof(VolumeToolTip));
    }

    private sealed class DisabledMediaVolume : IMediaVolumeController
    {
        public static DisabledMediaVolume Instance { get; } = new();

        public bool TryGet(string? processName, out MediaVolumeLevel level)
        {
            level = default;
            return false;
        }

        public bool TrySet(string? processName, float level, bool muted) => false;
    }

    private static void InvokeOnUiThread(Action action)
    {
        var dispatcher = Dispatcher.UIThread;
        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.Post(action);
    }
}
