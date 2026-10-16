using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure.Media;
using OpenGameHUB.Services.Media;
using OpenGameHUB.Services.Presence;
using OpenGameHUB.ViewModels.MainWindow;

namespace OpenGameHUB.ViewModels;

public partial class ConsoleModeViewModel : ViewModelBase
{
    private readonly MainWindowLibraryViewModel _library;
    private readonly Action _exit;
    private readonly IDiscordPresence? _discord;
    private readonly Func<string?>? _discordApplicationId;
    private readonly List<ConsoleCategoryItem> _categories = [];
    private GameItemViewModel? _watchedGame;
    private int _categoryIndex;
    private bool _suppressSearchRefresh;
    private bool _syncingFocus;
    private bool _presenceSuspended;

    public ConsoleModeViewModel(
        MainWindowLibraryViewModel library,
        Action exit,
        ISystemMediaController? media = null,
        IMediaVolumeController? volume = null,
        IDiscordPresence? discord = null,
        Func<string?>? discordApplicationId = null)
    {
        _library = library;
        _exit = exit;
        _discord = discord;
        _discordApplicationId = discordApplicationId;
        Media = new SystemMediaViewModel(
            media ?? new WindowsSystemMediaController(),
            volume: volume ?? new WindowsMediaVolume());
        Categories = new ObservableCollection<ConsoleCategoryItem>();
        Games = new ObservableCollection<GameItemViewModel>();
        _library.LibraryContentsChanged += OnLibraryContentsChanged;
    }

    public SystemMediaViewModel Media { get; }

    public ObservableCollection<ConsoleCategoryItem> Categories { get; }

    public ObservableCollection<GameItemViewModel> Games { get; }

    public event EventHandler? FocusRequested;

    public bool IsActive { get; private set; }

    public string ExitLabel => Loc.T("ExitConsoleMode");

    public string SearchPlaceholder => Loc.T("ConsoleSearchPlaceholder");

    public string Hint => Loc.T("ConsoleHint");

    public string EmptyMessage => Loc.T("ConsoleEmpty");

    public string FocusedTitle => FocusedGame?.Title ?? string.Empty;

    public string FocusedPlatform => FocusedGame?.PlatformLabel ?? string.Empty;

    public string FocusedMeta => FocusedGame is null
        ? string.Empty
        : $"{FocusedGame.InstallStatus}  ·  {FocusedGame.PlaytimeLabel}";

    public string FocusedAction => FocusedGame?.ActionLabel ?? string.Empty;

    public Bitmap? HeroCover => FocusedGame?.CoverImage;

    public bool HasHeroCover => FocusedGame?.HasCover == true;

    public bool HasFocusedGame => FocusedGame is not null;

    public bool IsEmpty => Games.Count == 0;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _searchFocused;

    [ObservableProperty]
    private int _focusedIndex = -1;

    [ObservableProperty]
    private GameItemViewModel? _focusedGame;

    public void Activate()
    {
        if (IsActive)
            return;

        IsActive = true;
        Media.Start();
        _library.SetConsoleCoverMode(true);
        Rebuild(preserveGameId: null);
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
        Media.Stop();
        SearchFocused = false;
        _suppressSearchRefresh = true;
        SearchText = string.Empty;
        _suppressSearchRefresh = false;
        _categoryIndex = 0;
        _library.ClearSelection();
        _library.SetConsoleCoverMode(false);
        PublishPresence();
    }

    public void ApplyLocalization()
    {
        OnPropertyChanged(nameof(ExitLabel));
        OnPropertyChanged(nameof(SearchPlaceholder));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(EmptyMessage));
        Media.ApplyLocalization();
        NotifyHero();
        if (!IsActive)
            return;

        Rebuild(FocusedGame?.Source.Id);
    }

    public void ApplyInput(ConsoleInput input, bool textConsumesHorizontal = false)
    {
        if (!IsActive)
            return;

        var state = new ConsoleNavigationState(_categoryIndex, Math.Max(FocusedIndex, 0), SearchFocused);
        var (next, effect) = ConsoleNavigation.Apply(
            state,
            input,
            Categories.Count,
            Games.Count,
            textConsumesHorizontal);

        if (effect == ConsoleInputEffect.MediaToggle)
        {
            if (Media.ToggleCommand.CanExecute(null))
                _ = Media.ToggleCommand.ExecuteAsync(null);
            return;
        }

        if (effect == ConsoleInputEffect.MediaNext)
        {
            if (Media.NextCommand.CanExecute(null))
                _ = Media.NextCommand.ExecuteAsync(null);
            return;
        }

        if (effect == ConsoleInputEffect.MediaPrevious)
        {
            if (Media.PreviousCommand.CanExecute(null))
                _ = Media.PreviousCommand.ExecuteAsync(null);
            return;
        }

        if (effect == ConsoleInputEffect.Exit)
        {
            _exit();
            return;
        }

        SearchFocused = next.SearchFocused;
        if (effect == ConsoleInputEffect.ClearSearch)
        {
            SearchText = string.Empty;
            FocusRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (next.CategoryIndex != _categoryIndex)
        {
            _categoryIndex = next.CategoryIndex;
            PublishFiltered(next.GameIndex);
        }
        else if (Games.Count > 0 && next.GameIndex != FocusedIndex)
        {
            FocusedIndex = next.GameIndex;
        }

        if (effect == ConsoleInputEffect.Activate)
            PlayFocused();
        else if (effect == ConsoleInputEffect.Favorite)
            ToggleFocusedFavorite();

        if (input is ConsoleInput.MoveLeft or ConsoleInput.MoveRight
            or ConsoleInput.MoveUp or ConsoleInput.MoveDown
            or ConsoleInput.PreviousCategory or ConsoleInput.NextCategory)
        {
            FocusRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SelectCategory(ConsoleCategoryItem item)
    {
        if (!IsActive)
            return;

        var index = _categories.IndexOf(item);
        if (index < 0 || index == _categoryIndex)
            return;

        _categoryIndex = index;
        PublishFiltered(0);
    }

    [RelayCommand]
    private void PlayFocused()
    {
        if (!IsActive || FocusedGame is null)
            return;

        _library.LaunchGameCommand.Execute(FocusedGame);
    }

    [RelayCommand]
    private void ExitMode() => _exit();

    partial void OnSearchTextChanged(string value)
    {
        if (!IsActive || _suppressSearchRefresh)
            return;

        PublishFiltered(0);
    }

    partial void OnFocusedIndexChanged(int value)
    {
        if (_syncingFocus)
            return;

        ApplyFocus();
    }

    private void OnLibraryContentsChanged(object? sender, EventArgs e)
    {
        if (IsActive)
            Rebuild(FocusedGame?.Source.Id);
    }

    private void Rebuild(string? preserveGameId)
    {
        var kind = CurrentCategory?.Kind ?? ConsoleCategoryKind.All;
        var platform = CurrentCategory?.Platform;
        RebuildCategories(kind, platform);
        PublishFiltered(0, preserveGameId);
    }

    private void RebuildCategories(ConsoleCategoryKind kind, Platform? platform)
    {
        var built = ConsoleCatalog.Build(_library.AllGames);
        _categories.Clear();
        Categories.Clear();
        foreach (var category in built)
        {
            var item = new ConsoleCategoryItem(category.Kind, category.Platform, LabelFor(category), SelectCategory);
            _categories.Add(item);
            Categories.Add(item);
        }

        var index = 0;
        for (var i = 0; i < _categories.Count; i++)
        {
            if (_categories[i].Kind == kind && _categories[i].Platform == platform)
            {
                index = i;
                break;
            }
        }

        _categoryIndex = index;
        MarkCategorySelection();
    }

    private void PublishFiltered(int focusIndex, string? preserveGameId = null)
    {
        var category = CurrentCategory;
        if (category is null)
        {
            ReplaceGames([], -1);
            return;
        }

        var games = ConsoleCatalog.Filter(
            _library.AllGames,
            category.Kind,
            category.Platform,
            SearchText);
        var index = focusIndex;
        if (preserveGameId is not null)
        {
            var found = games.FindIndex(game =>
                string.Equals(game.Source.Id, preserveGameId, StringComparison.Ordinal));
            index = found >= 0 ? found : 0;
        }

        MarkCategorySelection();
        ReplaceGames(games, index);
    }

    private void ReplaceGames(List<GameItemViewModel> games, int focusIndex)
    {
        var clamped = games.Count == 0 ? -1 : Math.Clamp(focusIndex, 0, games.Count - 1);
        _syncingFocus = true;
        Games.Clear();
        foreach (var game in games)
            Games.Add(game);
        FocusedIndex = clamped;
        _syncingFocus = false;
        ApplyFocus();
    }

    private void ApplyFocus()
    {
        var game = FocusedIndex >= 0 && FocusedIndex < Games.Count
            ? Games[FocusedIndex]
            : null;
        if (!ReferenceEquals(FocusedGame, game))
        {
            Watch(game);
            FocusedGame = game;
        }

        if (IsActive)
            _library.LoadConsoleCovers(Games, Math.Max(FocusedIndex, 0));

        NotifyHero();
        PublishPresence();
    }

    public void SuspendPresence()
    {
        _presenceSuspended = true;
        PublishPresence();
    }

    public void ResumePresence()
    {
        if (!_presenceSuspended)
            return;

        _presenceSuspended = false;
        PublishPresence();
    }

    private void PublishPresence()
    {
        if (_discord is null)
            return;

        DiscordActivity? activity = null;
        if (IsActive && !_presenceSuspended && FocusedGame is { } game && !string.IsNullOrWhiteSpace(game.Title))
            activity = new DiscordActivity(game.Title, game.PlatformLabel);

        _discord.Publish(_discordApplicationId?.Invoke(), activity);
    }

    private void MarkCategorySelection()
    {
        for (var i = 0; i < _categories.Count; i++)
            _categories[i].IsSelected = i == _categoryIndex;
    }

    private ConsoleCategoryItem? CurrentCategory =>
        _categoryIndex >= 0 && _categoryIndex < _categories.Count
            ? _categories[_categoryIndex]
            : null;

    private void ToggleFocusedFavorite()
    {
        if (FocusedGame is null)
            return;

        _library.ToggleFavoriteCommand.Execute(FocusedGame);
    }

    private void Watch(GameItemViewModel? game)
    {
        if (_watchedGame is not null)
            _watchedGame.PropertyChanged -= OnWatchedGamePropertyChanged;

        _watchedGame = game;
        if (_watchedGame is not null)
            _watchedGame.PropertyChanged += OnWatchedGamePropertyChanged;
    }

    private void OnWatchedGamePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GameItemViewModel.CoverImage)
            or nameof(GameItemViewModel.HasCover)
            or nameof(GameItemViewModel.ActionLabel)
            or nameof(GameItemViewModel.InstallStatus)
            or nameof(GameItemViewModel.PlaytimeLabel)
            or nameof(GameItemViewModel.IsFavorite))
        {
            NotifyHero();
        }
    }

    private void NotifyHero()
    {
        OnPropertyChanged(nameof(FocusedTitle));
        OnPropertyChanged(nameof(FocusedPlatform));
        OnPropertyChanged(nameof(FocusedMeta));
        OnPropertyChanged(nameof(FocusedAction));
        OnPropertyChanged(nameof(HeroCover));
        OnPropertyChanged(nameof(HasHeroCover));
        OnPropertyChanged(nameof(HasFocusedGame));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private static string LabelFor(ConsoleCategory category) => category.Kind switch
    {
        ConsoleCategoryKind.Installed => Loc.T("ConsoleInstalled"),
        ConsoleCategoryKind.Favorites => Loc.T("ConsoleFavorites"),
        ConsoleCategoryKind.Platform => PlatformLabels.Get(category.Platform!.Value),
        _ => Loc.T("ConsoleAll")
    };
}
