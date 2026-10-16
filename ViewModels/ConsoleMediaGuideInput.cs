using OpenGameHUB.Infrastructure.Input;

namespace OpenGameHUB.ViewModels;

public enum ConsoleMediaAction
{
    TogglePlay,
    SkipPrev,
    SkipNext,
    VolumeUp,
    VolumeDown,
    Mute,
    Search,
    GuideOpened,
    GuideClosed
}

public sealed class ConsoleMediaGuideInput
{
    public static readonly TimeSpan HoldThreshold = TimeSpan.FromMilliseconds(350);

    public const byte TriggerThreshold = 30;

    private readonly GamepadRepeatState _triggerRepeat = new();
    private TimeSpan _xHeld;
    private bool _xDown;
    private bool _openedGuideThisPress;
    private bool _ignoreXUntilRelease;

    public bool IsGuideOpen { get; private set; }

    public bool IsCharging { get; private set; }

    public double HoldProgress { get; private set; }

    public bool BlocksNavigation => IsGuideOpen || (_xDown && !_ignoreXUntilRelease);

    public void Reset()
    {
        _triggerRepeat.Reset();
        _xHeld = TimeSpan.Zero;
        _xDown = false;
        _openedGuideThisPress = false;
        _ignoreXUntilRelease = false;
        IsGuideOpen = false;
        IsCharging = false;
        HoldProgress = 0;
    }

    public (IReadOnlyList<ConsoleMediaAction> Actions, bool BlockNavigation) Process(
        GamepadButtons previous,
        GamepadButtons current,
        TimeSpan elapsed)
    {
        var actions = new List<ConsoleMediaAction>(4);

        if (Pressed(previous.B, current.B))
        {
            if (IsGuideOpen)
            {
                CloseGuide(actions);
                _ignoreXUntilRelease = current.X;
                RefreshCharge();
                return (actions, true);
            }

            if (_xDown)
            {
                _ignoreXUntilRelease = current.X;
                _xHeld = TimeSpan.Zero;
                RefreshCharge();
                return (actions, true);
            }
        }

        if (Pressed(previous.Back, current.Back) || Pressed(previous.Start, current.Start))
        {
            if (IsGuideOpen)
                CloseGuide(actions);

            actions.Add(ConsoleMediaAction.Search);
            RefreshCharge();
            return (actions, BlocksNavigation);
        }

        TrackHold(current, elapsed, actions);

        if (IsGuideOpen)
        {
            if (Pressed(previous.Left, current.Left))
                actions.Add(ConsoleMediaAction.SkipPrev);
            if (Pressed(previous.Right, current.Right))
                actions.Add(ConsoleMediaAction.SkipNext);

            CollectTriggers(current, elapsed, actions);
        }
        else
        {
            _triggerRepeat.Reset();
        }

        RefreshCharge();
        return (actions, BlocksNavigation);
    }

    private void TrackHold(GamepadButtons current, TimeSpan elapsed, List<ConsoleMediaAction> actions)
    {
        if (!current.X)
        {
            if (_xDown && !_openedGuideThisPress && !_ignoreXUntilRelease)
                actions.Add(ConsoleMediaAction.TogglePlay);

            _xDown = false;
            _xHeld = TimeSpan.Zero;
            _openedGuideThisPress = false;
            _ignoreXUntilRelease = false;
            return;
        }

        if (!_xDown)
        {
            _xDown = true;
            _xHeld = TimeSpan.Zero;
            _openedGuideThisPress = false;
            return;
        }

        if (_openedGuideThisPress || _ignoreXUntilRelease)
            return;

        _xHeld += elapsed;
        if (_xHeld < HoldThreshold)
            return;

        IsGuideOpen = true;
        _openedGuideThisPress = true;
        actions.Add(ConsoleMediaAction.GuideOpened);
    }

    private void CloseGuide(List<ConsoleMediaAction> actions)
    {
        if (!IsGuideOpen)
            return;

        IsGuideOpen = false;
        _triggerRepeat.Reset();
        actions.Add(ConsoleMediaAction.GuideClosed);
    }

    private void RefreshCharge()
    {
        IsCharging = _xDown && !IsGuideOpen && !_openedGuideThisPress && !_ignoreXUntilRelease;
        HoldProgress = IsCharging
            ? Math.Clamp(_xHeld / HoldThreshold, 0, 1)
            : 0;
    }

    private void CollectTriggers(GamepadButtons current, TimeSpan elapsed, List<ConsoleMediaAction> actions)
    {
        var left = current.LeftTrigger >= TriggerThreshold;
        var right = current.RightTrigger >= TriggerThreshold;
        var direction = 0;
        if (left && right)
            direction = current.RightTrigger >= current.LeftTrigger ? 1 : -1;
        else if (right)
            direction = 1;
        else if (left)
            direction = -1;

        if (direction == 0)
        {
            _triggerRepeat.Reset();
            return;
        }

        var input = direction > 0 ? ConsoleMediaAction.VolumeUp : ConsoleMediaAction.VolumeDown;
        if (direction != _triggerRepeat.Direction)
        {
            _triggerRepeat.Direction = direction;
            _triggerRepeat.Elapsed = TimeSpan.Zero;
            _triggerRepeat.WaitingForInitialDelay = true;
            actions.Add(input);
            return;
        }

        _triggerRepeat.Elapsed += elapsed;
        if (_triggerRepeat.WaitingForInitialDelay)
        {
            if (_triggerRepeat.Elapsed < GamepadInput.InitialRepeat)
                return;

            _triggerRepeat.WaitingForInitialDelay = false;
            _triggerRepeat.Elapsed = TimeSpan.Zero;
            actions.Add(input);
            return;
        }

        if (_triggerRepeat.Elapsed < GamepadInput.RepeatInterval)
            return;

        _triggerRepeat.Elapsed -= GamepadInput.RepeatInterval;
        actions.Add(input);
    }

    private static bool Pressed(bool previous, bool current) => !previous && current;
}
