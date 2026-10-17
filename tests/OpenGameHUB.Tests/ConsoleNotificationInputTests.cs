using OpenGameHUB.Infrastructure.Input;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Tests;

public sealed class ConsoleNotificationInputTests
{
    [Fact]
    public void L3_toggles_inbox_and_blocks_navigation()
    {
        var input = new ConsoleNotificationInput();
        var l3 = Idle with { LeftThumb = true };

        var result = input.Process(
            Idle,
            l3,
            TimeSpan.Zero,
            inboxOpen: false,
            hasActiveBanner: false,
            replying: false);

        Assert.Equal([ConsoleNotificationAction.ToggleInbox], result.Actions);
        Assert.True(result.BlockNavigation);
    }

    [Fact]
    public void Pressing_A_while_banner_is_visible_opens_latest_notification()
    {
        var input = new ConsoleNotificationInput();
        var pressA = Idle with { A = true };

        var result = input.Process(
            Idle,
            pressA,
            TimeSpan.Zero,
            inboxOpen: false,
            hasActiveBanner: true,
            replying: false);

        Assert.Equal([ConsoleNotificationAction.OpenLatestFromBanner], result.Actions);
        Assert.True(result.BlockNavigation);
    }

    [Fact]
    public void Inbox_consumes_navigation_and_supports_reply_and_close()
    {
        var input = new ConsoleNotificationInput();

        var down = input.Process(
            Idle,
            Idle with { Down = true },
            TimeSpan.Zero,
            inboxOpen: true,
            hasActiveBanner: false,
            replying: false);
        Assert.Equal([ConsoleNotificationAction.MoveDown], down.Actions);
        Assert.True(down.BlockNavigation);

        var reply = input.Process(
            Idle,
            Idle with { A = true },
            TimeSpan.Zero,
            inboxOpen: true,
            hasActiveBanner: false,
            replying: false);
        Assert.Equal([ConsoleNotificationAction.BeginReply], reply.Actions);

        var close = input.Process(
            Idle,
            Idle with { B = true },
            TimeSpan.Zero,
            inboxOpen: true,
            hasActiveBanner: false,
            replying: false);
        Assert.Equal([ConsoleNotificationAction.CloseInbox], close.Actions);
    }

    [Fact]
    public void Replying_delegates_buttons_to_on_screen_keyboard()
    {
        var input = new ConsoleNotificationInput();

        var result = input.Process(
            Idle,
            Idle with { B = true },
            TimeSpan.Zero,
            inboxOpen: true,
            hasActiveBanner: false,
            replying: true);

        Assert.Empty(result.Actions);
        Assert.False(result.BlockNavigation);
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
