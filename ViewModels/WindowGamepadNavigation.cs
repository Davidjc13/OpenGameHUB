namespace OpenGameHUB.ViewModels;

public readonly record struct GridStep(int Index, int PageDelta);

public static class WindowGamepadNavigation
{
    public static GridStep Move(int currentIndex, int count, int columns, ConsoleInput input)
    {
        if (count <= 0)
            return new GridStep(-1, 0);

        var step = input switch
        {
            ConsoleInput.MoveLeft => -1,
            ConsoleInput.MoveRight => 1,
            ConsoleInput.MoveUp => -Math.Max(1, columns),
            ConsoleInput.MoveDown => Math.Max(1, columns),
            _ => 0
        };

        if (step == 0)
            return new GridStep(currentIndex, 0);

        if (currentIndex < 0)
            return new GridStep(step > 0 ? 0 : count - 1, 0);

        var next = currentIndex + step;
        if (next < 0)
            return new GridStep(count - 1, -1);
        if (next >= count)
            return new GridStep(0, 1);

        return new GridStep(next, 0);
    }
}
