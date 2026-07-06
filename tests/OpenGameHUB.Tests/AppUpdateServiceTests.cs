using OpenGameHUB.Services.Updates;

namespace OpenGameHUB.Tests;

public sealed class AppUpdateServiceTests
{
    [Fact]
    public void IsNewer_returns_false_for_empty_latest_tag()
    {
        Assert.False(AppUpdateService.IsNewer("", "alpha-0.0.10"));
    }

    [Fact]
    public void IsNewer_returns_true_in_dev_build_when_latest_tag_is_present()
    {
        Assert.True(AppUpdateService.IsDevBuild);
        Assert.True(AppUpdateService.IsNewer("alpha-0.0.10", "alpha-0.0.10"));
    }
}
