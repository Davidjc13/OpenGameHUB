using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Services.Media;

namespace OpenGameHUB.ViewModels;

public partial class SystemMediaViewModel : ViewModelBase
{
    private readonly ISystemMediaController _media;
    private readonly Action<Action> _invoke;
    private string _artworkKey = string.Empty;
    private int _artworkRequest;
    private bool _subscribed;

    public SystemMediaViewModel(ISystemMediaController media, Action<Action>? invoke = null)
    {
        _media = media;
        _invoke = invoke ?? InvokeOnUiThread;
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

    public bool HasArtist => !string.IsNullOrWhiteSpace(Artist);

    public bool HasArtwork => Artwork is not null;

    public string ToggleGlyph => IsPlaying ? "\uE769" : "\uE768";

    public string ToggleToolTip => IsPlaying ? Loc.T("MediaPause") : Loc.T("MediaPlay");

    public string PreviousToolTip => Loc.T("MediaPrevious");

    public string NextToolTip => Loc.T("MediaNext");

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

    public void Stop() => _media.Stop();

    public void ApplyLocalization()
    {
        OnPropertyChanged(nameof(ToggleToolTip));
        OnPropertyChanged(nameof(PreviousToolTip));
        OnPropertyChanged(nameof(NextToolTip));
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

    private static void InvokeOnUiThread(Action action)
    {
        var dispatcher = Dispatcher.UIThread;
        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.Post(action);
    }
}
