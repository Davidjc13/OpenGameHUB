using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Infrastructure.Input;

public readonly record struct GamepadButtons(
    bool A,
    bool B,
    bool Y,
    bool Left,
    bool Right,
    bool LeftShoulder,
    bool RightShoulder,
    short ThumbX,
    bool Up = false,
    bool Down = false,
    short ThumbY = 0);

public sealed class GamepadRepeatState
{
    public int Direction { get; set; }

    public TimeSpan Elapsed { get; set; }

    public bool WaitingForInitialDelay { get; set; }

    public void Reset()
    {
        Direction = 0;
        Elapsed = TimeSpan.Zero;
        WaitingForInitialDelay = false;
    }
}

public static class GamepadInput
{
    public const short Deadzone = 12000;

    public static readonly TimeSpan InitialRepeat = TimeSpan.FromMilliseconds(420);

    public static readonly TimeSpan RepeatInterval = TimeSpan.FromMilliseconds(140);

    public static IReadOnlyList<ConsoleInput> Collect(
        GamepadButtons previous,
        GamepadButtons current,
        GamepadRepeatState repeat,
        TimeSpan elapsed)
    {
        var inputs = new List<ConsoleInput>(4);
        if (Pressed(previous.A, current.A))
            inputs.Add(ConsoleInput.Activate);
        if (Pressed(previous.B, current.B))
            inputs.Add(ConsoleInput.Back);
        if (Pressed(previous.Y, current.Y))
            inputs.Add(ConsoleInput.Favorite);
        if (Pressed(previous.LeftShoulder, current.LeftShoulder))
            inputs.Add(ConsoleInput.PreviousCategory);
        if (Pressed(previous.RightShoulder, current.RightShoulder))
            inputs.Add(ConsoleInput.NextCategory);

        var usedDpad = false;
        if (Pressed(previous.Left, current.Left))
        {
            inputs.Add(ConsoleInput.MoveLeft);
            usedDpad = true;
        }

        if (Pressed(previous.Right, current.Right))
        {
            inputs.Add(ConsoleInput.MoveRight);
            usedDpad = true;
        }

        if (Pressed(previous.Up, current.Up))
        {
            inputs.Add(ConsoleInput.MoveUp);
            usedDpad = true;
        }

        if (Pressed(previous.Down, current.Down))
        {
            inputs.Add(ConsoleInput.MoveDown);
            usedDpad = true;
        }

        if (!usedDpad)
            CollectStick(current.ThumbX, current.ThumbY, repeat, elapsed, inputs);

        return inputs;
    }

    private static void CollectStick(
        short thumbX,
        short thumbY,
        GamepadRepeatState repeat,
        TimeSpan elapsed,
        List<ConsoleInput> inputs)
    {
        var horizontal = Math.Abs((int)thumbX) >= Math.Abs((int)thumbY);
        var value = horizontal ? thumbX : thumbY;
        var direction = 0;
        if (value > Deadzone)
            direction = 1;
        else if (value < -Deadzone)
            direction = -1;

        if (direction == 0)
        {
            repeat.Reset();
            return;
        }

        var signed = direction * (horizontal ? 1 : 2);
        var input = horizontal
            ? direction > 0 ? ConsoleInput.MoveRight : ConsoleInput.MoveLeft
            : direction > 0 ? ConsoleInput.MoveUp : ConsoleInput.MoveDown;

        if (signed != repeat.Direction)
        {
            repeat.Direction = signed;
            repeat.Elapsed = TimeSpan.Zero;
            repeat.WaitingForInitialDelay = true;
            inputs.Add(input);
            return;
        }

        repeat.Elapsed += elapsed;
        if (repeat.WaitingForInitialDelay)
        {
            if (repeat.Elapsed < InitialRepeat)
                return;

            repeat.WaitingForInitialDelay = false;
            repeat.Elapsed = TimeSpan.Zero;
            inputs.Add(input);
            return;
        }

        if (repeat.Elapsed < RepeatInterval)
            return;

        repeat.Elapsed -= RepeatInterval;
        inputs.Add(input);
    }

    private static bool Pressed(bool previous, bool current) => !previous && current;
}
