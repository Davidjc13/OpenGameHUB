using OpenGameHUB.Infrastructure.Input;

namespace OpenGameHUB.ViewModels;

public enum ConsoleNotificationAction
{
    ToggleInbox,
    OpenLatestFromBanner,
    CloseInbox,
    MoveUp,
    MoveDown,
    BeginReply
}

public sealed class ConsoleNotificationInput
{
    private readonly GamepadRepeatState _repeat = new();

    public void Reset() => _repeat.Reset();

    public (IReadOnlyList<ConsoleNotificationAction> Actions, bool BlockNavigation) Process(
        GamepadButtons previous,
        GamepadButtons current,
        TimeSpan elapsed,
        bool inboxOpen,
        bool hasActiveBanner,
        bool replying)
    {
        var actions = new List<ConsoleNotificationAction>(2);

        if (Pressed(previous.LeftThumb, current.LeftThumb))
        {
            _repeat.Reset();
            actions.Add(ConsoleNotificationAction.ToggleInbox);
            return (actions, true);
        }

        if (replying)
        {
            _repeat.Reset();
            return (actions, false);
        }

        if (inboxOpen)
        {
            var inputs = GamepadInput.Collect(previous, current, _repeat, elapsed);
            foreach (var input in inputs)
            {
                switch (input)
                {
                    case ConsoleInput.Back:
                        actions.Add(ConsoleNotificationAction.CloseInbox);
                        break;
                    case ConsoleInput.Activate:
                        actions.Add(ConsoleNotificationAction.BeginReply);
                        break;
                    case ConsoleInput.MoveUp:
                    case ConsoleInput.PreviousCategory:
                        actions.Add(ConsoleNotificationAction.MoveUp);
                        break;
                    case ConsoleInput.MoveDown:
                    case ConsoleInput.NextCategory:
                        actions.Add(ConsoleNotificationAction.MoveDown);
                        break;
                }
            }

            return (actions, true);
        }

        _repeat.Reset();
        if (hasActiveBanner && Pressed(previous.A, current.A))
        {
            actions.Add(ConsoleNotificationAction.OpenLatestFromBanner);
            return (actions, true);
        }

        return (actions, false);
    }

    private static bool Pressed(bool previous, bool current) => !previous && current;
}
