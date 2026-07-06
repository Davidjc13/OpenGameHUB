using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Localization;
using OpenGameHUB.Providers.Ea;
using OpenGameHUB.Providers.Epic;
using OpenGameHUB.Providers.Rockstar;
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
    private CancellationTokenSource? _appUpdateCheckCts;
    private CancellationTokenSource? _appUpdateInstallCts;
    private AppReleaseInfo? _pendingAppUpdate;
    private string? _dismissedAppUpdateTag;
    private bool _steamApiPromptOffered;
    private bool _eaLibraryPromptOffered;
    private bool _legendaryPromptOffered;
    private int _modalDepth;
    private bool _replayOnboardingAfterSettings;
    private bool _pendingDevRelaunch;
    private bool _pendingDevClearDatabase;
    private bool _suppressCoverLoading = true;

    public MainWindowViewModel()
    {
        Loc.Service.Initialize(_libraryService.Settings.Current.Language);
        Loc.Service.LanguageChanged += OnLanguageChanged;

        Games = new ObservableCollection<GameItemViewModel>();
        PlatformFilters = new ObservableCollection<PlatformFilterItem>();
        SortOptions = new ObservableCollection<SortOptionItem>();
        Strings = new LocalizedStrings();

        RebuildSortOptions();
        RebuildPlatformFilters();

        CoverQualityMode = _libraryService.Settings.Current.CoverQualityMode;
        IsListView = _libraryService.Settings.Current.LibraryViewMode == LibraryViewMode.List;

        AppVersionText = Loc.T("AppCurrentVersion", AppUpdateService.CurrentVersion);
        StatusText = Loc.T("LoadingLibrary");
        LoadCachedGames();
        _ = RefreshLibraryCommand.ExecuteAsync(null);
        _ = CheckForAppUpdateAsync();
        StartPeriodicAppUpdateChecks();
    }

    public ObservableCollection<GameItemViewModel> Games { get; }
    public ObservableCollection<PlatformFilterItem> PlatformFilters { get; }
    public ObservableCollection<SortOptionItem> SortOptions { get; }
    public LocalizedStrings Strings { get; }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _appVersionText = string.Empty;

    [ObservableProperty]
    private string _gamesCountLabel = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _isAppUpdateBannerVisible;

    [ObservableProperty]
    private string _appUpdateBannerTag = string.Empty;

    [ObservableProperty]
    private string _appUpdateBannerText = string.Empty;

    [ObservableProperty]
    private bool _isAppUpdateInstalling;

    [ObservableProperty]
    private double _appUpdateProgress;

    public bool CanInstallAppUpdate => _pendingAppUpdate is not null && !IsAppUpdateInstalling;

    [ObservableProperty]
    private GameItemViewModel? _selectedGame;

    [ObservableProperty]
    private PlatformFilterItem? _selectedPlatformFilter;

    [ObservableProperty]
    private SortOptionItem? _selectedSortOption;

    [ObservableProperty]
    private bool _showFavoritesOnly;

    [ObservableProperty]
    private bool _showInstalledOnly;

    [ObservableProperty]
    private CoverQualityMode _coverQualityMode = CoverQualityMode.Low;

    [ObservableProperty]
    private bool _isListView;

    public bool IsGridView => !IsListView;

    public bool ShowDetailCover => CoverQualitySettings.Get(CoverQualityMode).ShowDetailCover;

    private CoverQualityProfile CoverProfile => CoverQualitySettings.Get(CoverQualityMode);

    private int PageSize => CoverProfile.PageSize;

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
    partial void OnSelectedSortOptionChanged(SortOptionItem? value) => ApplyFilter();
    partial void OnShowFavoritesOnlyChanged(bool value) => ApplyFilter();
    partial void OnShowInstalledOnlyChanged(bool value) => ApplyFilter();

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
            _suppressCoverLoading = true;
            ReleaseAllGameCovers();
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
                _suppressCoverLoading = false;
                RebuildPlatformFilters();
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
            catch
            {
                // Cover downloads are optional.
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

    private async Task CheckForAppUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (AppUpdateService.IsDevBuild)
            return;

        try
        {
            var release = await AppUpdateService.GetLatestReleaseAsync(cancellationToken);
            if (release is null || !AppUpdateService.IsNewer(release.TagName, AppUpdateService.CurrentVersion))
                return;

            _pendingAppUpdate = release;
            var showBanner = !string.Equals(_dismissedAppUpdateTag, release.TagName, StringComparison.OrdinalIgnoreCase);

            await RunOnUiThreadAsync(() =>
            {
                AppUpdateBannerTag = release.TagName;
                UpdateAppUpdateBannerText();
                IsAppUpdateBannerVisible = showBanner;
                OnPropertyChanged(nameof(CanInstallAppUpdate));
                StatusText = Loc.T("AppUpdateAvailableHint", release.TagName);
                ScheduleStatusClear(TimeSpan.FromSeconds(12));
            });
        }
        catch (OperationCanceledException)
        {
            // optional background check
        }
        catch
        {
            // optional background check
        }
    }

    private void StartPeriodicAppUpdateChecks()
    {
        if (AppUpdateService.IsDevBuild)
            return;

        _appUpdateCheckCts?.Cancel();
        _appUpdateCheckCts = new CancellationTokenSource();
        _ = RunPeriodicAppUpdateChecksAsync(_appUpdateCheckCts.Token);
    }

    private async Task RunPeriodicAppUpdateChecksAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(AppUpdateService.BackgroundCheckInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await CheckForAppUpdateAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // app shutting down
        }
    }

    [RelayCommand]
    private void DismissAppUpdateBanner()
    {
        if (!string.IsNullOrWhiteSpace(AppUpdateBannerTag))
            _dismissedAppUpdateTag = AppUpdateBannerTag;

        IsAppUpdateBannerVisible = false;
    }

    [RelayCommand(CanExecute = nameof(CanInstallAppUpdate))]
    private async Task InstallAppUpdateAsync()
    {
        if (_pendingAppUpdate is null)
            return;

        _appUpdateInstallCts?.Cancel();
        _appUpdateInstallCts = new CancellationTokenSource();
        var cancellationToken = _appUpdateInstallCts.Token;

        IsAppUpdateInstalling = true;
        AppUpdateProgress = 0;
        InstallAppUpdateCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanInstallAppUpdate));

        try
        {
            var progress = new Progress<double>(value =>
            {
                AppUpdateProgress = value;
                StatusText = Loc.T("AppUpdateDownloading", value);
            });

            StatusText = Loc.T("AppUpdateDownloading", 0);
            await AppUpdateService.DownloadAndInstallAsync(
                _pendingAppUpdate,
                progress,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // optional
        }
        catch (Exception ex)
        {
            StatusText = Loc.T("AppUpdateDownloadFailed", ex.Message);
            IsAppUpdateInstalling = false;
            AppUpdateProgress = 0;
            InstallAppUpdateCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanInstallAppUpdate));
        }
    }

    partial void OnIsAppUpdateInstallingChanged(bool value)
    {
        InstallAppUpdateCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanInstallAppUpdate));
    }

    private void UpdateAppUpdateBannerText() =>
        AppUpdateBannerText = string.IsNullOrWhiteSpace(AppUpdateBannerTag)
            ? string.Empty
            : Loc.T("AppUpdateBannerMessage", AppUpdateBannerTag);

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
                LegendaryClient.RunAuth();
            }
            catch
            {
                // optional
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
    private void LaunchSelectedGame()
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
                EpicLauncherClient.StartInstall(SelectedGame.Source.LaunchSpec.Value);
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
                _libraryService.LaunchGame(SelectedGame.Source);
                StatusText = Loc.T("XboxInstallStarted", SelectedGame.Title);
                ScheduleStatusClear(TimeSpan.FromSeconds(8));
                return;
            }

            _libraryService.LaunchGame(SelectedGame.Source);
            StatusText = SelectedGame.Source.IsInstalled
                ? Loc.T("LaunchingGame", SelectedGame.Title)
                : Loc.T("StartingInstallLaunch", SelectedGame.Title);
        }
        catch (Exception ex)
        {
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
        RebuildPlatformFilters();
        ApplyFilter();
        StatusText = Loc.T("GamesInCache", _allGames.Count);
    }

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
        var selected = SelectedSortOption?.Option;
        SortOptions.Clear();
        SortOptions.Add(new SortOptionItem(Loc.T("SortTitleAsc"), SortOption.TitleAsc));
        SortOptions.Add(new SortOptionItem(Loc.T("SortTitleDesc"), SortOption.TitleDesc));
        SortOptions.Add(new SortOptionItem(Loc.T("SortPlatform"), SortOption.Platform));
        SortOptions.Add(new SortOptionItem(Loc.T("SortInstalledFirst"), SortOption.InstalledFirst));
        SortOptions.Add(new SortOptionItem(Loc.T("SortPlaytimeDesc"), SortOption.PlaytimeDesc));
        SelectedSortOption = SortOptions.FirstOrDefault(s => s.Option == selected) ?? SortOptions[0];
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        IEnumerable<GameItemViewModel> filtered = _allGames;

        if (SelectedPlatformFilter?.Platform is Platform platform)
            filtered = filtered.Where(g => g.Platform == platform);

        if (ShowFavoritesOnly)
            filtered = filtered.Where(g => g.IsFavorite);

        if (ShowInstalledOnly)
            filtered = filtered.Where(g => g.Source.IsInstalled);

        if (!string.IsNullOrWhiteSpace(query))
            filtered = filtered.Where(g => GameSearchHelper.MatchesTitle(g.Title, query));

        filtered = (SelectedSortOption?.Option ?? SortOption.TitleAsc) switch
        {
            SortOption.TitleDesc => filtered.OrderByDescending(g => g.Title, StringComparer.OrdinalIgnoreCase),
            SortOption.Platform => filtered.OrderBy(g => g.Platform).ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase),
            SortOption.InstalledFirst => filtered.OrderByDescending(g => g.Source.IsInstalled).ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase),
            SortOption.PlaytimeDesc => filtered.OrderByDescending(g => g.Source.PlaytimeMinutes).ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
        };

        _filteredGames = filtered.ToList();
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
            : Loc.T("ShowingGamesPage", Games.Count, _filteredGames.Count, CurrentPage, TotalPages);

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
        UpdateAppUpdateBannerText();
        AppVersionText = Loc.T("AppCurrentVersion", AppUpdateService.CurrentVersion);

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
