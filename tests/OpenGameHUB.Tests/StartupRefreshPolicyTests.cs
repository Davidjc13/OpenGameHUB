using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Tests;

public sealed class StartupRefreshPolicyTests
{
    [Fact]
    public void Should_refresh_when_cache_is_empty()
    {
        var settings = new AppSettings { RefreshLibraryOnStartup = false };

        Assert.True(MainWindowViewModel.ShouldRefreshLibraryOnStartup([], settings));
    }

    [Fact]
    public void Should_skip_refresh_when_cache_exists_and_setting_off()
    {
        var settings = new AppSettings { RefreshLibraryOnStartup = false };
        var cached = new[] { CreateGame("one") };

        Assert.False(MainWindowViewModel.ShouldRefreshLibraryOnStartup(cached, settings));
    }

    [Fact]
    public void Should_refresh_when_cache_exists_and_setting_on()
    {
        var settings = new AppSettings { RefreshLibraryOnStartup = true };
        var cached = new[] { CreateGame("one") };

        Assert.True(MainWindowViewModel.ShouldRefreshLibraryOnStartup(cached, settings));
    }

    private static GameItemViewModel CreateGame(string id) =>
        new(new UnifiedGame
        {
            Id = id,
            Title = id,
            Platform = Platform.Steam,
            PlatformGameId = id,
            LaunchSpec = LaunchSpec.Executable("game.exe")
        });
}
