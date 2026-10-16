using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure.Input;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Tests;

public sealed class ConsoleModeTests
{
    [Fact]
    public void Move_right_wraps_to_the_first_game()
    {
        var state = new ConsoleNavigationState(0, 2, false);

        var (next, effect) = ConsoleNavigation.Apply(state, ConsoleInput.MoveRight, 3, 3);

        Assert.Equal(ConsoleInputEffect.None, effect);
        Assert.Equal(0, next.GameIndex);
        Assert.Equal(0, next.CategoryIndex);
    }

    [Fact]
    public void Move_left_wraps_to_the_last_game()
    {
        var state = new ConsoleNavigationState(1, 0, false);

        var (next, effect) = ConsoleNavigation.Apply(state, ConsoleInput.MoveLeft, 4, 5);

        Assert.Equal(ConsoleInputEffect.None, effect);
        Assert.Equal(4, next.GameIndex);
        Assert.Equal(1, next.CategoryIndex);
    }

    [Fact]
    public void Next_category_wraps_and_resets_the_game_index()
    {
        var state = new ConsoleNavigationState(2, 4, false);

        var (next, effect) = ConsoleNavigation.Apply(state, ConsoleInput.NextCategory, 3, 8);

        Assert.Equal(ConsoleInputEffect.None, effect);
        Assert.Equal(0, next.CategoryIndex);
        Assert.Equal(0, next.GameIndex);
    }

    [Fact]
    public void Previous_category_wraps_and_resets_the_game_index()
    {
        var state = new ConsoleNavigationState(0, 3, false);

        var (next, effect) = ConsoleNavigation.Apply(state, ConsoleInput.PreviousCategory, 4, 6);

        Assert.Equal(ConsoleInputEffect.None, effect);
        Assert.Equal(3, next.CategoryIndex);
        Assert.Equal(0, next.GameIndex);
    }

    [Fact]
    public void Move_up_changes_the_menu_instead_of_the_category()
    {
        var state = new ConsoleNavigationState(0, 4, false, ConsoleMenuRegion.Games);

        var (next, effect) = ConsoleNavigation.Apply(state, ConsoleInput.MoveUp, 3, 8);

        Assert.Equal(ConsoleInputEffect.None, effect);
        Assert.Equal(0, next.CategoryIndex);
        Assert.Equal(4, next.GameIndex);
        Assert.Equal(ConsoleMenuRegion.Categories, next.Menu);
    }

    [Fact]
    public void Left_and_right_change_categories_when_that_menu_is_focused()
    {
        var state = new ConsoleNavigationState(1, 4, false, ConsoleMenuRegion.Categories);

        var next = ConsoleNavigation.Apply(state, ConsoleInput.MoveRight, 3, 8).State;

        Assert.Equal(2, next.CategoryIndex);
        Assert.Equal(0, next.GameIndex);
        Assert.Equal(ConsoleMenuRegion.Categories, next.Menu);
    }

    [Fact]
    public void Right_from_search_moves_focus_to_exit()
    {
        var state = new ConsoleNavigationState(0, 1, true, ConsoleMenuRegion.Search);

        var (next, effect) = ConsoleNavigation.Apply(state, ConsoleInput.MoveRight, 3, 8);

        Assert.Equal(ConsoleInputEffect.None, effect);
        Assert.Equal(ConsoleMenuRegion.Exit, next.Menu);
        Assert.False(next.SearchFocused);
    }

    [Fact]
    public void Left_from_exit_returns_to_search()
    {
        var state = new ConsoleNavigationState(0, 1, false, ConsoleMenuRegion.Exit);

        var next = ConsoleNavigation.Apply(state, ConsoleInput.MoveLeft, 3, 8).State;

        Assert.Equal(ConsoleMenuRegion.Search, next.Menu);
        Assert.True(next.SearchFocused);
    }

    [Fact]
    public void Activate_opens_search_when_the_search_menu_is_focused()
    {
        var state = new ConsoleNavigationState(0, 1, true, ConsoleMenuRegion.Search);

        var (_, effect) = ConsoleNavigation.Apply(state, ConsoleInput.Activate, 3, 8);

        Assert.Equal(ConsoleInputEffect.OpenSearch, effect);
    }

    [Fact]
    public void Window_pad_moves_through_the_grid()
    {
        Assert.Equal(new GridStep(0, 0), WindowGamepadNavigation.Move(-1, 8, 4, ConsoleInput.MoveRight));
        Assert.Equal(new GridStep(5, 0), WindowGamepadNavigation.Move(1, 8, 4, ConsoleInput.MoveDown));
        Assert.Equal(new GridStep(1, 0), WindowGamepadNavigation.Move(5, 8, 4, ConsoleInput.MoveUp));
        Assert.Equal(new GridStep(7, -1), WindowGamepadNavigation.Move(0, 8, 4, ConsoleInput.MoveLeft));
        Assert.Equal(new GridStep(0, 1), WindowGamepadNavigation.Move(7, 8, 4, ConsoleInput.MoveRight));
    }

    [Fact]
    public void Horizontal_move_is_ignored_while_text_consumes_it()
    {
        var state = new ConsoleNavigationState(0, 2, true);

        var (next, effect) = ConsoleNavigation.Apply(
            state,
            ConsoleInput.MoveLeft,
            3,
            5,
            textConsumesHorizontal: true);

        Assert.Equal(ConsoleInputEffect.None, effect);
        Assert.Equal(state, next);
    }

    [Fact]
    public void Back_clears_search_when_it_is_focused()
    {
        var state = new ConsoleNavigationState(1, 2, true);

        var (next, effect) = ConsoleNavigation.Apply(state, ConsoleInput.Back, 3, 4);

        Assert.Equal(ConsoleInputEffect.ClearSearch, effect);
        Assert.False(next.SearchFocused);
        Assert.Equal(2, next.GameIndex);
    }

    [Fact]
    public void Media_controls_keep_the_current_game_selected()
    {
        var state = new ConsoleNavigationState(1, 2, true);

        var (next, effect) = ConsoleNavigation.Apply(state, ConsoleInput.MediaToggle, 4, 6);

        Assert.Equal(ConsoleInputEffect.MediaToggle, effect);
        Assert.Equal(state, next);
    }

    [Fact]
    public void Back_exits_when_search_is_not_focused()
    {
        var state = new ConsoleNavigationState(0, 1, false);

        var (_, effect) = ConsoleNavigation.Apply(state, ConsoleInput.Back, 3, 4);

        Assert.Equal(ConsoleInputEffect.Exit, effect);
    }

    [Fact]
    public void Activate_and_favorite_do_nothing_when_the_row_is_empty()
    {
        var state = new ConsoleNavigationState(0, 0, false);

        var (_, activate) = ConsoleNavigation.Apply(state, ConsoleInput.Activate, 3, 0);
        var (_, favorite) = ConsoleNavigation.Apply(state, ConsoleInput.Favorite, 3, 0);

        Assert.Equal(ConsoleInputEffect.None, activate);
        Assert.Equal(ConsoleInputEffect.None, favorite);
    }

    [Fact]
    public void Build_adds_a_category_per_platform_in_label_order()
    {
        var games = new[]
        {
            CreateGame("steam:1", Platform.Steam, "Alpha"),
            CreateGame("epic:1", Platform.Epic, "Beta", alternatePlatform: Platform.Gog)
        };

        var categories = ConsoleCatalog.Build(games);

        Assert.Equal(
            [
                ConsoleCategoryKind.All,
                ConsoleCategoryKind.Installed,
                ConsoleCategoryKind.Favorites,
                ConsoleCategoryKind.Platform,
                ConsoleCategoryKind.Platform,
                ConsoleCategoryKind.Platform
            ],
            categories.Select(category => category.Kind));
        Assert.Equal(
            [Platform.Epic, Platform.Gog, Platform.Steam],
            categories.Where(category => category.Platform is not null).Select(category => category.Platform));
    }

    [Fact]
    public void Filter_keeps_installed_favorites_platform_and_search()
    {
        var older = DateTime.UtcNow.AddDays(-10);
        var recent = DateTime.UtcNow.AddDays(-1);
        var games = new List<GameItemViewModel>
        {
            CreateGame("steam:1", Platform.Steam, "Alpha", isFavorite: true, isInstalled: true, lastPlayed: older),
            CreateGame("steam:2", Platform.Steam, "Beta", isInstalled: true),
            CreateGame("epic:1", Platform.Epic, "Alpha Epic", isFavorite: true, lastPlayed: recent),
            CreateGame("custom:1", Platform.Unknown, "Hidden")
        };

        var installed = ConsoleCatalog.Filter(games, ConsoleCategoryKind.Installed, null, null);
        var favorites = ConsoleCatalog.Filter(games, ConsoleCategoryKind.Favorites, null, "alpha");
        var steam = ConsoleCatalog.Filter(games, ConsoleCategoryKind.Platform, Platform.Steam, null);

        Assert.Equal(["Alpha", "Beta"], installed.Select(game => game.Title));
        Assert.Equal(["Alpha Epic", "Alpha"], favorites.Select(game => game.Title));
        Assert.Equal(["Alpha", "Beta"], steam.Select(game => game.Title));
    }

    [Fact]
    public void Filter_matches_alternate_platforms()
    {
        var games = new List<GameItemViewModel>
        {
            CreateGame("steam:1", Platform.Steam, "Shared", alternatePlatform: Platform.Epic)
        };

        var epic = ConsoleCatalog.Filter(games, ConsoleCategoryKind.Platform, Platform.Epic, null);

        Assert.Equal("Shared", Assert.Single(epic).Title);
    }

    [Fact]
    public void Gamepad_buttons_fire_on_the_rising_edge()
    {
        var repeat = new GamepadRepeatState();
        var pressed = new GamepadButtons(A: true, B: true, Y: true, Left: false, Right: false, LeftShoulder: true, RightShoulder: false, ThumbX: 0);

        var first = GamepadInput.Collect(default, pressed, repeat, TimeSpan.Zero);
        var held = GamepadInput.Collect(pressed, pressed, repeat, TimeSpan.FromMilliseconds(50));

        Assert.Equal(
            [ConsoleInput.Activate, ConsoleInput.Back, ConsoleInput.Favorite, ConsoleInput.PreviousCategory],
            first);
        Assert.Empty(held);
    }

    [Fact]
    public void Stick_ignores_the_deadzone_then_repeats()
    {
        var repeat = new GamepadRepeatState();
        var inside = new GamepadButtons(false, false, false, false, false, false, false, GamepadInput.Deadzone);
        var outside = inside with { ThumbX = (short)(GamepadInput.Deadzone + 1) };

        Assert.Empty(GamepadInput.Collect(default, inside, repeat, TimeSpan.Zero));

        var initial = GamepadInput.Collect(inside, outside, repeat, TimeSpan.Zero);
        var tooSoon = GamepadInput.Collect(outside, outside, repeat, TimeSpan.FromMilliseconds(100));
        var afterInitialDelay = GamepadInput.Collect(outside, outside, repeat, GamepadInput.InitialRepeat);
        var afterRepeat = GamepadInput.Collect(outside, outside, repeat, GamepadInput.RepeatInterval);

        Assert.Equal([ConsoleInput.MoveRight], initial);
        Assert.Empty(tooSoon);
        Assert.Equal([ConsoleInput.MoveRight], afterInitialDelay);
        Assert.Equal([ConsoleInput.MoveRight], afterRepeat);
    }

    [Fact]
    public void Dpad_takes_the_frame_instead_of_the_stick()
    {
        var repeat = new GamepadRepeatState();
        var current = new GamepadButtons(
            A: false,
            B: false,
            Y: false,
            Left: true,
            Right: false,
            LeftShoulder: false,
            RightShoulder: false,
            ThumbX: 20000);

        var inputs = GamepadInput.Collect(default, current, repeat, TimeSpan.Zero);

        Assert.Equal([ConsoleInput.MoveLeft], inputs);
        Assert.Equal(0, repeat.Direction);
    }

    [Fact]
    public void Gamepad_x_is_not_a_direct_media_toggle()
    {
        var repeat = new GamepadRepeatState();
        var pressed = new GamepadButtons(
            A: false,
            B: false,
            Y: false,
            Left: false,
            Right: false,
            LeftShoulder: false,
            RightShoulder: false,
            ThumbX: 0,
            X: true);

        var first = GamepadInput.Collect(default, pressed, repeat, TimeSpan.Zero);
        var held = GamepadInput.Collect(pressed, pressed, repeat, TimeSpan.FromMilliseconds(50));

        Assert.Empty(first);
        Assert.Empty(held);
    }

    private static GameItemViewModel CreateGame(
        string id,
        Platform platform,
        string title,
        bool isFavorite = false,
        bool isInstalled = false,
        DateTime? lastPlayed = null,
        Platform? alternatePlatform = null)
    {
        UnifiedGame? alternate = null;
        if (alternatePlatform is Platform other)
        {
            alternate = new UnifiedGame
            {
                Id = id + ":alt",
                Platform = other,
                PlatformGameId = id + ":alt",
                Title = title,
                LaunchSpec = LaunchSpec.Executable("game.exe")
            };
        }

        return new GameItemViewModel(new UnifiedGame
        {
            Id = id,
            Platform = platform,
            PlatformGameId = id,
            Title = title,
            IsFavorite = isFavorite,
            IsInstalled = isInstalled,
            LastPlayed = lastPlayed,
            LaunchSpec = LaunchSpec.Executable("game.exe"),
            AlternateListings = alternate is null ? [] : [alternate]
        });
    }
}
