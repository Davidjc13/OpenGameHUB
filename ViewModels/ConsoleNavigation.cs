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
    MediaToggle,
    MediaNext,
    MediaPrevious
}

public readonly record struct ConsoleNavigationState(int CategoryIndex, int GameIndex, bool SearchFocused);

public static class ConsoleNavigation
{
    public static (ConsoleNavigationState State, ConsoleInputEffect Effect) Apply(
        ConsoleNavigationState state,
        ConsoleInput input,
        int categoryCount,
        int gameCount,
        bool textConsumesHorizontal = false)
    {
        switch (input)
        {
            case ConsoleInput.MediaToggle:
                return (state, ConsoleInputEffect.MediaToggle);
            case ConsoleInput.MediaNext:
                return (state, ConsoleInputEffect.MediaNext);
            case ConsoleInput.MediaPrevious:
                return (state, ConsoleInputEffect.MediaPrevious);
            case ConsoleInput.Back when state.SearchFocused:
                return (state with { SearchFocused = false }, ConsoleInputEffect.ClearSearch);
            case ConsoleInput.Back:
                return (state, ConsoleInputEffect.Exit);
            case ConsoleInput.Activate:
                return (state, gameCount > 0 ? ConsoleInputEffect.Activate : ConsoleInputEffect.None);
            case ConsoleInput.Favorite:
                return (state, gameCount > 0 ? ConsoleInputEffect.Favorite : ConsoleInputEffect.None);
            case ConsoleInput.MoveLeft when textConsumesHorizontal:
            case ConsoleInput.MoveRight when textConsumesHorizontal:
                return (state, ConsoleInputEffect.None);
            case ConsoleInput.MoveLeft:
                return (state with { GameIndex = Wrap(state.GameIndex, -1, gameCount) }, ConsoleInputEffect.None);
            case ConsoleInput.MoveRight:
                return (state with { GameIndex = Wrap(state.GameIndex, 1, gameCount) }, ConsoleInputEffect.None);
            case ConsoleInput.MoveUp:
            case ConsoleInput.PreviousCategory:
                return (state with
                {
                    CategoryIndex = Wrap(state.CategoryIndex, -1, categoryCount),
                    GameIndex = 0
                }, ConsoleInputEffect.None);
            case ConsoleInput.MoveDown:
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

    private static int Wrap(int index, int delta, int count)
    {
        if (count <= 0)
            return 0;

        var next = (index + delta) % count;
        if (next < 0)
            next += count;

        return next;
    }
}
