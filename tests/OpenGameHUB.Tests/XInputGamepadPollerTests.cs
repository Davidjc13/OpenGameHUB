using OpenGameHUB.Infrastructure.Input;

namespace OpenGameHUB.Tests;

public sealed class XInputGamepadPollerTests
{
    [Fact]
    public void Idle_interval_is_slower_than_interactive()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(400), XInputGamepadPoller.IdleInterval);
        Assert.Equal(TimeSpan.FromMilliseconds(33), XInputGamepadPoller.InteractiveInterval);
        Assert.True(XInputGamepadPoller.IdleInterval > XInputGamepadPoller.InteractiveInterval);
    }
}
