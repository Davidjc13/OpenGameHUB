using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Providers.Ea;
using OpenGameHUB.Services.Games;
using OpenGameHUB.Views;

namespace OpenGameHUB.ViewModels.MainWindow;

public partial class MainWindowLibraryViewModel : ViewModelBase
{
    private readonly GameLibraryService _library;
    private readonly GameInstallOrchestrator _installOrchestrator;
    private readonly MainWindowSidebarViewModel _sidebar;
    private readonly Action<string> _setStatusText;
    private readonly Action<TimeSpan> _scheduleStatusClear;
    private readonly Action _cancelScheduledStatusClear;
    private readonly Func<Window> _getMainWindow;
    private readonly Func<Window, Task> _showDialogAsync;
    private List<GameItemViewModel> _allGames = [];
    private List<GameItemViewModel> _filteredGames = [];
    private CancellationTokenSource? _refreshCts;
    private CancellationTokenSource? _coverCts;
    private bool _suppressCoverLoading = true;
    private int _effectivePageSize;
    private double _libraryViewportWidth;
    private double _libraryViewportHeight;
    private GameItemViewModel? _previousSelectedGame;

    public MainWindowLibraryViewModel(
        GameLibraryService library,
        GameInstallOrchestrator installOrchestrator,
        MainWindowSidebarViewModel sidebar,
        Action<string> setStatusText,
        Action<TimeSpan> scheduleStatusClear,
        Action cancelScheduledStatusClear,
        Func<Window> getMainWindow,
        Func<Window, Task> showDialogAsync)
    {
        _library = library;
        _installOrchestrator = installOrchestrator;
        _sidebar = sidebar;
        _setStatusText = setStatusText;
        _scheduleStatusClear = scheduleStatusClear;
        _cancelScheduledStatusClear = cancelScheduledStatusClear;
        _getMainWindow = getMainWindow;
        _showDialogAsync = showDialogAsync;

        Games = new ObservableCollection<GameItemViewModel>();
        CoverQualityMode = _library.Settings.Current.CoverQualityMode;
        IsListView = _library.Settings.Current.LibraryViewMode == LibraryViewMode.List;
    }

    public ObservableCollection<GameItemViewModel> Games { get; }

    [ObservableProperty]
    private string _gamesCountLabel = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private GameItemViewModel? _selectedGame;

    [ObservableProperty]
    private CoverQualityMode _coverQualityMode = CoverQualityMode.Low;

    [ObservableProperty]
    private bool _isListView;

    [ObservableProperty]
    private int _gridColumns = 4;

    [ObservableProperty]
    private int _gridRows = 3;

    [ObservableProperty]
    private int _currentPage = 1;

    public bool IsGridView => !IsListView;

    public bool ShowDetailCover => CoverQualitySettings.Get(CoverQualityMode).ShowDetailCover;

    public bool SelectedGameHasCustomCover => SelectedGame?.HasCustomCover == true;

    public bool HasSelectedGame => SelectedGame is not null;

    public string SelectedGameTitle => SelectedGame?.Title ?? Loc.T("SelectGame");

    public string SelectedGameActionLabel => SelectedGame?.ActionLabel ?? Loc.T("Play");

    public bool IsEpicCloudAvailable => _library.IsEpicCloudAvailable;

    public bool IsUbisoftCloudAvailable => _library.IsUbisoftCloudAvailable;

    public bool IsEaCloudAvailable => _library.IsEaCloudAvailable;

    public bool IsRiotCloudAvailable => _library.IsRiotCloudAvailable;

    public bool IsGogCloudAvailable => _library.IsGogCloudAvailable;

    public bool IsRockstarCloudAvailable => _library.IsRockstarCloudAvailable;

    public bool IsXboxCloudAvailable => _library.IsXboxCloudAvailable;

    public bool IsSteamCloudAvailable => _library.IsSteamCloudAvailable;

    public bool IsSteamApiConfigured => _library.IsSteamApiConfigured;

    private CoverQualityProfile CoverProfile => CoverQualitySettings.Get(CoverQualityMode);

    private int PageSize => _effectivePageSize > 0
        ? _effectivePageSize
        : (IsListView ? CoverProfile.PageSize : Math.Max(1, GridColumns * GridRows));

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(_filteredGames.Count / (double)PageSize));

    public bool CanGoPrevious => CurrentPage > 1;

    public bool CanGoNext => CurrentPage < TotalPages;

    public void LoadCachedGames()
    {
        var cached = _library.LoadCachedGames();
        if (cached.Count == 0)
            return;

        _allGames = GameItemViewModelFactory.CreateGrouped(cached);
        ApplyGameMembership();
        _sidebar.RebuildAll(_allGames);
        ApplyFilter();
        _suppressCoverLoading = false;
        ApplyVisibleCovers();
        _setStatusText(Loc.T("GamesInCache", _allGames.Count));
    }

    public void ReplaceAllGames(IReadOnlyList<GameItemViewModel> games)
    {
        _allGames = games.ToList();
        ApplyGameMembership();
        _sidebar.RebuildAll(_allGames);
        ApplyFilter();
    }

    public void MergeGames(IReadOnlyList<GameItemViewModel> games)
    {
        _allGames = games.ToList();
        ApplyFilter();
    }

    public void ApplyLocalization(bool releaseCoversOnQualityChange)
    {
        var previousQuality = CoverQualityMode;
        CoverQualityMode = _library.Settings.Current.CoverQualityMode;
        IsListView = _library.Settings.Current.LibraryViewMode == LibraryViewMode.List;
        OnPropertyChanged(nameof(ShowDetailCover));
        if (releaseCoversOnQualityChange && previousQuality != CoverQualityMode)
            ReleaseAllGameCovers();
        OnPropertyChanged(nameof(IsGridView));
        OnPropertyChanged(nameof(SelectedGameHasCustomCover));
        OnPropertyChanged(nameof(SelectedGameTitle));
        OnPropertyChanged(nameof(SelectedGameActionLabel));
        OnPropertyChanged(nameof(HasSelectedGame));
        ApplyFilter();
        ApplyVisibleCovers();
    }

    public void LocalizeGames()
    {
        foreach (var game in _allGames)
            game.ApplyLocalization();
    }

    public void UpdateLibraryViewport(double width, double height)
    {
        if (width >= 10 && height >= 10)
        {
            _libraryViewportWidth = width;
            _libraryViewportHeight = height;
        }

        width = _libraryViewportWidth;
        height = _libraryViewportHeight;
        if (width < 10 || height < 10)
            return;

        var newPageSize = IsListView
            ? LibraryGridMetrics.ListPageSizeFromHeight(height)
            : LibraryGridMetrics.Calculate(width, height).PageSize;

        if (!IsListView)
        {
            var metrics = LibraryGridMetrics.Calculate(width, height);
            GridColumns = metrics.Columns;
            GridRows = metrics.Rows;
        }

        if (newPageSize == _effectivePageSize)
            return;

        _effectivePageSize = newPageSize;
        NotifyPaginationChanged();

        if (CurrentPage > TotalPages)
            CurrentPage = TotalPages;

        ApplyCurrentPage();
    }

    partial void OnCurrentPageChanged(int value)
    {
        ApplyCurrentPage();
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnCoverQualityModeChanged(CoverQualityMode value)
    {
        OnPropertyChanged(nameof(ShowDetailCover));
        ReleaseAllGameCovers();
        NotifyPaginationChanged();
        ApplyCurrentPage();
        ApplyVisibleCovers();
    }

    partial void OnIsListViewChanged(bool value)
    {
        OnPropertyChanged(nameof(IsGridView));
        PersistLibraryViewMode();
        UpdateLibraryViewport(_libraryViewportWidth, _libraryViewportHeight);
        ApplyCurrentPage();
    }

    partial void OnSelectedGameChanged(GameItemViewModel? value)
    {
        if (!ReferenceEquals(_previousSelectedGame, value) && _previousSelectedGame is not null)
        {
            var profile = CoverProfile;
            var keepForGrid = profile.ShowLibraryCovers && Games.Contains(_previousSelectedGame);
            if (!keepForGrid)
                _previousSelectedGame.ReleaseCover();
        }

        _previousSelectedGame = value;

        foreach (var game in _allGames)
            game.IsSelected = ReferenceEquals(game, value);

        var currentProfile = CoverProfile;
        if (value is not null && currentProfile.ShowDetailCover)
        {
            _ = value.EnsureCoverAsync(
                currentProfile.DetailDecodeWidth,
                currentProfile.Interpolation,
                _library.Metadata);
        }

        OnPropertyChanged(nameof(ShowDetailCover));
        OnPropertyChanged(nameof(SelectedGameTitle));
        OnPropertyChanged(nameof(SelectedGameActionLabel));
        OnPropertyChanged(nameof(SelectedGameHasCustomCover));
        OnPropertyChanged(nameof(HasSelectedGame));
        _sidebar.OnSelectedGameChanged();
    }

    [RelayCommand]
    private async Task RefreshLibraryAsync()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource();
        var token = _refreshCts.Token;

        _cancelScheduledStatusClear();
        await RunOnUiThreadAsync(() => IsRefreshing = true);

        try
        {
            var progress = new Progress<string>(message =>
                Dispatcher.UIThread.Post(() => _setStatusText(message)));

            await RunOnUiThreadAsync(() => _setStatusText(Loc.T("ScanningLaunchers")));
            var games = await _library.RefreshLibraryAsync(progress, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();

            await RunOnUiThreadAsync(() =>
            {
                ReleaseAllGameCovers();
                SelectedGame = null;
                _previousSelectedGame = null;

                _allGames = GameItemViewModelFactory.CreateGrouped(games);
                ApplyGameMembership();
                _suppressCoverLoading = false;
                _sidebar.RebuildAll(_allGames);
                ApplyFilter();

                var epicHint = IsEpicCloudAvailable ? Loc.T("EpicCloudHint") : string.Empty;
                var steamHint = IsSteamCloudAvailable
                    ? IsSteamApiConfigured
                        ? Loc.T("SteamCloudHint")
                        : Loc.T("SteamLocalLibraryHint")
                    : string.Empty;
                var ubisoftHint = IsUbisoftCloudAvailable ? Loc.T("UbisoftCloudHint") : string.Empty;
                var eaHint = IsEaCloudAvailable
                    && _library.EaLibraryCacheStatus is EaLibraryCacheStatus.Available
                        or EaLibraryCacheStatus.DecryptFailedUsingLogs
                        ? Loc.T("EaCloudHint")
                        : string.Empty;
                var riotHint = IsRiotCloudAvailable ? Loc.T("RiotCloudHint") : string.Empty;
                var gogHint = IsGogCloudAvailable ? Loc.T("GogCloudHint") : string.Empty;
                var rockstarHint = IsRockstarCloudAvailable ? Loc.T("RockstarCloudHint") : string.Empty;
                var xboxHint = IsXboxCloudAvailable ? Loc.T("XboxCloudHint") : string.Empty;
                _setStatusText(Loc.T("GamesInLibrary", _allGames.Count) + steamHint + ubisoftHint + eaHint + riotHint + gogHint + rockstarHint + xboxHint + epicHint);
            });

            StartBackgroundCoverEnrichment();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            await RunOnUiThreadAsync(() => _setStatusText(string.Empty));
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(MainWindowLibraryViewModel),
                operation: "RefreshLibraryAsync",
                exception: ex,
                details: "Top-level library refresh");
            await RunOnUiThreadAsync(() =>
                _setStatusText(Loc.T("ScanError", ex.Message)));
        }
        finally
        {
            await RunOnUiThreadAsync(() =>
            {
                if (_suppressCoverLoading)
                {
                    _suppressCoverLoading = false;
                    ApplyVisibleCovers();
                }

                IsRefreshing = false;
                _scheduleStatusClear(TimeSpan.FromSeconds(8));
            });
        }
    }

    [RelayCommand]
    private void SelectGame(GameItemViewModel? game) =>
        SelectedGame = ReferenceEquals(SelectedGame, game) ? null : game;

    [RelayCommand]
    private async Task LaunchSelectedGameAsync()
    {
        if (SelectedGame is null)
        {
            _setStatusText(Loc.T("SelectGameFirst"));
            _scheduleStatusClear(TimeSpan.Zero);
            return;
        }

        try
        {
            var launchTarget = await ResolveInstallLaunchTargetAsync(SelectedGame);
            if (launchTarget is null)
                return;

            var installResult = await _installOrchestrator.TryStartInstallAsync(launchTarget);
            switch (installResult.Outcome)
            {
                case GameInstallOutcome.InstallStarted:
                    if (installResult.CancelScheduledStatusClear)
                        _cancelScheduledStatusClear();
                    if (installResult.PreInstallStatusKey is not null)
                        _setStatusText(Loc.T(installResult.PreInstallStatusKey));
                    _setStatusText(Loc.T(installResult.StatusKey!, installResult.StatusArgs ?? [SelectedGame.Title]));
                    _scheduleStatusClear(TimeSpan.FromSeconds(8));
                    return;
                case GameInstallOutcome.ManualInstallNotice:
                    await ShowEaManualInstallNoticeAsync(SelectedGame.Title);
                    return;
            }

            _library.LaunchGame(launchTarget);
            SelectedGame.RefreshLaunchState();
            if (!_sidebar.HasUserSelectedSort)
                _sidebar.RebuildSortOptions();
            ApplyFilter();
            _setStatusText(launchTarget.IsInstalled
                ? Loc.T("LaunchingGame", SelectedGame.Title)
                : Loc.T("StartingInstallLaunch", SelectedGame.Title));
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(MainWindowLibraryViewModel),
                operation: "LaunchSelectedGameAsync",
                exception: ex,
                platform: SelectedGame?.Platform,
                details: SelectedGame?.Source.Id);
            _setStatusText(Loc.T("LaunchFailed", ex.Message));
        }
        finally
        {
            if (SelectedGame is null || SelectedGame.Source.IsInstalled)
                _scheduleStatusClear(TimeSpan.Zero);
        }
    }

    [RelayCommand]
    private void LaunchGame(GameItemViewModel? game)
    {
        if (game is null)
            return;

        SelectedGame = game;
        LaunchSelectedGameCommand.Execute(null);
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
            CurrentPage--;
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
            CurrentPage++;
    }

    [RelayCommand]
    private void SetGridView()
    {
        if (!IsListView)
            return;

        IsListView = false;
    }

    [RelayCommand]
    private void SetListView()
    {
        if (IsListView)
            return;

        IsListView = true;
    }

    [RelayCommand]
    private async Task ChangeCustomCoverAsync()
    {
        if (SelectedGame is null)
        {
            _setStatusText(Loc.T("SelectGameForCover"));
            _scheduleStatusClear(TimeSpan.FromSeconds(4));
            return;
        }

        try
        {
            var files = await _getMainWindow().StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Loc.T("ChangeCoverDialogTitle"),
                AllowMultiple = false,
                FileTypeFilter = [FilePickerFileTypes.ImageAll]
            });

            if (files.Count == 0)
                return;

            var localPath = files[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(localPath)
                || !_library.TrySetCustomCover(SelectedGame.Source, localPath))
            {
                _setStatusText(Loc.T("InvalidCoverImage"));
                _scheduleStatusClear(TimeSpan.FromSeconds(4));
                return;
            }

            var profile = CoverProfile;
            await SelectedGame.ApplyCoverFromPathAsync(
                SelectedGame.Source.CoverPath!,
                profile.DetailDecodeWidth,
                profile.Interpolation);
            ApplyVisibleCovers();
            OnPropertyChanged(nameof(SelectedGameHasCustomCover));
            _setStatusText(Loc.T("CoverUpdated", SelectedGame.Title));
            _scheduleStatusClear(TimeSpan.FromSeconds(4));
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(MainWindowLibraryViewModel),
                operation: "ChangeCustomCoverAsync",
                exception: ex,
                platform: SelectedGame?.Platform,
                details: SelectedGame?.Source.Id);
            _setStatusText(Loc.T("CoverUpdateFailedDetail", ex.Message));
            _scheduleStatusClear(TimeSpan.FromSeconds(6));
        }
    }

    [RelayCommand]
    private async Task ResetCustomCoverAsync()
    {
        if (SelectedGame is null || !SelectedGame.HasCustomCover)
            return;

        try
        {
            _setStatusText(Loc.T("ResettingCover", SelectedGame.Title));
            SelectedGame.ReleaseCover();

            var path = await _library.TryResetCustomCoverAsync(SelectedGame.Source);
            if (path is not null)
            {
                var profile = CoverProfile;
                await SelectedGame.ApplyCoverFromPathAsync(path, profile.DetailDecodeWidth, profile.Interpolation);
            }

            ApplyVisibleCovers();
            OnPropertyChanged(nameof(SelectedGameHasCustomCover));
            _setStatusText(path is null
                ? Loc.T("CoverResetNoReplacement", SelectedGame.Title)
                : Loc.T("CoverReset", SelectedGame.Title));
            _scheduleStatusClear(TimeSpan.FromSeconds(4));
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(MainWindowLibraryViewModel),
                operation: "ResetCustomCoverAsync",
                exception: ex,
                platform: SelectedGame?.Platform,
                details: SelectedGame?.Source.Id);
            _setStatusText(Loc.T("CoverUpdateFailedDetail", ex.Message));
            _scheduleStatusClear(TimeSpan.FromSeconds(6));
        }
    }

    [RelayCommand]
    private void ToggleFavorite(GameItemViewModel? game)
    {
        if (game is null)
            return;

        game.IsFavorite = !game.IsFavorite;
        _library.ToggleFavorite(game.Source);
        _sidebar.RebuildLibraryCollections();
        ApplyFilter();
        _setStatusText(game.IsFavorite
            ? Loc.T("AddedToFavorites", game.Title)
            : Loc.T("RemovedFromFavorites", game.Title));
        _scheduleStatusClear(TimeSpan.Zero);
    }

    [RelayCommand]
    private void OpenInstallFolder(GameItemViewModel? game)
    {
        if (game is null)
            return;

        try
        {
            _library.OpenInstallFolder(game.Source);
            _setStatusText(Loc.T("InstallFolderOpened", game.Title));
            _scheduleStatusClear(TimeSpan.FromSeconds(4));
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(MainWindowLibraryViewModel),
                operation: "OpenInstallFolder",
                exception: ex,
                platform: game.Platform,
                details: game.Source.Id);
            _setStatusText(Loc.T("OpenInstallFolderFailed", ex.Message));
            _scheduleStatusClear(TimeSpan.FromSeconds(6));
        }
    }

    [RelayCommand]
    private void OpenStorePage(GameItemViewModel? game)
    {
        if (game is null)
            return;

        try
        {
            _library.OpenStorePage(game.Source);
            _setStatusText(Loc.T("StorePageOpened", game.Title));
            _scheduleStatusClear(TimeSpan.FromSeconds(4));
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(MainWindowLibraryViewModel),
                operation: "OpenStorePage",
                exception: ex,
                platform: game.Platform,
                details: game.Source.Id);
            _setStatusText(Loc.T("OpenStorePageFailed", ex.Message));
            _scheduleStatusClear(TimeSpan.FromSeconds(6));
        }
    }

    [RelayCommand]
    private async Task UninstallGameAsync(GameItemViewModel? game)
    {
        if (game is null || !GameLibraryActions.CanUninstall(game.Source))
            return;

        var confirmed = await ConfirmActionAsync(
            Loc.T("UninstallGame"),
            Loc.T("UninstallGameConfirm", game.Title, game.PlatformLabel),
            Loc.T("UninstallGame"));
        if (!confirmed)
            return;

        try
        {
            _library.StartUninstall(game.Source);
            _setStatusText(Loc.T("UninstallStarted", game.Title, game.PlatformLabel));
            _scheduleStatusClear(TimeSpan.FromSeconds(8));
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(MainWindowLibraryViewModel),
                operation: "UninstallGameAsync",
                exception: ex,
                platform: game.Platform,
                details: game.Source.Id);
            _setStatusText(Loc.T("UninstallFailed", ex.Message));
            _scheduleStatusClear(TimeSpan.FromSeconds(6));
        }
    }

    [RelayCommand]
    private async Task RemoveCustomGameAsync(GameItemViewModel? game)
    {
        if (game is null || !GameLibraryActions.CanRemoveFromLibrary(game.Source))
            return;

        var confirmed = await ConfirmActionAsync(
            Loc.T("RemoveFromLibrary"),
            Loc.T("RemoveCustomGameConfirm", game.Title),
            Loc.T("RemoveFromLibrary"));
        if (!confirmed)
            return;

        try
        {
            if (!_library.RemoveCustomGame(game.Source.Id))
            {
                _setStatusText(Loc.T("CustomGameRemoveFailed", game.Title));
                _scheduleStatusClear(TimeSpan.FromSeconds(6));
                return;
            }

            RemoveGameFromUi(game);
            _setStatusText(Loc.T("CustomGameRemoved", game.Title));
            _scheduleStatusClear(TimeSpan.FromSeconds(4));
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(MainWindowLibraryViewModel),
                operation: "RemoveCustomGameAsync",
                exception: ex,
                platform: game.Platform,
                details: game.Source.Id);
            _setStatusText(Loc.T("CustomGameRemoveFailed", game.Title));
            _scheduleStatusClear(TimeSpan.FromSeconds(6));
        }
    }

    private async Task<bool> ConfirmActionAsync(string title, string message, string confirmLabel)
    {
        var viewModel = new CollectionConfirmDialogViewModel(title, message, confirmLabel);
        var window = new CollectionConfirmDialog { DataContext = viewModel };
        await _showDialogAsync(window);
        return viewModel.Confirmed;
    }

    private void RemoveGameFromUi(GameItemViewModel game)
    {
        if (ReferenceEquals(SelectedGame, game)
            || string.Equals(SelectedGame?.Source.Id, game.Source.Id, StringComparison.Ordinal))
        {
            SelectedGame = null;
        }

        game.ReleaseCover();
        _allGames = _allGames
            .Where(item => !string.Equals(item.Source.Id, game.Source.Id, StringComparison.Ordinal))
            .ToList();
        _sidebar.RebuildAll(_allGames);
        ApplyFilter();
    }

    public void ApplyFilter()
    {
        var view = _sidebar.BuildLibraryViewState();
        IReadOnlySet<string>? collectionGameIds = null;
        if (view.Kind == LibraryViewKind.UserCollection && view.UserCollectionId is not null)
            collectionGameIds = _library.Collections.GetGameIdsForCollection(view.UserCollectionId);

        _filteredGames = LibraryFilterPipeline.Apply(
            _allGames,
            view,
            _sidebar.SelectedPlatformFilter?.Platform,
            _sidebar.SearchText,
            _sidebar.ResolveSortOption(),
            collectionGameIds);

        CurrentPage = 1;
        ApplyCurrentPage();

        NotifyPaginationChanged();

        var selected = SelectedGame;
        if (selected is not null && !Games.Contains(selected))
            SelectedGame = null;
    }

    private void ApplyCurrentPage()
    {
        Games.Clear();
        var start = (CurrentPage - 1) * PageSize;
        foreach (var game in _filteredGames.Skip(start).Take(PageSize))
            Games.Add(game);

        GamesCountLabel = _filteredGames.Count == 0
            ? Loc.T("ShowingGamesCount", 0)
            : Loc.T("ShowingGamesPage", CurrentPage, TotalPages, Games.Count, _filteredGames.Count);

        ApplyVisibleCovers();
    }

    private void ApplyVisibleCovers()
    {
        if (_suppressCoverLoading)
        {
            foreach (var game in _allGames)
                game.ShowCoverInGrid = false;
            return;
        }

        var profile = CoverProfile;
        var pageGames = Games.ToHashSet();

        if (!profile.ShowLibraryCovers)
        {
            foreach (var game in _allGames)
            {
                game.ShowCoverInGrid = false;
                if (!ReferenceEquals(game, SelectedGame) || !profile.ShowDetailCover)
                    game.ReleaseCover();
            }

            if (!profile.ShowDetailCover)
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: false);

            return;
        }

        foreach (var game in _allGames)
        {
            if (pageGames.Contains(game))
                continue;

            game.ShowCoverInGrid = false;
            if (!ReferenceEquals(game, SelectedGame) || !profile.ShowDetailCover)
                game.ReleaseCover();
        }

        var decodeWidth = IsListView ? profile.ListDecodeWidth : profile.GridDecodeWidth;
        foreach (var game in pageGames)
        {
            game.ShowCoverInGrid = true;
            _ = game.EnsureCoverAsync(
                decodeWidth,
                profile.Interpolation,
                _library.Metadata);
        }
    }

    private void StartBackgroundCoverEnrichment()
    {
        var maxCovers = CoverProfile.BackgroundMaxCovers;
        if (maxCovers <= 0)
            return;

        _coverCts?.Cancel();
        _coverCts?.Dispose();
        _coverCts = new CancellationTokenSource();
        var token = _coverCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                var progress = new Progress<string>(message =>
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (!IsRefreshing)
                            _setStatusText(message);
                    }));

                await _library.EnrichCoversAsync(progress, token, maxCovers: maxCovers);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // A newer refresh started.
            }
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(MainWindowLibraryViewModel),
                    operation: "StartBackgroundCoverEnrichment",
                    exception: ex);
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!IsRefreshing)
                    {
                        ApplyVisibleCovers();
                        _scheduleStatusClear(TimeSpan.FromSeconds(2));
                    }
                });
            }
        }, token);
    }

    private void ApplyGameMembership()
    {
        var collections = _library.Collections;
        foreach (var game in _allGames)
        {
            var collectionIds = new HashSet<string>(
                collections.GetCollectionIdsForGame(game.Source.Id),
                StringComparer.Ordinal);
            foreach (var alternate in game.Source.AlternateListings)
            {
                foreach (var collectionId in collections.GetCollectionIdsForGame(alternate.Id))
                    collectionIds.Add(collectionId);
            }

            game.SetCollectionIds(collectionIds);
        }
    }

    private void ReleaseAllGameCovers()
    {
        foreach (var game in _allGames)
            game.ReleaseCover();
    }

    private void PersistLibraryViewMode()
    {
        var current = _library.Settings.Current;
        var mode = IsListView ? LibraryViewMode.List : LibraryViewMode.Grid;
        if (current.LibraryViewMode == mode)
            return;

        var updated = current.Clone();
        updated.LibraryViewMode = mode;
        _library.Settings.Save(updated);
    }

    private void NotifyPaginationChanged()
    {
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private async Task ShowEaManualInstallNoticeAsync(string gameTitle)
    {
        var viewModel = new EaManualInstallNoticeViewModel(gameTitle);
        var window = new EaManualInstallNoticeWindow(viewModel);
        await _showDialogAsync(window);
    }

    private async Task<UnifiedGame?> ResolveInstallLaunchTargetAsync(GameItemViewModel selectedGame)
    {
        if (selectedGame.Source.IsInstalled)
            return selectedGame.Source;

        var installTargets = selectedGame.GetUninstalledInstallTargets();
        if (installTargets.Count <= 1)
            return installTargets.FirstOrDefault() ?? selectedGame.Source;

        var viewModel = new StoreInstallChoiceDialogViewModel(selectedGame.Title, installTargets);
        var window = new StoreInstallChoiceDialog { DataContext = viewModel };
        await _showDialogAsync(window);
        return viewModel.Confirmed ? viewModel.SelectedGame : null;
    }

    private static async Task RunOnUiThreadAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            await Dispatcher.UIThread.InvokeAsync(action);
    }
}
