using OpenGameHUB.Providers.Xbox;

namespace OpenGameHUB.Tests;

public sealed class XboxTitleFilterTests
{
    [Fact]
    public void IsPcLibraryTitle_accepts_pc_device_games()
    {
        var title = new XboxTitleEntry
        {
            type = "Game",
            pfn = "BethesdaSoftworks.DOOMEternal_3275kfvn8vcwc",
            devices = ["PC"]
        };

        Assert.True(XboxTitleFilter.IsPcLibraryTitle(title));
    }

    [Fact]
    public void IsPcLibraryTitle_accepts_store_listed_games_without_pc_device()
    {
        var title = new XboxTitleEntry
        {
            type = "Game",
            name = "DOOM Eternal (BATTLEMODE)",
            pfn = "BethesdaSoftworks.Base-DOOMEternal_3275kfvn8vcwc",
            devices = ["XboxOne", "XboxSeries"],
            modernTitleId = "331135949"
        };

        Assert.True(XboxTitleFilter.IsPcLibraryTitle(title));
    }

    [Fact]
    public void IsPcLibraryTitle_rejects_console_only_games_without_store_listing()
    {
        var title = new XboxTitleEntry
        {
            type = "Game",
            pfn = "Example.Game_8wekyb3d8bbwe",
            devices = ["XboxOne"]
        };

        Assert.False(XboxTitleFilter.IsPcLibraryTitle(title));
    }

    [Fact]
    public void IsPcLibraryTitle_rejects_non_game_entries()
    {
        var title = new XboxTitleEntry
        {
            type = "App",
            pfn = "Microsoft.XboxApp_8wekyb3d8bbwe",
            devices = ["PC"],
            modernTitleId = "123"
        };

        Assert.False(XboxTitleFilter.IsPcLibraryTitle(title));
    }
}
