using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Localization;
using OpenGameHUB.Models;
using OpenGameHUB.Services;
using OpenGameHUB.Views;

namespace OpenGameHUB.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const int PageSize = 24;

    private readonly GameLibraryService _libraryService = new();
    private List<GameItemViewModel> _allGames = [];
    private List<GameItemViewModel> _filteredGames = [];
    private CancellationTokenSource? _statusClearCts;
    private bool _offeredSteamSetup;

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

        ShowGridCovers = _libraryService.Settings.Current.ShowGridCovers;

        StatusText = Loc.T("LoadingLibrary");
        LoadCachedGames();
        _ = RefreshLibraryCommand.ExecuteAsync(null);
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
    private bool _showFavoritesOnly;

    [ObservableProperty]
    private bool _showInstalledOnly;

    [ObservableProperty]
    private bool _showGridCovers = true;

    public bool IsLegendaryAvailable => _libraryService.IsLegendaryAvailable;

    public bool IsUbisoftCloudAvailable => _libraryService.IsUbisoftCloudAvailable;

    public bool IsSteamCloudAvailable => _libraryService.IsSteamCloudAvailable;

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

    private GameItemViewModel? _previousSelectedGame;

    partial void OnSelectedGameChanged(GameItemViewModel? value)
    {
        if (!ReferenceEquals(_previousSelectedGame, value))
            _previousSelectedGame?.ReleaseCover();

        _previousSelectedGame = value;

        foreach (var game in _allGames)
            game.IsSelected = ReferenceEquals(game, value);

        if (value is not null && !value.HasCover)
            _ = value.LoadCoverAsync();

        OnPropertyChanged(nameof(SelectedGameTitle));
        OnPropertyChanged(nameof(SelectedGameActionLabel));
    }

    public string SelectedGameTitle => SelectedGame?.Title ?? Loc.T("SelectGame");

    public string SelectedGameActionLabel => SelectedGame?.ActionLabel ?? Loc.T("Play");

    [RelayCommand]
    private async Task RefreshLibraryAsync()
    {
        if (IsRefreshing)
            return;

        try
        {
            CancelScheduledStatusClear();
            IsRefreshing = true;
            var progress = new Progress<string>(message => StatusText = message);
            StatusText = Loc.T("ScanningLaunchers");
            var games = await _libraryService.RefreshLibraryAsync(progress);
            _allGames = games.Select(g => new GameItemViewModel(g)).ToList();
            RebuildPlatformFilters();
            ApplyFilter();

            var legendaryHint = IsLegendaryAvailable ? Loc.T("EpicCloudHint") : string.Empty;
            var steamHint = IsSteamCloudAvailable ? Loc.T("SteamCloudHint") : string.Empty;
            var steamSetupHint = !IsSteamCloudAvailable && SteamLocalAccountReader.IsSteamInstalled
                ? Loc.T("SteamSetupPrompt")
                : string.Empty;
            var ubisoftHint = IsUbisoftCloudAvailable ? Loc.T("UbisoftCloudHint") : string.Empty;
            StatusText = Loc.T("GamesInLibrary", _allGames.Count) + steamSetupHint + steamHint + ubisoftHint + legendaryHint;

            if (!_offeredSteamSetup
                && !_libraryService.Settings.Current.IsSteamApiConfigured
                && SteamLocalAccountReader.IsSteamInstalled)
            {
                _offeredSteamSetup = true;
                _ = OfferSteamSetupAsync();
            }
        }
        catch (Exception ex)
        {
            StatusText = Loc.T("ScanError", ex.Message);
        }
        finally
        {
            IsRefreshing = false;
            ScheduleStatusClear(TimeSpan.Zero);
        }
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var window = new SettingsWindow(new SettingsViewModel(_libraryService.Settings));
        await window.ShowDialog(GetMainWindow());
        ApplyLocalization();
    }

    private async Task OfferSteamSetupAsync()
    {
        await Task.Delay(400);
        var window = new SteamSetupWindow(new SteamSetupViewModel(_libraryService.Settings));
        await window.ShowDialog(GetMainWindow());

        if (_libraryService.Settings.Current.IsSteamApiConfigured)
            await RefreshLibraryCommand.ExecuteAsync(null);
    }

    private static Window GetMainWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow ?? throw new InvalidOperationException(Loc.T("MainWindowUnavailable"));

        throw new InvalidOperationException(Loc.T("MainWindowUnavailable"));
    }

    [RelayCommand]
    private void SelectGame(GameItemViewModel? game) => SelectedGame = game;

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
            ScheduleStatusClear(TimeSpan.Zero);
        }
    }

    [RelayCommand]
    private void LaunchGame(GameItemViewModel? game)
    {
        if (game is null)
            return;

        SelectedGame = game;
        LaunchSelectedGame();
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
        {
            filtered = filtered.Where(g =>
                g.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                g.PlatformLabel.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

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

        ApplyGridCovers();
    }

    private void ApplyGridCovers()
    {
        var pageGames = Games.ToHashSet();

        foreach (var game in _allGames)
        {
            if (pageGames.Contains(game))
                continue;

            game.ShowCoverInGrid = false;
            if (!ReferenceEquals(game, SelectedGame))
                game.ReleaseCover();
        }

        if (!ShowGridCovers)
        {
            foreach (var game in pageGames)
                game.ShowCoverInGrid = false;
            return;
        }

        foreach (var game in pageGames)
        {
            game.ShowCoverInGrid = true;
            if (!game.HasCover)
                _ = game.LoadCoverAsync();
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(ApplyLocalization);

    private void ApplyLocalization()
    {
        Strings.Refresh();
        RebuildSortOptions();
        RebuildPlatformFilters();

        foreach (var game in _allGames)
            game.ApplyLocalization();

        ShowGridCovers = _libraryService.Settings.Current.ShowGridCovers;

        OnPropertyChanged(nameof(SelectedGameTitle));
        OnPropertyChanged(nameof(SelectedGameActionLabel));
        ApplyFilter();
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
