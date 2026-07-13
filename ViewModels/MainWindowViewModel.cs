using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Localization;
using OpenGameHUB.Providers.Ea;
using OpenGameHUB.Providers.Epic;
using OpenGameHUB.Providers.Rockstar;
using OpenGameHUB.Services.Games;
using OpenGameHUB.Views;

namespace OpenGameHUB.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly GameLibraryService _libraryService = new();
    private List<GameItemViewModel> _allGames = [];
    private List<GameItemViewModel> _filteredGames = [];
    private CancellationTokenSource? _statusClearCts;
    private CancellationTokenSource? _refreshCts;
    private CancellationTokenSource? _coverCts;
    private bool _steamApiPromptOffered;
    private bool _eaLibraryPromptOffered;
    private bool _legendaryPromptOffered;
    private int _modalDepth;
    private bool _replayOnboardingAfterSettings;
    private bool _pendingDevRelaunch;
    private bool _pendingDevClearDatabase;
    private bool _suppressCoverLoading = true;
    private bool _suppressSortOptionChanged;
    private SortOption? _userSelectedSort;

    public MainWindowViewModel()
    {
        Loc.Service.Initialize(_libraryService.Settings.Current.Language);
        Loc.Service.LanguageChanged += OnLanguageChanged;

        Games = new ObservableCollection<GameItemViewModel>();
        PlatformFilters = new ObservableCollection<PlatformFilterItem>();
        SortOptions = new ObservableCollection<SortOptionItem>();
        LibraryCollections = new ObservableCollection<LibraryCollectionItem>();
        DetailCollectionMemberships = new ObservableCollection<CollectionMembershipItem>();
        Strings = new LocalizedStrings();

        RebuildSortOptions();
        RebuildPlatformFilters();
        RebuildLibraryCollections();

        CoverQualityMode = _libraryService.Settings.Current.CoverQualityMode;
        IsListView = _libraryService.Settings.Current.LibraryViewMode == LibraryViewMode.List;
        Updates = new MainWindowUpdatesViewModel(
            statusText => StatusText = statusText,
            ScheduleStatusClear);

        StatusText = Loc.T("LoadingLibrary");
        LoadCachedGames();
        _ = RefreshLibraryCommand.ExecuteAsync(null);
    }

    public ObservableCollection<GameItemViewModel> Games { get; }
    public ObservableCollection<PlatformFilterItem> PlatformFilters { get; }
    public ObservableCollection<SortOptionItem> SortOptions { get; }
    public ObservableCollection<LibraryCollectionItem> LibraryCollections { get; }
    public ObservableCollection<CollectionMembershipItem> DetailCollectionMemberships { get; }
    public LocalizedStrings Strings { get; }
    public MainWindowUpdatesViewModel Updates { get; }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _gamesCountLabel = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private GameItemViewModel? _selectedGame;

    [ObservableProperty]
    private PlatformFilterItem? _selectedPlatformFilter;

    [ObservableProperty]
    private SortOptionItem? _selectedSortOption;

    [ObservableProperty]
    private LibraryCollectionItem? _selectedLibraryCollection;

    public bool CanManageSelectedCollection =>
        SelectedLibraryCollection?.IsUserCollection == true;

    public bool HasUserCollections => _libraryService.Collections.UserCollections.Count > 0;

    public bool ShowDetailCollections => HasUserCollections && SelectedGame is not null;

    [ObservableProperty]
    private CoverQualityMode _coverQualityMode = CoverQualityMode.Low;

    [ObservableProperty]
    private bool _isListView;

    [ObservableProperty]
    private int _gridColumns = 4;

    [ObservableProperty]
    private int _gridRows = 3;

    private int _effectivePageSize;
    private double _libraryViewportWidth;
    private double _libraryViewportHeight;

    public bool IsGridView => !IsListView;

    public bool ShowDetailCover => CoverQualitySettings.Get(CoverQualityMode).ShowDetailCover;

    private CoverQualityProfile CoverProfile => CoverQualitySettings.Get(CoverQualityMode);

    private int PageSize => _effectivePageSize > 0
        ? _effectivePageSize
        : (IsListView ? CoverProfile.PageSize : Math.Max(1, GridColumns * GridRows));

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

    private void NotifyPaginationChanged()
    {
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    public bool SelectedGameHasCustomCover => SelectedGame?.HasCustomCover == true;

    public bool IsEpicCloudAvailable => _libraryService.IsEpicCloudAvailable;

    public bool IsUbisoftCloudAvailable => _libraryService.IsUbisoftCloudAvailable;

    public bool IsEaCloudAvailable => _libraryService.IsEaCloudAvailable;

    public bool IsRiotCloudAvailable => _libraryService.IsRiotCloudAvailable;

    public bool IsGogCloudAvailable => _libraryService.IsGogCloudAvailable;

    public bool IsRockstarCloudAvailable => _libraryService.IsRockstarCloudAvailable;

    public bool IsXboxCloudAvailable => _libraryService.IsXboxCloudAvailable;

    public bool IsSteamCloudAvailable => _libraryService.IsSteamCloudAvailable;

    public bool IsSteamApiConfigured => _libraryService.IsSteamApiConfigured;

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(_filteredGames.Count / (double)PageSize));

    public bool CanGoPrevious => CurrentPage > 1;

    public bool CanGoNext => CurrentPage < TotalPages;

    [ObservableProperty]
    private int _currentPage = 1;

    partial void OnCurrentPageChanged(int value)
    {
        ApplyCurrentPage();
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedPlatformFilterChanged(PlatformFilterItem? value) => ApplyFilter();
    partial void OnSelectedSortOptionChanged(SortOptionItem? value)
    {
        if (_suppressSortOptionChanged)
            return;

        _userSelectedSort = value?.Option;
        ApplyFilter();
    }
    partial void OnSelectedLibraryCollectionChanged(LibraryCollectionItem? value)
    {
        OnPropertyChanged(nameof(CanManageSelectedCollection));
        ApplyFilter();
    }

    partial void OnCoverQualityModeChanged(CoverQualityMode value)
    {
        OnPropertyChanged(nameof(ShowDetailCover));
        ReleaseAllGameCovers();
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
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

    private GameItemViewModel? _previousSelectedGame;

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
                _libraryService.Metadata);
        }

        OnPropertyChanged(nameof(ShowDetailCover));

        OnPropertyChanged(nameof(SelectedGameTitle));
        OnPropertyChanged(nameof(SelectedGameActionLabel));
        OnPropertyChanged(nameof(SelectedGameHasCustomCover));
        OnPropertyChanged(nameof(ShowDetailCollections));
        RefreshDetailCollectionMemberships();
    }

    public string SelectedGameTitle => SelectedGame?.Title ?? Loc.T("SelectGame");

    public string SelectedGameActionLabel => SelectedGame?.ActionLabel ?? Loc.T("Play");

    [RelayCommand]
    private async Task RefreshLibraryAsync()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource();
        var token = _refreshCts.Token;

        CancelScheduledStatusClear();
        await RunOnUiThreadAsync(() =>
        {
            IsRefreshing = true;
        });

        try
        {
            var progress = new Progress<string>(message =>
                Dispatcher.UIThread.Post(() => StatusText = message));

            await RunOnUiThreadAsync(() => StatusText = Loc.T("ScanningLaunchers"));
            var games = await _libraryService.RefreshLibraryAsync(progress, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();

            await RunOnUiThreadAsync(() =>
            {
                ReleaseAllGameCovers();
                SelectedGame = null;
                _previousSelectedGame = null;

                _allGames = games.Select(g => new GameItemViewModel(g)).ToList();
                ApplyGameMembership();
                _suppressCoverLoading = false;
                RebuildPlatformFilters();
                RebuildLibraryCollections();
                RebuildSortOptions();
                ApplyFilter();

                var epicHint = IsEpicCloudAvailable ? Loc.T("EpicCloudHint") : string.Empty;
                var steamHint = IsSteamCloudAvailable
                    ? IsSteamApiConfigured
                        ? Loc.T("SteamCloudHint")
                        : Loc.T("SteamLocalLibraryHint")
                    : string.Empty;
                var ubisoftHint = IsUbisoftCloudAvailable ? Loc.T("UbisoftCloudHint") : string.Empty;
                var eaHint = IsEaCloudAvailable
                    && _libraryService.EaLibraryCacheStatus is EaLibraryCacheStatus.Available
                        or EaLibraryCacheStatus.DecryptFailedUsingLogs
                        ? Loc.T("EaCloudHint")
                        : string.Empty;
                var riotHint = IsRiotCloudAvailable ? Loc.T("RiotCloudHint") : string.Empty;
                var gogHint = IsGogCloudAvailable ? Loc.T("GogCloudHint") : string.Empty;
                var rockstarHint = IsRockstarCloudAvailable ? Loc.T("RockstarCloudHint") : string.Empty;
                var xboxHint = IsXboxCloudAvailable ? Loc.T("XboxCloudHint") : string.Empty;
                StatusText = Loc.T("GamesInLibrary", _allGames.Count) + steamHint + ubisoftHint + eaHint + riotHint + gogHint + rockstarHint + xboxHint + epicHint;
            });

            StartBackgroundCoverEnrichment();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            await RunOnUiThreadAsync(() => StatusText = string.Empty);
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(MainWindowViewModel),
                operation: "RefreshLibraryAsync",
                exception: ex,
                details: "Top-level library refresh");
            await RunOnUiThreadAsync(() =>
                StatusText = Loc.T("ScanError", ex.Message));
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
                ScheduleStatusClear(TimeSpan.FromSeconds(8));
            });
        }

        if (_modalDepth == 0)
            await OfferOnboardingPromptsAsync();
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
                            StatusText = message;
                    }));

                await _libraryService.EnrichCoversAsync(progress, token, maxCovers: maxCovers);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // A newer refresh started.
            }
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(MainWindowViewModel),
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
                        ScheduleStatusClear(TimeSpan.FromSeconds(2));
                    }
                });
            }
        }, token);
    }

    private async Task OfferOnboardingPromptsAsync()
    {
        await RunOnUiThreadAsync(async () =>
        {
            try
            {
                await OfferSteamApiKeyPromptIfNeededAsync();
                await OfferEaLibraryPromptIfNeededAsync();
                await OfferLegendaryPromptIfNeededAsync();
            }
            catch
            {
                // optional setup prompts must not close the app
            }
        });
    }

    private static async Task RunOnUiThreadAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            await Dispatcher.UIThread.InvokeAsync(action);
    }

    private static async Task RunOnUiThreadAsync(Func<Task> action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            await action();
        else
            await Dispatcher.UIThread.InvokeAsync(action);
    }

    private async Task ShowDialogAsync(Window window)
    {
        _modalDepth++;
        try
        {
            await window.ShowDialog(GetMainWindow());
        }
        finally
        {
            _modalDepth--;
        }
    }

    private async Task ShowEaManualInstallNoticeAsync(string gameTitle)
    {
        var viewModel = new EaManualInstallNoticeViewModel(gameTitle);
        var window = new EaManualInstallNoticeWindow(viewModel);
        await ShowDialogAsync(window);
    }

    private void ResetDevSession()
    {
        _steamApiPromptOffered = false;
        _eaLibraryPromptOffered = false;
        _legendaryPromptOffered = false;
        _replayOnboardingAfterSettings = true;
    }

    private void ScheduleDevRelaunch() => _pendingDevRelaunch = true;

    private void ClearDevLocalDatabase()
    {
        _libraryService.ResetLocalCache();
        _pendingDevClearDatabase = true;
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        try
        {
            var wasEpicConnected = _libraryService.IsEpicConnected;
            await ShowDialogAsync(new SettingsWindow(new SettingsViewModel(
                _libraryService.Settings,
                ResetDevSession,
                ScheduleDevRelaunch,
                ClearDevLocalDatabase)));
            ApplyLocalization();

            if (_pendingDevRelaunch)
            {
                _pendingDevRelaunch = false;
                _libraryService.Dispose();
                DevModeService.ClearLocalLibraryCache();
                DevModeService.RelaunchApp();
                return;
            }

            if (_pendingDevClearDatabase)
            {
                _pendingDevClearDatabase = false;
                await RefreshLibraryCommand.ExecuteAsync(null);
            }
            else if (_replayOnboardingAfterSettings)
            {
                _replayOnboardingAfterSettings = false;
                await RefreshLibraryCommand.ExecuteAsync(null);
            }
            else if (wasEpicConnected != _libraryService.IsEpicConnected)
            {
                await RefreshLibraryCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(MainWindowViewModel),
                operation: "OpenSettingsAsync",
                exception: ex,
                details: "Settings dialog flow");
            StatusText = Loc.T("ScanError", ex.Message);
            ScheduleStatusClear(TimeSpan.FromSeconds(8));
        }
    }

    private async Task OfferEaLibraryPromptIfNeededAsync()
    {
        if (_eaLibraryPromptOffered
            || !_libraryService.ShouldOfferEaLibraryPrompt
            || _libraryService.Settings.Current.DismissEaLibraryPrompt)
        {
            return;
        }

        _eaLibraryPromptOffered = true;
        await Task.Delay(350);

        var viewModel = new EaLibraryPromptViewModel(
            _libraryService.Settings,
            _libraryService.EaLibraryCacheStatus);
        var window = new EaLibraryPromptWindow(viewModel);
        await ShowDialogAsync(window);

        if (viewModel.Choice == EaLibraryPromptChoice.OpenEaApp)
            await LaunchEaAndRefreshLibraryAsync();
    }

    private async Task LaunchEaAndRefreshLibraryAsync()
    {
        EaCatalogReader.InvalidateCache();
        var baselineStatus = EaCatalogReader.GetCacheStatus();
        var baselineLogCount = EaCatalogReader.GetLogLibraryEntryCount();

        EaDesktopSyncHelper.LaunchEaDesktop();
        CancelScheduledStatusClear();
        StatusText = Loc.T("WaitingEaAppLaunch");

        if (await EaDesktopSyncHelper.WaitForEaDesktopProcessAsync(TimeSpan.FromSeconds(45)))
        {
            var progress = new Progress<string>(message => StatusText = message);
            await EaDesktopSyncHelper.WaitForLibraryUpdateAsync(
                baselineStatus,
                baselineLogCount,
                progress);
        }

        await RefreshLibraryCommand.ExecuteAsync(null);
    }

    private async Task OfferLegendaryPromptIfNeededAsync()
    {
        if (_legendaryPromptOffered
            || !_libraryService.ShouldOfferLegendaryPrompt)
        {
            return;
        }

        _legendaryPromptOffered = true;
        await Task.Delay(350);

        var viewModel = new LegendaryPromptViewModel(_libraryService.Settings);
        var window = new LegendaryPromptWindow(viewModel);
        await ShowDialogAsync(window);

        if (viewModel.Choice == LegendaryPromptChoice.ConnectEpic)
        {
            try
            {
                CancelScheduledStatusClear();
                StatusText = Loc.T("PreparingEpicLibrary");
                await EpicAuthService.SignInAsync(_libraryService.Settings, GetMainWindow());
                await RefreshLibraryCommand.ExecuteAsync(null);
                StatusText = Loc.T("EpicAuthCompleted");
                ScheduleStatusClear(TimeSpan.FromSeconds(8));
            }
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(MainWindowViewModel),
                    operation: nameof(OfferLegendaryPromptIfNeededAsync),
                    exception: ex,
                    platform: Platform.Epic);
                StatusText = Loc.T("EpicConnectFailed", ex.Message);
                ScheduleStatusClear(TimeSpan.FromSeconds(8));
            }
        }
        else if (viewModel.Choice == LegendaryPromptChoice.OpenGuide)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/derrod/legendary",
                    UseShellExecute = true
                });
            }
            catch
            {
                // optional
            }
        }
    }

    private async Task OfferSteamApiKeyPromptIfNeededAsync()
    {
        if (_steamApiPromptOffered
            || IsSteamApiConfigured
            || !SteamLocalAccountReader.IsSteamInstalled
            || _libraryService.Settings.Current.DismissSteamApiKeyPrompt)
        {
            return;
        }

        _steamApiPromptOffered = true;
        await Task.Delay(350);

        var viewModel = new SteamApiKeyPromptViewModel(_libraryService.Settings);
        var window = new SteamApiKeyPromptWindow(viewModel);
        await ShowDialogAsync(window);

        if (viewModel.Choice == SteamApiKeyPromptChoice.Configure)
            await OpenSteamSetupAsync();
    }

    private async Task OpenSteamSetupAsync()
    {
        var window = new SteamSetupWindow(new SteamSetupViewModel(_libraryService.Settings));
        await ShowDialogAsync(window);

        if (_libraryService.Settings.Current.IsSteamApiConfigured)
            await RefreshLibraryCommand.ExecuteAsync(null);
    }

    private void PersistLibraryViewMode()
    {
        var current = _libraryService.Settings.Current;
        var mode = IsListView ? LibraryViewMode.List : LibraryViewMode.Grid;
        if (current.LibraryViewMode == mode)
            return;

        var updated = current.Clone();
        updated.LibraryViewMode = mode;
        _libraryService.Settings.Save(updated);
    }

    private static Window GetMainWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow ?? throw new InvalidOperationException(Loc.T("MainWindowUnavailable"));

        throw new InvalidOperationException(Loc.T("MainWindowUnavailable"));
    }

    [RelayCommand]
    private void SelectGame(GameItemViewModel? game) =>
        SelectedGame = ReferenceEquals(SelectedGame, game) ? null : game;

    [RelayCommand]
    private async Task LaunchSelectedGameAsync()
    {
        if (SelectedGame is null)
        {
            StatusText = Loc.T("SelectGameFirst");
            ScheduleStatusClear(TimeSpan.Zero);
            return;
        }

        try
        {
            if (!SelectedGame.Source.IsInstalled
                && SelectedGame.Platform == Platform.Epic
                && SelectedGame.Source.LaunchSpec.Kind == "protocol"
                && !string.IsNullOrWhiteSpace(SelectedGame.Source.LaunchSpec.Value)
                && LegendaryClient.IsEpicLauncherInstalled())
            {
                CancelScheduledStatusClear();
                if (!EpicLauncherClient.IsEpicLauncherRunning())
                    StatusText = Loc.T("EpicLauncherInstallWaiting");

                await EpicLauncherClient.StartInstallAsync(SelectedGame.Source.LaunchSpec.Value);
                StatusText = Loc.T("EpicLauncherInstallStarted", SelectedGame.Title);
                ScheduleStatusClear(TimeSpan.FromSeconds(8));
                return;
            }

            if (!SelectedGame.Source.IsInstalled && SelectedGame.Platform == Platform.Riot)
            {
                RiotLauncherClient.StartInstall(SelectedGame.Source);
                StatusText = Loc.T("RiotClientInstallStarted", SelectedGame.Title);
                ScheduleStatusClear(TimeSpan.FromSeconds(8));
                return;
            }

            if (!SelectedGame.Source.IsInstalled && SelectedGame.Platform == Platform.Rockstar)
            {
                RockstarLauncherClient.StartInstall(SelectedGame.Source.PlatformGameId
                    ?? throw new InvalidOperationException(Loc.T("NoLaunchMethod")));
                StatusText = Loc.T("RockstarLauncherInstallStarted", SelectedGame.Title);
                ScheduleStatusClear(TimeSpan.FromSeconds(8));
                return;
            }

            if (!SelectedGame.Source.IsInstalled && SelectedGame.Platform == Platform.GamePass)
            {
                StatusText = Loc.T("XboxInstallStarted", SelectedGame.Title);
                var pfn = SelectedGame.Source.PlatformGameId;
                var storeProductId = await XboxInstallClient.ResolveStoreProductIdAsync(pfn);
                XboxInstallClient.StartInstall(storeProductId, pfn);
                ScheduleStatusClear(TimeSpan.FromSeconds(8));
                return;
            }

            if (!SelectedGame.Source.IsInstalled && SelectedGame.Platform == Platform.Ea)
            {
                await ShowEaManualInstallNoticeAsync(SelectedGame.Title);
                return;
            }

            _libraryService.LaunchGame(SelectedGame.Source);
            SelectedGame.RefreshLaunchState();
            if (_userSelectedSort is null)
                RebuildSortOptions();
            ApplyFilter();
            StatusText = SelectedGame.Source.IsInstalled
                ? Loc.T("LaunchingGame", SelectedGame.Title)
                : Loc.T("StartingInstallLaunch", SelectedGame.Title);
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(MainWindowViewModel),
                operation: "LaunchSelectedGameAsync",
                exception: ex,
                platform: SelectedGame?.Platform,
                details: SelectedGame?.Source.Id);
            StatusText = Loc.T("LaunchFailed", ex.Message);
        }
        finally
        {
            if (SelectedGame is null || SelectedGame.Source.IsInstalled)
                ScheduleStatusClear(TimeSpan.Zero);
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
            StatusText = Loc.T("SelectGameForCover");
            ScheduleStatusClear(TimeSpan.FromSeconds(4));
            return;
        }

        try
        {
            var files = await GetMainWindow().StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Loc.T("ChangeCoverDialogTitle"),
                AllowMultiple = false,
                FileTypeFilter = [FilePickerFileTypes.ImageAll]
            });

            if (files.Count == 0)
                return;

            var localPath = files[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(localPath)
                || !_libraryService.TrySetCustomCover(SelectedGame.Source, localPath))
            {
                StatusText = Loc.T("InvalidCoverImage");
                ScheduleStatusClear(TimeSpan.FromSeconds(4));
                return;
            }

            var profile = CoverProfile;
            await SelectedGame.ApplyCoverFromPathAsync(
                SelectedGame.Source.CoverPath!,
                profile.DetailDecodeWidth,
                profile.Interpolation);
            ApplyVisibleCovers();
            OnPropertyChanged(nameof(SelectedGameHasCustomCover));
            StatusText = Loc.T("CoverUpdated", SelectedGame.Title);
            ScheduleStatusClear(TimeSpan.FromSeconds(4));
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(MainWindowViewModel),
                operation: "ChangeCustomCoverAsync",
                exception: ex,
                platform: SelectedGame?.Platform,
                details: SelectedGame?.Source.Id);
            StatusText = Loc.T("CoverUpdateFailedDetail", ex.Message);
            ScheduleStatusClear(TimeSpan.FromSeconds(6));
        }
    }

    [RelayCommand]
    private async Task ResetCustomCoverAsync()
    {
        if (SelectedGame is null || !SelectedGame.HasCustomCover)
            return;

        try
        {
            StatusText = Loc.T("ResettingCover", SelectedGame.Title);
            SelectedGame.ReleaseCover();

            var path = await _libraryService.TryResetCustomCoverAsync(SelectedGame.Source);
            if (path is not null)
            {
                var profile = CoverProfile;
                await SelectedGame.ApplyCoverFromPathAsync(path, profile.DetailDecodeWidth, profile.Interpolation);
            }

            ApplyVisibleCovers();
            OnPropertyChanged(nameof(SelectedGameHasCustomCover));
            StatusText = path is null
                ? Loc.T("CoverResetNoReplacement", SelectedGame.Title)
                : Loc.T("CoverReset", SelectedGame.Title);
            ScheduleStatusClear(TimeSpan.FromSeconds(4));
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(MainWindowViewModel),
                operation: "ResetCustomCoverAsync",
                exception: ex,
                platform: SelectedGame?.Platform,
                details: SelectedGame?.Source.Id);
            StatusText = Loc.T("CoverUpdateFailedDetail", ex.Message);
            ScheduleStatusClear(TimeSpan.FromSeconds(6));
        }
    }

    [RelayCommand]
    private void ToggleFavorite(GameItemViewModel? game)
    {
        if (game is null)
            return;

        game.IsFavorite = !game.IsFavorite;
        _libraryService.ToggleFavorite(game.Source);
        RebuildLibraryCollections();
        ApplyFilter();
        StatusText = game.IsFavorite
            ? Loc.T("AddedToFavorites", game.Title)
            : Loc.T("RemovedFromFavorites", game.Title);
        ScheduleStatusClear(TimeSpan.Zero);
    }

    private void LoadCachedGames()
    {
        var cached = _libraryService.LoadCachedGames();
        if (cached.Count == 0)
            return;

        _allGames = cached.Select(g => new GameItemViewModel(g)).ToList();
        ApplyGameMembership();
        RebuildPlatformFilters();
        RebuildLibraryCollections();
        RebuildSortOptions();
        ApplyFilter();
        _suppressCoverLoading = false;
        ApplyVisibleCovers();
        StatusText = Loc.T("GamesInCache", _allGames.Count);
    }

    private void ApplyGameMembership()
    {
        var collections = _libraryService.Collections;
        foreach (var game in _allGames)
            game.SetCollectionIds(collections.GetCollectionIdsForGame(game.Source.Id));
    }

    private void RebuildLibraryCollections()
    {
        var selectedKind = SelectedLibraryCollection?.Kind ?? LibraryViewKind.All;
        var selectedCollectionId = SelectedLibraryCollection?.CollectionId;

        LibraryCollections.Clear();

        var favoriteCount = _allGames.Count(g => g.IsFavorite);
        var installedCount = _allGames.Count(g => g.Source.IsInstalled);

        LibraryCollections.Add(new LibraryCollectionItem(
            LibraryViewKind.All,
            Loc.T("AllGames", _allGames.Count),
            null));
        LibraryCollections.Add(new LibraryCollectionItem(
            LibraryViewKind.Favorites,
            Loc.T("FavoritesCount", favoriteCount),
            null));
        LibraryCollections.Add(new LibraryCollectionItem(
            LibraryViewKind.Installed,
            Loc.T("InstalledCount", installedCount),
            null));

        foreach (var collection in _libraryService.Collections.UserCollections)
        {
            var count = _libraryService.Collections.GetCollectionGameCount(collection.Id);
            LibraryCollections.Add(new LibraryCollectionItem(
                LibraryViewKind.UserCollection,
                Loc.T("CollectionWithCount", collection.Name, count),
                collection.Id));
        }

        SelectedLibraryCollection =
            LibraryCollections.FirstOrDefault(item =>
                item.Kind == selectedKind
                && (item.Kind != LibraryViewKind.UserCollection
                    || string.Equals(item.CollectionId, selectedCollectionId, StringComparison.Ordinal)))
            ?? LibraryCollections[0];

        OnPropertyChanged(nameof(HasUserCollections));
        OnPropertyChanged(nameof(CanManageSelectedCollection));
        OnPropertyChanged(nameof(ShowDetailCollections));
        RefreshDetailCollectionMemberships();
    }

    private void RefreshDetailCollectionMemberships()
    {
        DetailCollectionMemberships.Clear();
        if (SelectedGame is null)
            return;

        foreach (var collection in _libraryService.Collections.UserCollections)
        {
            DetailCollectionMemberships.Add(new CollectionMembershipItem(
                collection.Id,
                collection.Name,
                SelectedGame.IsInCollection(collection.Id)));
        }
    }

    private LibraryViewState BuildLibraryViewState()
    {
        var selected = SelectedLibraryCollection;
        if (selected is null || selected.Kind == LibraryViewKind.All)
            return new LibraryViewState(LibraryViewKind.All);

        return selected.Kind == LibraryViewKind.UserCollection
            ? new LibraryViewState(LibraryViewKind.UserCollection, selected.CollectionId)
            : new LibraryViewState(selected.Kind);
    }

    private async Task<string?> PromptCollectionNameAsync(string title, string prompt, string initialName = "")
    {
        var viewModel = new CollectionNameDialogViewModel(title, prompt, initialName);
        var window = new CollectionNameDialog { DataContext = viewModel };
        await ShowDialogAsync(window);
        return viewModel.Confirmed ? viewModel.Name.Trim() : null;
    }

    [RelayCommand]
    private async Task AddCustomGameAsync()
    {
        var viewModel = new AddCustomGameDialogViewModel(_libraryService, GetMainWindow);
        var window = new AddCustomGameDialog { DataContext = viewModel };
        await ShowDialogAsync(window);
        if (!viewModel.Confirmed || viewModel.CreatedGame is null)
            return;

        var game = viewModel.CreatedGame;
        if (_allGames.Any(item => string.Equals(item.Source.Id, game.Id, StringComparison.Ordinal)))
            return;

        var item = new GameItemViewModel(game);
        item.SetCollectionIds(_libraryService.Collections.GetCollectionIdsForGame(game.Id));
        _allGames.Add(item);

        RebuildPlatformFilters();
        RebuildSortOptions();
        ApplyFilter();
        SelectedGame = item;
        StatusText = Loc.T("CustomGameAdded", game.Title);
        ScheduleStatusClear(TimeSpan.FromSeconds(4));
    }

    [RelayCommand]
    private async Task CreateCollectionAsync()
    {
        var name = await PromptCollectionNameAsync(
            Loc.T("NewCollection"),
            Loc.T("CollectionNamePrompt"));
        if (string.IsNullOrWhiteSpace(name))
            return;

        var collection = _libraryService.Collections.Create(name);
        RebuildLibraryCollections();
        SelectedLibraryCollection = LibraryCollections.FirstOrDefault(item =>
            item.Kind == LibraryViewKind.UserCollection
            && string.Equals(item.CollectionId, collection.Id, StringComparison.Ordinal));
        StatusText = Loc.T("CollectionCreated", collection.Name);
        ScheduleStatusClear(TimeSpan.FromSeconds(4));
    }

    [RelayCommand]
    private async Task RenameCollectionAsync()
    {
        if (SelectedLibraryCollection?.IsUserCollection != true
            || string.IsNullOrWhiteSpace(SelectedLibraryCollection.CollectionId))
            return;

        var current = _libraryService.Collections.UserCollections
            .FirstOrDefault(c => c.Id == SelectedLibraryCollection.CollectionId);
        if (current is null)
            return;

        var name = await PromptCollectionNameAsync(
            Loc.T("RenameCollection"),
            Loc.T("CollectionNamePrompt"),
            current.Name);
        if (string.IsNullOrWhiteSpace(name))
            return;

        _libraryService.Collections.Rename(current.Id, name);
        RebuildLibraryCollections();
        SelectedLibraryCollection = LibraryCollections.FirstOrDefault(item =>
            item.Kind == LibraryViewKind.UserCollection
            && string.Equals(item.CollectionId, current.Id, StringComparison.Ordinal));
        StatusText = Loc.T("CollectionRenamed", name);
        ScheduleStatusClear(TimeSpan.FromSeconds(4));
    }

    [RelayCommand]
    private async Task DeleteCollectionAsync()
    {
        if (SelectedLibraryCollection?.IsUserCollection != true
            || string.IsNullOrWhiteSpace(SelectedLibraryCollection.CollectionId))
            return;

        var current = _libraryService.Collections.UserCollections
            .FirstOrDefault(c => c.Id == SelectedLibraryCollection.CollectionId);
        if (current is null)
            return;

        var viewModel = new CollectionConfirmDialogViewModel(
            Loc.T("DeleteCollection"),
            Loc.T("DeleteCollectionConfirm", current.Name));
        var window = new CollectionConfirmDialog { DataContext = viewModel };
        await ShowDialogAsync(window);
        if (!viewModel.Confirmed)
            return;

        _libraryService.Collections.Delete(current.Id);
        ApplyGameMembership();
        RebuildLibraryCollections();
        ApplyFilter();
        StatusText = Loc.T("CollectionDeleted", current.Name);
        ScheduleStatusClear(TimeSpan.FromSeconds(4));
    }

    [RelayCommand]
    private void ToggleGameInCollection(CollectionToggleRequest? request)
    {
        if (request is null)
            return;

        ToggleGameInCollection(request.Game, request.CollectionId);
    }

    public void ToggleGameInCollection(GameItemViewModel game, string collectionId)
    {
        var collection = _libraryService.Collections.UserCollections
            .FirstOrDefault(c => c.Id == collectionId);
        if (collection is null)
            return;

        var wasMember = game.IsInCollection(collectionId);
        if (wasMember)
            _libraryService.Collections.RemoveGame(collectionId, game.Source.Id);
        else
            _libraryService.Collections.AddGame(collectionId, game.Source.Id);

        game.SetCollectionMembership(collectionId, !wasMember);
        RebuildLibraryCollections();
        ApplyFilter();
        RefreshDetailCollectionMemberships();

        StatusText = wasMember
            ? Loc.T("RemovedFromCollection", game.Title, collection.Name)
            : Loc.T("AddedToCollection", game.Title, collection.Name);
        ScheduleStatusClear(TimeSpan.FromSeconds(4));
    }

    public IReadOnlyList<CollectionMembershipItem> GetContextMenuCollections(GameItemViewModel game) =>
        _libraryService.Collections.UserCollections
            .Select(collection => new CollectionMembershipItem(
                collection.Id,
                collection.Name,
                game.IsInCollection(collection.Id)))
            .ToList();

    private void RebuildPlatformFilters()
    {
        var selected = SelectedPlatformFilter?.Platform;
        PlatformFilters.Clear();
        PlatformFilters.Add(new PlatformFilterItem(Loc.T("AllPlatforms"), null, selected is null));

        foreach (var platform in _allGames.Select(g => g.Platform).Distinct().OrderBy(p => PlatformLabels.Get(p)))
        {
            var count = _allGames.Count(g => g.Platform == platform);
            PlatformFilters.Add(new PlatformFilterItem($"{PlatformLabels.Get(platform)} ({count})", platform, selected == platform));
        }

        SelectedPlatformFilter = PlatformFilters.FirstOrDefault(f => f.IsSelected) ?? PlatformFilters[0];
    }

    private void RebuildSortOptions()
    {
        var selected = _userSelectedSort ?? LibraryFilterPipeline.ResolveDefaultSort(_allGames);
        SortOptions.Clear();
        SortOptions.Add(new SortOptionItem(Loc.T("SortLastPlayedDesc"), SortOption.LastPlayedDesc));
        SortOptions.Add(new SortOptionItem(Loc.T("SortTitleAsc"), SortOption.TitleAsc));
        SortOptions.Add(new SortOptionItem(Loc.T("SortTitleDesc"), SortOption.TitleDesc));
        SortOptions.Add(new SortOptionItem(Loc.T("SortPlatform"), SortOption.Platform));
        SortOptions.Add(new SortOptionItem(Loc.T("SortInstalledFirst"), SortOption.InstalledFirst));
        SortOptions.Add(new SortOptionItem(Loc.T("SortPlaytimeDesc"), SortOption.PlaytimeDesc));

        _suppressSortOptionChanged = true;
        SelectedSortOption = SortOptions.FirstOrDefault(s => s.Option == selected) ?? SortOptions[0];
        _suppressSortOptionChanged = false;
    }

    private void ApplyFilter()
    {
        var view = BuildLibraryViewState();
        IReadOnlySet<string>? collectionGameIds = null;
        if (view.Kind == LibraryViewKind.UserCollection && view.UserCollectionId is not null)
            collectionGameIds = _libraryService.Collections.GetGameIdsForCollection(view.UserCollectionId);

        _filteredGames = LibraryFilterPipeline.Apply(
            _allGames,
            view,
            SelectedPlatformFilter?.Platform,
            SearchText,
            _userSelectedSort ?? LibraryFilterPipeline.ResolveDefaultSort(_allGames),
            collectionGameIds);

        CurrentPage = 1;
        ApplyCurrentPage();

        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();

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
                _libraryService.Metadata);
        }
    }

    private void ReleaseAllGameCovers()
    {
        foreach (var game in _allGames)
            game.ReleaseCover();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(ApplyLocalization);

    private void ApplyLocalization()
    {
        Strings.Refresh();
        RebuildSortOptions();
        RebuildPlatformFilters();
        RebuildLibraryCollections();
        Updates.RefreshLocalizedText();

        foreach (var game in _allGames)
            game.ApplyLocalization();

        var previousQuality = CoverQualityMode;
        CoverQualityMode = _libraryService.Settings.Current.CoverQualityMode;
        IsListView = _libraryService.Settings.Current.LibraryViewMode == LibraryViewMode.List;
        OnPropertyChanged(nameof(ShowDetailCover));
        if (previousQuality != CoverQualityMode)
            ReleaseAllGameCovers();
        OnPropertyChanged(nameof(IsGridView));
        OnPropertyChanged(nameof(SelectedGameHasCustomCover));

        OnPropertyChanged(nameof(SelectedGameTitle));
        OnPropertyChanged(nameof(SelectedGameActionLabel));
        ApplyFilter();
        ApplyVisibleCovers();
    }

    private void ScheduleStatusClear(TimeSpan delay)
    {
        CancelScheduledStatusClear();
        _statusClearCts = new CancellationTokenSource();
        var token = _statusClearCts.Token;
        _ = ClearStatusAfterDelayAsync(delay, token);
    }

    private void CancelScheduledStatusClear()
    {
        _statusClearCts?.Cancel();
        _statusClearCts?.Dispose();
        _statusClearCts = null;
    }

    private async Task ClearStatusAfterDelayAsync(TimeSpan delay, CancellationToken token)
    {
        try
        {
            await Task.Delay(delay, token);
            await Dispatcher.UIThread.InvokeAsync(() => StatusText = string.Empty);
        }
        catch (TaskCanceledException)
        {
            // A new status message was scheduled before clearing.
        }
    }

}

public sealed class PlatformFilterItem
{
    public PlatformFilterItem(string label, Platform? platform, bool isSelected)
    {
        Label = label;
        Platform = platform;
        IsSelected = isSelected;
    }

    public string Label { get; }
    public Platform? Platform { get; }
    public bool IsSelected { get; }
}

public sealed class SortOptionItem
{
    public SortOptionItem(string label, SortOption option)
    {
        Label = label;
        Option = option;
    }

    public string Label { get; }
    public SortOption Option { get; }
}
