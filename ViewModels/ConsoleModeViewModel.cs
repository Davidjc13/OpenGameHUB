using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure.Input;
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
    private readonly Func<bool> _animationsEnabled;
    private readonly List<ConsoleCategoryItem> _categories = [];
    private GameItemViewModel? _watchedGame;
    private int _categoryIndex;
    private bool _suppressSearchRefresh;
    private bool _syncingFocus;
    private bool _presenceSuspended;
    private readonly ConsoleMediaGuideInput _mediaGuide = new();
    private readonly GamepadRepeatState _gamepadRepeat = new();
    private readonly ConsoleOnScreenKeyboard _keyboard = new();
    private int _backdropGeneration;

    public const int HeroBackdropDecodeWidth = 96;

    public ConsoleModeViewModel(
        MainWindowLibraryViewModel library,
        Action exit,
        ISystemMediaController? media = null,
        IMediaVolumeController? volume = null,
        IDiscordPresence? discord = null,
        Func<string?>? discordApplicationId = null,
        Func<bool>? animationsEnabled = null)
    {
        _library = library;
        _exit = exit;
        _discord = discord;
        _discordApplicationId = discordApplicationId;
        _animationsEnabled = animationsEnabled ?? (() => false);
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

    public event EventHandler? SearchRequested;

    public bool IsActive { get; private set; }

    public string ExitLabel => Loc.T("ExitConsoleMode");

    public string SearchPlaceholder => Loc.T("ConsoleSearchPlaceholder");

    public string HintPlay => MenuRegion == ConsoleMenuRegion.Exit
        ? ExitLabel
        : KeyboardOpen || MenuRegion is ConsoleMenuRegion.Search or ConsoleMenuRegion.Categories
            ? Loc.T("ConsoleHintConfirm")
            : Loc.T("MediaPlay");

    public string HintBack => KeyboardOpen ? Loc.T("ConsoleMediaGuideClose") : ExitLabel;

    public string HintFavorite => Loc.T("ConsoleFavorites");

    public string HintCategories => Loc.T("ConsoleHintCategories");

    public string HintSearch => Loc.T("ConsoleHintSearch");

    public string HintMove => Loc.T("ConsoleHintMove");

    public string EmptyMessage => Loc.T("ConsoleEmpty");

    public string MediaGuideTitle => Loc.T("ConsoleMediaGuideTitle");

    public string MediaGuidePrevious => Loc.T("MediaPrevious");

    public string MediaGuideNext => Loc.T("MediaNext");

    public string MediaGuideVolume => Loc.T("MediaVolume");

    public string MediaGuideMute => Loc.T("ConsoleMediaGuideMute");

    public string MediaGuideClose => Loc.T("ConsoleMediaGuideClose");

    public string KeyboardSpace => Loc.T("ConsoleKeyboardSpace");

    public string KeyboardBackspace => Loc.T("ConsoleKeyboardBackspace");

    public string KeyboardDone => Loc.T("ConsoleKeyboardDone");

    public string FocusedTitle => FocusedGame?.Title ?? string.Empty;

    public string FocusedPlatform => FocusedGame?.PlatformLabel ?? string.Empty;

    public string FocusedMeta => FocusedGame is null
        ? string.Empty
        : $"{FocusedGame.InstallStatus}  ·  {FocusedGame.PlaytimeLabel}";

    public string FocusedAction => FocusedGame?.ActionLabel ?? string.Empty;

    public Bitmap? HeroCover => FocusedGame?.CoverImage;

    public bool HasHeroCover => FocusedGame?.HasCover == true;

    public bool HasHeroBackdrop => HeroBackdrop is not null;

    [ObservableProperty]
    private Bitmap? _heroBackdrop;

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

    [ObservableProperty]
    private bool _mediaGuideOpen;

    [ObservableProperty]
    private bool _mediaGuideCharging;

    [ObservableProperty]
    private double _mediaGuideHoldSweep;

    [ObservableProperty]
    private bool _usingGamepad;

    [ObservableProperty]
    private bool _keyboardOpen;

    [ObservableProperty]
    private ConsoleMenuRegion _menuRegion = ConsoleMenuRegion.Games;

    public bool IsSearchMenu => MenuRegion == ConsoleMenuRegion.Search;

    public bool IsPlayMenu => MenuRegion == ConsoleMenuRegion.Play;

    public bool IsCategoriesMenu => MenuRegion == ConsoleMenuRegion.Categories;

    public bool IsGamesMenu => MenuRegion == ConsoleMenuRegion.Games;

    public bool IsMediaMenu => MenuRegion == ConsoleMenuRegion.Media;

    public bool IsExitMenu => MenuRegion == ConsoleMenuRegion.Exit;

    public ObservableCollection<ConsoleKeyboardRow> KeyboardRows { get; } = [];

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
        _mediaGuide.Reset();
        _gamepadRepeat.Reset();
        MediaGuideOpen = false;
        MediaGuideCharging = false;
        MediaGuideHoldSweep = 0;
        Media.Stop();
        CloseSearchKeyboard();
        SearchFocused = false;
        MenuRegion = ConsoleMenuRegion.Games;
        _suppressSearchRefresh = true;
        SearchText = string.Empty;
        _suppressSearchRefresh = false;
        _categoryIndex = 0;
        _library.ClearSelection();
        _library.SetConsoleCoverMode(false);
        ClearHeroBackdrop();
        PublishPresence();
    }

    public void ApplyLocalization()
    {
        OnPropertyChanged(nameof(ExitLabel));
        OnPropertyChanged(nameof(SearchPlaceholder));
        OnPropertyChanged(nameof(HintPlay));
        OnPropertyChanged(nameof(HintBack));
        OnPropertyChanged(nameof(HintFavorite));
        OnPropertyChanged(nameof(HintCategories));
        OnPropertyChanged(nameof(HintSearch));
        OnPropertyChanged(nameof(HintMove));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(MediaGuideTitle));
        OnPropertyChanged(nameof(MediaGuidePrevious));
        OnPropertyChanged(nameof(MediaGuideNext));
        OnPropertyChanged(nameof(MediaGuideVolume));
        OnPropertyChanged(nameof(MediaGuideMute));
        OnPropertyChanged(nameof(MediaGuideClose));
        OnPropertyChanged(nameof(KeyboardSpace));
        OnPropertyChanged(nameof(KeyboardBackspace));
        OnPropertyChanged(nameof(KeyboardDone));
        if (KeyboardOpen)
            PublishKeyboard();
        Media.ApplyLocalization();
        NotifyHero();
        if (!IsActive)
            return;

        Rebuild(FocusedGame?.Source.Id);
    }

    public void NoteGamepad() => UsingGamepad = true;

    public void NoteKeyboardOrMouse() => UsingGamepad = false;

    public void ProcessGamepad(GamepadButtons previous, GamepadButtons current, TimeSpan elapsed)
    {
        if (!IsActive)
            return;

        var (actions, blockNavigation) = _mediaGuide.Process(previous, current, elapsed);
        foreach (var action in actions)
            ApplyMediaAction(action);

        MediaGuideOpen = _mediaGuide.IsGuideOpen;
        var charging = _mediaGuide.IsCharging && _animationsEnabled();
        MediaGuideCharging = charging;
        MediaGuideHoldSweep = charging ? _mediaGuide.HoldProgress * 360 : 0;
        if (blockNavigation)
        {
            _gamepadRepeat.Reset();
            return;
        }

        var inputs = GamepadInput.Collect(previous, current, _gamepadRepeat, elapsed);
        if (KeyboardOpen)
        {
            foreach (var input in inputs)
                ApplyKeyboardInput(input);
            return;
        }

        foreach (var input in inputs)
            ApplyInput(input);
    }

    public void ApplyInput(ConsoleInput input, bool textConsumesHorizontal = false)
    {
        if (!IsActive)
            return;

        var state = new ConsoleNavigationState(
            _categoryIndex,
            Math.Max(FocusedIndex, 0),
            SearchFocused,
            MenuRegion);
        var (next, effect) = ConsoleNavigation.Apply(
            state,
            input,
            Categories.Count,
            Games.Count,
            textConsumesHorizontal,
            hasMedia: Media.IsAvailable);

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
        MenuRegion = next.Menu;
        if (effect == ConsoleInputEffect.ClearSearch)
        {
            SearchText = string.Empty;
            CloseSearchKeyboard();
            MenuRegion = ConsoleMenuRegion.Games;
            FocusRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (effect == ConsoleInputEffect.OpenSearch)
        {
            OpenSearchKeyboard();
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

        if (MenuRegion == ConsoleMenuRegion.Games
            && input is ConsoleInput.MoveLeft or ConsoleInput.MoveRight
                or ConsoleInput.PreviousCategory or ConsoleInput.NextCategory)
        {
            FocusRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyMediaAction(ConsoleMediaAction action)
    {
        switch (action)
        {
            case ConsoleMediaAction.TogglePlay:
                ApplyInput(ConsoleInput.MediaToggle);
                break;
            case ConsoleMediaAction.SkipPrev:
                ApplyInput(ConsoleInput.MediaPrevious);
                break;
            case ConsoleMediaAction.SkipNext:
                ApplyInput(ConsoleInput.MediaNext);
                break;
            case ConsoleMediaAction.VolumeUp:
                Media.BumpVolume(SystemMediaViewModel.VolumeStep);
                break;
            case ConsoleMediaAction.VolumeDown:
                Media.BumpVolume(-SystemMediaViewModel.VolumeStep);
                break;
            case ConsoleMediaAction.Mute:
                Media.ToggleMute();
                break;
            case ConsoleMediaAction.Search:
                if (KeyboardOpen)
                    CloseSearchKeyboard();
                else
                    OpenSearchKeyboard();
                SearchRequested?.Invoke(this, EventArgs.Empty);
                break;
            case ConsoleMediaAction.GuideOpened:
                MediaGuideOpen = true;
                break;
            case ConsoleMediaAction.GuideClosed:
                MediaGuideOpen = false;
                break;
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

    public void OpenSearchKeyboard()
    {
        KeyboardOpen = true;
        SearchFocused = true;
        MenuRegion = ConsoleMenuRegion.Search;
        PublishKeyboard();
        _keyboard.ResetFocus();
    }

    public void CloseSearchKeyboard()
    {
        KeyboardOpen = false;
        SearchFocused = false;
        if (MenuRegion == ConsoleMenuRegion.Search)
            MenuRegion = ConsoleMenuRegion.Games;
    }

    private void ApplyKeyboardInput(ConsoleInput input)
    {
        if (input == ConsoleInput.Back)
        {
            CloseSearchKeyboard();
            FocusRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (input == ConsoleInput.Activate)
        {
            var result = _keyboard.Activate(SearchText);
            if (result.Text != SearchText)
                SearchText = result.Text;
            if (result.Close)
            {
                CloseSearchKeyboard();
                FocusRequested?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        _keyboard.Move(input);
    }

    [RelayCommand]
    private void SelectKeyboardKey(ConsoleKeyboardKey? key)
    {
        if (key is null || !KeyboardOpen)
            return;

        _keyboard.Focus(key);
        ApplyKeyboardInput(ConsoleInput.Activate);
    }

    private void PublishKeyboard()
    {
        _keyboard.Rebuild(KeyboardSpace, KeyboardBackspace, KeyboardDone);
        KeyboardRows.Clear();
        foreach (var row in _keyboard.Rows)
            KeyboardRows.Add(new ConsoleKeyboardRow(row));
    }

    partial void OnKeyboardOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(HintPlay));
        OnPropertyChanged(nameof(HintBack));
    }

    partial void OnMenuRegionChanged(ConsoleMenuRegion value)
    {
        SearchFocused = value == ConsoleMenuRegion.Search || KeyboardOpen;
        OnPropertyChanged(nameof(HintPlay));
        OnPropertyChanged(nameof(IsSearchMenu));
        OnPropertyChanged(nameof(IsPlayMenu));
        OnPropertyChanged(nameof(IsCategoriesMenu));
        OnPropertyChanged(nameof(IsGamesMenu));
        OnPropertyChanged(nameof(IsMediaMenu));
        OnPropertyChanged(nameof(IsExitMenu));
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
        RefreshHeroBackdrop();
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
            or nameof(GameItemViewModel.HasCover))
        {
            NotifyHero();
            RefreshHeroBackdrop();
            return;
        }

        if (e.PropertyName is nameof(GameItemViewModel.ActionLabel)
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
        OnPropertyChanged(nameof(HasHeroBackdrop));
        OnPropertyChanged(nameof(HasFocusedGame));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnHeroBackdropChanged(Bitmap? value) =>
        OnPropertyChanged(nameof(HasHeroBackdrop));

    private void RefreshHeroBackdrop()
    {
        var generation = ++_backdropGeneration;
        if (!IsActive || FocusedGame is null)
        {
            ClearHeroBackdrop(incrementGeneration: false);
            return;
        }

        var path = CoverPathHelper.ResolveExistingPath(FocusedGame.Source);
        if (path is null)
        {
            ClearHeroBackdrop(incrementGeneration: false);
            return;
        }

        _ = LoadHeroBackdropAsync(path, generation);
    }

    private async Task LoadHeroBackdropAsync(string path, int generation)
    {
        var bitmap = await CoverImageLoader.LoadDecodedAsync(
            path,
            HeroBackdropDecodeWidth,
            BitmapInterpolationMode.LowQuality);
        if (bitmap is null)
            return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (generation != _backdropGeneration)
            {
                bitmap.Dispose();
                return;
            }

            var previous = HeroBackdrop;
            HeroBackdrop = bitmap;
            DisposeLater(previous);
        });
    }

    private void ClearHeroBackdrop(bool incrementGeneration = true)
    {
        if (incrementGeneration)
            _backdropGeneration++;

        var previous = HeroBackdrop;
        HeroBackdrop = null;
        DisposeLater(previous);
    }

    private static void DisposeLater(Bitmap? bitmap)
    {
        if (bitmap is null)
            return;

        try
        {
            Dispatcher.UIThread.Post(bitmap.Dispose, DispatcherPriority.Background);
        }
        catch (InvalidOperationException)
        {
            bitmap.Dispose();
        }
    }

    private static string LabelFor(ConsoleCategory category) => category.Kind switch
    {
        ConsoleCategoryKind.Installed => Loc.T("ConsoleInstalled"),
        ConsoleCategoryKind.Favorites => Loc.T("ConsoleFavorites"),
        ConsoleCategoryKind.Platform => PlatformLabels.Get(category.Platform!.Value),
        _ => Loc.T("ConsoleAll")
    };
}
