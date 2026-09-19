using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Services.Games;
using OpenGameHUB.Views;

namespace OpenGameHUB.ViewModels.MainWindow;

public partial class MainWindowSidebarViewModel : ViewModelBase
{
    private readonly GameLibraryService _library;
    private readonly Func<Window> _getMainWindow;
    private readonly Func<Window, Task> _showDialogAsync;
    private readonly Action _onFilterChanged;
    private readonly Action<string> _setStatusText;
    private readonly Action<TimeSpan> _scheduleStatusClear;
    private readonly Func<GameItemViewModel?> _getSelectedGame;
    private readonly Action<GameItemViewModel?> _setSelectedGame;
    private readonly Action<IReadOnlyList<GameItemViewModel>> _onGamesMutated;
    private bool _suppressSortOptionChanged;
    private bool _suppressFilterChanged;
    private SortOption? _userSelectedSort;
    private IReadOnlyList<GameItemViewModel> _allGames = [];

    public MainWindowSidebarViewModel(
        GameLibraryService library,
        Func<Window> getMainWindow,
        Func<Window, Task> showDialogAsync,
        Action onFilterChanged,
        Action<string> setStatusText,
        Action<TimeSpan> scheduleStatusClear,
        Func<GameItemViewModel?> getSelectedGame,
        Action<GameItemViewModel?> setSelectedGame,
        Action<IReadOnlyList<GameItemViewModel>> onGamesMutated)
    {
        _library = library;
        _getMainWindow = getMainWindow;
        _showDialogAsync = showDialogAsync;
        _onFilterChanged = onFilterChanged;
        _setStatusText = setStatusText;
        _scheduleStatusClear = scheduleStatusClear;
        _getSelectedGame = getSelectedGame;
        _setSelectedGame = setSelectedGame;
        _onGamesMutated = onGamesMutated;

        PlatformFilters = new ObservableCollection<PlatformFilterItem>();
        SortOptions = new ObservableCollection<SortOptionItem>();
        LibraryCollections = new ObservableCollection<LibraryCollectionItem>();
        DetailCollectionMemberships = new ObservableCollection<CollectionMembershipItem>();

        _suppressFilterChanged = true;
        try
        {
            RebuildSortOptions();
            RebuildPlatformFilters();
            RebuildLibraryCollections();
        }
        finally
        {
            _suppressFilterChanged = false;
        }
    }

    public ObservableCollection<PlatformFilterItem> PlatformFilters { get; }
    public ObservableCollection<SortOptionItem> SortOptions { get; }
    public ObservableCollection<LibraryCollectionItem> LibraryCollections { get; }
    public ObservableCollection<CollectionMembershipItem> DetailCollectionMemberships { get; }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private PlatformFilterItem? _selectedPlatformFilter;

    [ObservableProperty]
    private SortOptionItem? _selectedSortOption;

    [ObservableProperty]
    private LibraryCollectionItem? _selectedLibraryCollection;

    public bool CanManageSelectedCollection =>
        SelectedLibraryCollection?.IsUserCollection == true;

    public bool HasUserCollections => _library.Collections.UserCollections.Count > 0;

    public bool ShowDetailCollections => HasUserCollections && _getSelectedGame() is not null;

    partial void OnSearchTextChanged(string value)
    {
        if (!_suppressFilterChanged)
            _onFilterChanged();
    }

    partial void OnSelectedPlatformFilterChanged(PlatformFilterItem? value)
    {
        if (!_suppressFilterChanged)
            _onFilterChanged();
    }

    partial void OnSelectedSortOptionChanged(SortOptionItem? value)
    {
        if (_suppressSortOptionChanged)
            return;

        _userSelectedSort = value?.Option;
        _onFilterChanged();
    }

    partial void OnSelectedLibraryCollectionChanged(LibraryCollectionItem? value)
    {
        OnPropertyChanged(nameof(CanManageSelectedCollection));
        if (!_suppressFilterChanged)
            _onFilterChanged();
    }

    public void OnSelectedGameChanged()
    {
        OnPropertyChanged(nameof(ShowDetailCollections));
        RefreshDetailCollectionMemberships();
    }

    public void RebuildAll(IReadOnlyList<GameItemViewModel> allGames)
    {
        _allGames = allGames;
        _suppressFilterChanged = true;
        try
        {
            RebuildPlatformFilters();
            RebuildSortOptions();
            RebuildLibraryCollections();
        }
        finally
        {
            _suppressFilterChanged = false;
        }
    }

    public void RebuildPlatformFilters()
    {
        var selected = SelectedPlatformFilter?.Platform;
        PlatformFilters.Clear();
        PlatformFilters.Add(new PlatformFilterItem(Loc.T("AllPlatforms"), null, selected is null));

        foreach (var platform in _allGames.Select(g => g.Platform).Distinct().OrderBy(p => PlatformLabels.Get(p)))
        {
            var count = _allGames.Count(g => g.Platform == platform);
            PlatformFilters.Add(new PlatformFilterItem($"{PlatformLabels.Get(platform)} ({count})", platform, selected == platform));
        }

        SetSelectedPlatformFilterSilently(
            PlatformFilters.FirstOrDefault(f => f.IsSelected) ?? PlatformFilters[0]);
    }

    private void SetSelectedPlatformFilterSilently(PlatformFilterItem? filter)
    {
        if (_suppressFilterChanged)
        {
            SelectedPlatformFilter = filter;
            return;
        }

        _suppressFilterChanged = true;
        SelectedPlatformFilter = filter;
        _suppressFilterChanged = false;
    }

    public void RebuildSortOptions()
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

    public void RebuildLibraryCollections()
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

        foreach (var collection in _library.Collections.UserCollections)
        {
            var count = _library.Collections.GetCollectionGameCount(collection.Id);
            LibraryCollections.Add(new LibraryCollectionItem(
                LibraryViewKind.UserCollection,
                Loc.T("CollectionWithCount", collection.Name, count),
                collection.Id));
        }

        SetSelectedLibraryCollectionSilently(
            LibraryCollections.FirstOrDefault(item =>
                item.Kind == selectedKind
                && (item.Kind != LibraryViewKind.UserCollection
                    || string.Equals(item.CollectionId, selectedCollectionId, StringComparison.Ordinal)))
            ?? LibraryCollections[0]);

        OnPropertyChanged(nameof(HasUserCollections));
        OnPropertyChanged(nameof(CanManageSelectedCollection));
        OnPropertyChanged(nameof(ShowDetailCollections));
        RefreshDetailCollectionMemberships();
    }

    public void RefreshDetailCollectionMemberships()
    {
        DetailCollectionMemberships.Clear();
        var selectedGame = _getSelectedGame();
        if (selectedGame is null)
            return;

        foreach (var collection in _library.Collections.UserCollections)
        {
            DetailCollectionMemberships.Add(new CollectionMembershipItem(
                collection.Id,
                collection.Name,
                selectedGame.IsInCollection(collection.Id)));
        }
    }

    private void SetSelectedLibraryCollectionSilently(LibraryCollectionItem? collection)
    {
        if (_suppressFilterChanged)
        {
            SelectedLibraryCollection = collection;
            return;
        }

        _suppressFilterChanged = true;
        SelectedLibraryCollection = collection;
        _suppressFilterChanged = false;
    }

    public LibraryViewState BuildLibraryViewState()
    {
        var selected = SelectedLibraryCollection;
        if (selected is null || selected.Kind == LibraryViewKind.All)
            return new LibraryViewState(LibraryViewKind.All);

        return selected.Kind == LibraryViewKind.UserCollection
            ? new LibraryViewState(LibraryViewKind.UserCollection, selected.CollectionId)
            : new LibraryViewState(selected.Kind);
    }

    public SortOption ResolveSortOption() =>
        _userSelectedSort ?? LibraryFilterPipeline.ResolveDefaultSort(_allGames);

    public bool HasUserSelectedSort => _userSelectedSort is not null;

    public void ClearUserSelectedSort() => _userSelectedSort = null;

    public IReadOnlyList<CollectionMembershipItem> GetContextMenuCollections(GameItemViewModel game) =>
        _library.Collections.UserCollections
            .Select(collection => new CollectionMembershipItem(
                collection.Id,
                collection.Name,
                game.IsInCollection(collection.Id)))
            .ToList();

    public void ToggleGameInCollection(GameItemViewModel game, string collectionId)
    {
        var collection = _library.Collections.UserCollections
            .FirstOrDefault(c => c.Id == collectionId);
        if (collection is null)
            return;

        var wasMember = game.IsInCollection(collectionId);
        if (wasMember)
            _library.Collections.RemoveGame(collectionId, game.Source.Id);
        else
            _library.Collections.AddGame(collectionId, game.Source.Id);

        game.SetCollectionMembership(collectionId, !wasMember);
        RebuildLibraryCollections();
        _onFilterChanged();
        RefreshDetailCollectionMemberships();

        _setStatusText(wasMember
            ? Loc.T("RemovedFromCollection", game.Title, collection.Name)
            : Loc.T("AddedToCollection", game.Title, collection.Name));
        _scheduleStatusClear(TimeSpan.FromSeconds(4));
    }

    [RelayCommand]
    private void ToggleGameInCollection(CollectionToggleRequest? request)
    {
        if (request is null)
            return;

        ToggleGameInCollection(request.Game, request.CollectionId);
    }

    [RelayCommand]
    private async Task AddCustomGameAsync()
    {
        var viewModel = new AddCustomGameDialogViewModel(_library, _getMainWindow);
        var window = new AddCustomGameDialog { DataContext = viewModel };
        await _showDialogAsync(window);
        if (!viewModel.Confirmed || viewModel.CreatedGame is null)
            return;

        var game = viewModel.CreatedGame;
        if (_allGames.Any(item => string.Equals(item.Source.Id, game.Id, StringComparison.Ordinal)))
            return;

        var item = new GameItemViewModel(game);
        item.SetCollectionIds(_library.Collections.GetCollectionIdsForGame(game.Id));
        var updatedGames = _allGames.ToList();
        updatedGames.Add(item);
        _allGames = updatedGames;

        RebuildPlatformFilters();
        RebuildSortOptions();
        _onGamesMutated(updatedGames);
        _setSelectedGame(item);
        _setStatusText(Loc.T("CustomGameAdded", game.Title));
        _scheduleStatusClear(TimeSpan.FromSeconds(4));
    }

    [RelayCommand]
    private async Task CreateCollectionAsync()
    {
        var name = await PromptCollectionNameAsync(
            Loc.T("NewCollection"),
            Loc.T("CollectionNamePrompt"));
        if (string.IsNullOrWhiteSpace(name))
            return;

        var collection = _library.Collections.Create(name);
        RebuildLibraryCollections();
        SelectedLibraryCollection = LibraryCollections.FirstOrDefault(item =>
            item.Kind == LibraryViewKind.UserCollection
            && string.Equals(item.CollectionId, collection.Id, StringComparison.Ordinal));
        _setStatusText(Loc.T("CollectionCreated", collection.Name));
        _scheduleStatusClear(TimeSpan.FromSeconds(4));
    }

    [RelayCommand]
    private async Task RenameCollectionAsync()
    {
        if (SelectedLibraryCollection?.IsUserCollection != true
            || string.IsNullOrWhiteSpace(SelectedLibraryCollection.CollectionId))
            return;

        var current = _library.Collections.UserCollections
            .FirstOrDefault(c => c.Id == SelectedLibraryCollection.CollectionId);
        if (current is null)
            return;

        var name = await PromptCollectionNameAsync(
            Loc.T("RenameCollection"),
            Loc.T("CollectionNamePrompt"),
            current.Name);
        if (string.IsNullOrWhiteSpace(name))
            return;

        _library.Collections.Rename(current.Id, name);
        RebuildLibraryCollections();
        SelectedLibraryCollection = LibraryCollections.FirstOrDefault(item =>
            item.Kind == LibraryViewKind.UserCollection
            && string.Equals(item.CollectionId, current.Id, StringComparison.Ordinal));
        _setStatusText(Loc.T("CollectionRenamed", name));
        _scheduleStatusClear(TimeSpan.FromSeconds(4));
    }

    [RelayCommand]
    private async Task DeleteCollectionAsync()
    {
        if (SelectedLibraryCollection?.IsUserCollection != true
            || string.IsNullOrWhiteSpace(SelectedLibraryCollection.CollectionId))
            return;

        var current = _library.Collections.UserCollections
            .FirstOrDefault(c => c.Id == SelectedLibraryCollection.CollectionId);
        if (current is null)
            return;

        var viewModel = new CollectionConfirmDialogViewModel(
            Loc.T("DeleteCollection"),
            Loc.T("DeleteCollectionConfirm", current.Name));
        var window = new CollectionConfirmDialog { DataContext = viewModel };
        await _showDialogAsync(window);
        if (!viewModel.Confirmed)
            return;

        _library.Collections.Delete(current.Id);
        foreach (var game in _allGames)
            game.SetCollectionIds(_library.Collections.GetCollectionIdsForGame(game.Source.Id));
        RebuildLibraryCollections();
        _onFilterChanged();
        _setStatusText(Loc.T("CollectionDeleted", current.Name));
        _scheduleStatusClear(TimeSpan.FromSeconds(4));
    }

    private async Task<string?> PromptCollectionNameAsync(string title, string prompt, string initialName = "")
    {
        var viewModel = new CollectionNameDialogViewModel(title, prompt, initialName);
        var window = new CollectionNameDialog { DataContext = viewModel };
        await _showDialogAsync(window);
        return viewModel.Confirmed ? viewModel.Name.Trim() : null;
    }
}
