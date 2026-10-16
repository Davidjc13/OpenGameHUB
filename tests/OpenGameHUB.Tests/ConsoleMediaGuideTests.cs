using OpenGameHUB.Infrastructure.Input;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Tests;

public sealed class ConsoleMediaGuideTests
{
    [Fact]
    public void X_tap_toggles_playback()
    {
        var guide = new ConsoleMediaGuideInput();
        var held = Idle with { X = true };

        var press = guide.Process(Idle, held, TimeSpan.Zero);
        var release = guide.Process(held, Idle, TimeSpan.FromMilliseconds(80));

        Assert.Empty(press.Actions);
        Assert.True(press.BlockNavigation);
        Assert.Equal([ConsoleMediaAction.TogglePlay], release.Actions);
        Assert.False(guide.IsGuideOpen);
    }

    [Fact]
    public void Holding_X_opens_the_guide_without_toggling()
    {
        var guide = new ConsoleMediaGuideInput();
        var held = Idle with { X = true };

        guide.Process(Idle, held, TimeSpan.Zero);
        var opened = guide.Process(held, held, ConsoleMediaGuideInput.HoldThreshold);

        Assert.Equal([ConsoleMediaAction.GuideOpened], opened.Actions);
        Assert.True(opened.BlockNavigation);
        Assert.True(guide.IsGuideOpen);

        var stillHeld = guide.Process(held, held, TimeSpan.FromMilliseconds(50));
        Assert.Empty(stillHeld.Actions);

        var release = guide.Process(held, Idle, TimeSpan.FromMilliseconds(50));
        Assert.Empty(release.Actions);
        Assert.True(guide.IsGuideOpen);
    }

    [Fact]
    public void Guide_stays_open_after_releasing_X_until_B()
    {
        var guide = OpenGuide();
        var held = Idle with { X = true };

        guide.Process(held, Idle, TimeSpan.FromMilliseconds(50));
        Assert.True(guide.IsGuideOpen);

        var left = Idle with { Left = true };
        var skip = guide.Process(Idle, left, TimeSpan.Zero);
        Assert.Equal([ConsoleMediaAction.SkipPrev], skip.Actions);

        var closed = guide.Process(Idle, Idle with { B = true }, TimeSpan.Zero);
        Assert.Equal([ConsoleMediaAction.GuideClosed], closed.Actions);
        Assert.False(guide.IsGuideOpen);
    }

    [Fact]
    public void Holding_X_reports_charge_progress()
    {
        var guide = new ConsoleMediaGuideInput();
        var held = Idle with { X = true };

        guide.Process(Idle, held, TimeSpan.Zero);
        Assert.True(guide.IsCharging);
        Assert.Equal(0, guide.HoldProgress);

        guide.Process(held, held, TimeSpan.FromMilliseconds(175));
        Assert.True(guide.IsCharging);
        Assert.InRange(guide.HoldProgress, 0.4, 0.6);

        guide.Process(held, held, ConsoleMediaGuideInput.HoldThreshold);
        Assert.False(guide.IsCharging);
        Assert.Equal(0, guide.HoldProgress);
        Assert.True(guide.IsGuideOpen);
    }

    [Fact]
    public void Guide_dpad_skips_tracks()
    {
        var guide = OpenGuide();
        var held = Idle with { X = true };
        var left = held with { Left = true };
        var right = held with { Right = true };

        var previous = guide.Process(held, left, TimeSpan.Zero);
        var next = guide.Process(held, right, TimeSpan.Zero);

        Assert.Equal([ConsoleMediaAction.SkipPrev], previous.Actions);
        Assert.Equal([ConsoleMediaAction.SkipNext], next.Actions);
        Assert.True(previous.BlockNavigation);
    }

    [Fact]
    public void View_opens_search_and_closes_the_guide()
    {
        var guide = OpenGuide();
        var view = Idle with { Back = true };

        var opened = guide.Process(Idle with { X = true }, Idle, TimeSpan.FromMilliseconds(50));
        Assert.True(guide.IsGuideOpen);
        Assert.Empty(opened.Actions);

        var search = guide.Process(Idle, view, TimeSpan.Zero);
        Assert.Equal([ConsoleMediaAction.GuideClosed, ConsoleMediaAction.Search], search.Actions);
        Assert.False(guide.IsGuideOpen);
    }

    [Fact]
    public void View_searches_when_the_guide_is_closed()
    {
        var guide = new ConsoleMediaGuideInput();
        var view = Idle with { Back = true };

        var search = guide.Process(Idle, view, TimeSpan.Zero);

        Assert.Equal([ConsoleMediaAction.Search], search.Actions);
    }

    [Fact]
    public void Guide_triggers_repeat_volume()
    {
        var guide = OpenGuide();
        var held = Idle with { X = true };
        var trigger = held with { RightTrigger = 200 };

        var initial = guide.Process(held, trigger, TimeSpan.Zero);
        var tooSoon = guide.Process(trigger, trigger, TimeSpan.FromMilliseconds(100));
        var afterDelay = guide.Process(trigger, trigger, GamepadInput.InitialRepeat);

        Assert.Equal([ConsoleMediaAction.VolumeUp], initial.Actions);
        Assert.Empty(tooSoon.Actions);
        Assert.Equal([ConsoleMediaAction.VolumeUp], afterDelay.Actions);
    }

    [Fact]
    public void B_closes_the_guide_without_a_media_toggle()
    {
        var guide = OpenGuide();
        var held = Idle with { X = true };
        var cancel = held with { B = true };

        var closed = guide.Process(held, cancel, TimeSpan.Zero);

        Assert.Equal([ConsoleMediaAction.GuideClosed], closed.Actions);
        Assert.True(closed.BlockNavigation);
        Assert.False(guide.IsGuideOpen);

        var stillX = guide.Process(cancel, held, TimeSpan.FromMilliseconds(50));
        Assert.Empty(stillX.Actions);

        var release = guide.Process(held, Idle, TimeSpan.FromMilliseconds(50));
        Assert.Empty(release.Actions);
    }

    private static ConsoleMediaGuideInput OpenGuide()
    {
        var guide = new ConsoleMediaGuideInput();
        var held = Idle with { X = true };
        guide.Process(Idle, held, TimeSpan.Zero);
        guide.Process(held, held, ConsoleMediaGuideInput.HoldThreshold);
        return guide;
    }

    private static GamepadButtons Idle => new(
        A: false,
        B: false,
        Y: false,
        Left: false,
        Right: false,
        LeftShoulder: false,
        RightShoulder: false,
        ThumbX: 0);
}
