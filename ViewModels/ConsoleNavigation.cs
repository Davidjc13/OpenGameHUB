namespace OpenGameHUB.ViewModels;

public enum ConsoleInput
{
    MoveLeft,
    MoveRight,
    MoveUp,
    MoveDown,
    PreviousCategory,
    NextCategory,
    Activate,
    Favorite,
    Back,
    MediaToggle,
    MediaNext,
    MediaPrevious
}

public enum ConsoleInputEffect
{
    None,
    Activate,
    Favorite,
    Exit,
    ClearSearch,
    OpenSearch,
    MediaToggle,
    MediaNext,
    MediaPrevious
}

public enum ConsoleMenuRegion
{
    Search,
    Exit,
    Play,
    Categories,
    Games,
    Media
}

public readonly record struct ConsoleNavigationState(
    int CategoryIndex,
    int GameIndex,
    bool SearchFocused,
    ConsoleMenuRegion Menu = ConsoleMenuRegion.Games);

public static class ConsoleNavigation
{
    public static (ConsoleNavigationState State, ConsoleInputEffect Effect) Apply(
        ConsoleNavigationState state,
        ConsoleInput input,
        int categoryCount,
        int gameCount,
        bool textConsumesHorizontal = false,
        bool hasMedia = false)
    {
        switch (input)
        {
            case ConsoleInput.MediaToggle:
                return (state, ConsoleInputEffect.MediaToggle);
            case ConsoleInput.MediaNext:
                return (state, ConsoleInputEffect.MediaNext);
            case ConsoleInput.MediaPrevious:
                return (state, ConsoleInputEffect.MediaPrevious);
            case ConsoleInput.Back when state.SearchFocused || state.Menu == ConsoleMenuRegion.Search:
                return (state with
                {
                    SearchFocused = false,
                    Menu = ConsoleMenuRegion.Games
                }, ConsoleInputEffect.ClearSearch);
            case ConsoleInput.Back:
                return (state, ConsoleInputEffect.Exit);
            case ConsoleInput.Activate:
                return Activate(state, gameCount);
            case ConsoleInput.Favorite:
                return (state, state.Menu == ConsoleMenuRegion.Games && gameCount > 0
                    ? ConsoleInputEffect.Favorite
                    : ConsoleInputEffect.None);
            case ConsoleInput.MoveLeft when textConsumesHorizontal:
            case ConsoleInput.MoveRight when textConsumesHorizontal:
                return (state, ConsoleInputEffect.None);
            case ConsoleInput.MoveLeft:
            case ConsoleInput.MoveRight:
                return MoveHorizontal(state, input, categoryCount, gameCount);
            case ConsoleInput.MoveUp:
            case ConsoleInput.MoveDown:
            {
                var menu = ShiftMenu(state.Menu, input == ConsoleInput.MoveDown ? 1 : -1, categoryCount, gameCount, hasMedia);
                return (state with
                {
                    Menu = menu,
                    SearchFocused = menu == ConsoleMenuRegion.Search
                }, ConsoleInputEffect.None);
            }
            case ConsoleInput.PreviousCategory:
                return (state with
                {
                    CategoryIndex = Wrap(state.CategoryIndex, -1, categoryCount),
                    GameIndex = 0
                }, ConsoleInputEffect.None);
            case ConsoleInput.NextCategory:
                return (state with
                {
                    CategoryIndex = Wrap(state.CategoryIndex, 1, categoryCount),
                    GameIndex = 0
                }, ConsoleInputEffect.None);
            default:
                return (state, ConsoleInputEffect.None);
        }
    }

    private static (ConsoleNavigationState State, ConsoleInputEffect Effect) Activate(
        ConsoleNavigationState state,
        int gameCount)
    {
        return state.Menu switch
        {
            ConsoleMenuRegion.Search => (state with { SearchFocused = true }, ConsoleInputEffect.OpenSearch),
            ConsoleMenuRegion.Exit => (state, ConsoleInputEffect.Exit),
            ConsoleMenuRegion.Media => (state, ConsoleInputEffect.MediaToggle),
            ConsoleMenuRegion.Categories => (state, ConsoleInputEffect.None),
            _ => (state, gameCount > 0 ? ConsoleInputEffect.Activate : ConsoleInputEffect.None)
        };
    }

    private static (ConsoleNavigationState State, ConsoleInputEffect Effect) MoveHorizontal(
        ConsoleNavigationState state,
        ConsoleInput input,
        int categoryCount,
        int gameCount)
    {
        var delta = input == ConsoleInput.MoveRight ? 1 : -1;
        return state.Menu switch
        {
            ConsoleMenuRegion.Categories => (state with
            {
                CategoryIndex = Wrap(state.CategoryIndex, delta, categoryCount),
                GameIndex = 0
            }, ConsoleInputEffect.None),
            ConsoleMenuRegion.Media => (state, delta > 0 ? ConsoleInputEffect.MediaNext : ConsoleInputEffect.MediaPrevious),
            ConsoleMenuRegion.Search => (state with
            {
                Menu = delta > 0 ? ConsoleMenuRegion.Exit : ConsoleMenuRegion.Search,
                SearchFocused = delta <= 0
            }, ConsoleInputEffect.None),
            ConsoleMenuRegion.Exit => (state with
            {
                Menu = delta < 0 ? ConsoleMenuRegion.Search : ConsoleMenuRegion.Exit,
                SearchFocused = delta < 0
            }, ConsoleInputEffect.None),
            ConsoleMenuRegion.Play => (state, ConsoleInputEffect.None),
            _ => (state with { GameIndex = Wrap(state.GameIndex, delta, gameCount) }, ConsoleInputEffect.None)
        };
    }

    public static ConsoleMenuRegion ShiftMenu(
        ConsoleMenuRegion current,
        int delta,
        int categoryCount,
        int gameCount,
        bool hasMedia)
    {
        var regions = BuildMenus(categoryCount > 0, gameCount > 0, hasMedia);
        var from = current == ConsoleMenuRegion.Exit ? ConsoleMenuRegion.Search : current;
        var index = Array.IndexOf(regions, from);
        if (index < 0)
            index = Array.IndexOf(regions, ConsoleMenuRegion.Games);

        return regions[Wrap(index + delta, regions.Length)];
    }

    private static ConsoleMenuRegion[] BuildMenus(bool hasCategories, bool hasPlay, bool hasMedia)
    {
        var regions = new List<ConsoleMenuRegion>(5) { ConsoleMenuRegion.Search };
        if (hasPlay)
            regions.Add(ConsoleMenuRegion.Play);
        if (hasCategories)
            regions.Add(ConsoleMenuRegion.Categories);
        regions.Add(ConsoleMenuRegion.Games);
        if (hasMedia)
            regions.Add(ConsoleMenuRegion.Media);
        return [.. regions];
    }

    private static int Wrap(int index, int delta, int count)
    {
        if (count <= 0)
            return 0;

        var next = (index + delta) % count;
        if (next < 0)
            next += count;

        return next;
    }

    private static int Wrap(int index, int count)
    {
        if (count <= 0)
            return 0;

        var next = index % count;
        return next < 0 ? next + count : next;
    }
}
