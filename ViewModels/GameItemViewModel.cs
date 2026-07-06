using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenGameHUB.Models;
using OpenGameHUB.Services;

namespace OpenGameHUB.ViewModels;

public partial class GameItemViewModel : ViewModelBase
{
    public GameItemViewModel(UnifiedGame game)
    {
        Source = game;
        Title = game.Title;
        Platform = game.Platform;
        PlatformLabel = PlatformLabels.Get(game.Platform);
        IsFavorite = game.IsFavorite;
        ApplyLocalization();
    }

    public UnifiedGame Source { get; }

    public string Title { get; }
    public Platform Platform { get; }
    public string PlatformLabel { get; }

    [ObservableProperty]
    private string _installPath = string.Empty;

    [ObservableProperty]
    private string _installStatus = string.Empty;

    [ObservableProperty]
    private string _actionLabel = string.Empty;

    [ObservableProperty]
    private string _playtimeLabel = string.Empty;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private Bitmap? _coverImage;

    [ObservableProperty]
    private bool _hasCover;

    [ObservableProperty]
    private bool _showCoverInGrid;

    public bool DisplayGridCover => ShowCoverInGrid && HasCover;

    public bool DisplayListCover => ShowCoverInGrid && HasCover;

    public bool HasCustomCover => Source.HasCustomCover;

    public bool IsInstalled => Source.IsInstalled;

    public double GridCoverOpacity => IsInstalled ? 1.0 : 0.75;

    public double GridPlaceholderOpacity => IsInstalled ? 0.28 : 0.14;

    partial void OnHasCoverChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayGridCover));
        OnPropertyChanged(nameof(DisplayListCover));
    }

    partial void OnShowCoverInGridChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayGridCover));
        OnPropertyChanged(nameof(DisplayListCover));
    }

    public void ApplyLocalization()
    {
        InstallPath = Source.IsInstalled
            ? Source.InstallPath ?? Loc.T("UnknownPath")
            : Loc.T("NotInstalled");
        InstallStatus = Source.IsInstalled ? Loc.T("Installed") : Loc.T("InLibrary");
        ActionLabel = Source.IsInstalled ? Loc.T("Play") : Loc.T("Install");
        PlaytimeLabel = BuildPlaytimeLabel();
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(GridCoverOpacity));
        OnPropertyChanged(nameof(GridPlaceholderOpacity));
    }

    private string BuildPlaytimeLabel()
    {
        if (Source.PlaytimeMinutes <= 0)
            return Loc.T("NoPlaytimeData");

        var hours = Source.PlaytimeMinutes / 60;
        var minutes = Source.PlaytimeMinutes % 60;

        return Source.LastPlayed is null
            ? Loc.T("PlaytimePlayed", hours, minutes)
            : Loc.T("PlaytimeLastPlayed", hours, minutes, Source.LastPlayed.Value.ToString("d"));
    }

    public Task LoadCoverAsync(MetadataService? metadata = null, CancellationToken cancellationToken = default)
    {
        if (metadata is not null)
            return LoadCoverFromMetadataAsync(metadata, cancellationToken);

        return LoadCoverFromCacheAsync();
    }

    private async Task LoadCoverFromMetadataAsync(
        MetadataService metadata,
        CancellationToken cancellationToken)
    {
        var path = await metadata.EnsureCoverAsync(Source, cancellationToken);
        if (path is null)
            return;

        await SetCoverFromPathAsync(path);
    }

    private Task LoadCoverFromCacheAsync()
    {
        var path = CoverPathHelper.ResolveExistingPath(Source);
        return path is null ? Task.CompletedTask : SetCoverFromPathAsync(path);
    }

    public void ReleaseCover()
    {
        CoverImage?.Dispose();
        CoverImage = null;
        HasCover = false;
    }

    public void RefreshCoverState()
    {
        OnPropertyChanged(nameof(HasCustomCover));
        OnPropertyChanged(nameof(DisplayGridCover));
        OnPropertyChanged(nameof(DisplayListCover));
    }

    public async Task ApplyCoverFromPathAsync(string path)
    {
        await SetCoverFromPathAsync(path);
        RefreshCoverState();
    }

    private async Task SetCoverFromPathAsync(string path)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            CoverImage?.Dispose();
            Source.CoverPath = path;
            CoverImage = new Bitmap(path);
            HasCover = true;
        });
    }
}
