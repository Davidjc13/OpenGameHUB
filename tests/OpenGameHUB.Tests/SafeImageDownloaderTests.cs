using OpenGameHUB.Infrastructure.Http;

namespace OpenGameHUB.Tests;

public sealed class SafeImageDownloaderTests
{
    [Theory]
    [InlineData("https://cdn.cloudflare.steamstatic.com/steam/apps/570/library_600x900.jpg", true)]
    [InlineData("https://images.igdb.com/igdb/image/upload/t_cover_big/co1wyy.jpg", true)]
    [InlineData("https://cdn2.steamgriddb.com/grid/abc.png", true)]
    [InlineData("https://upload.wikimedia.org/wikipedia/en/thumb/a/ab/example.jpg", true)]
    [InlineData("https://cdn1.epicgames.com/example/cover.jpg", true)]
    [InlineData("https://ubistatic3-a.akamaihd.net/orbit/uplay_launcher_3_0/assets/thumb.jpg", true)]
    [InlineData("https://media-rockstargames-com.akamaized.net/rockstargames-newsite/img/global/games/fob/1280/gta.jpg", true)]
    [InlineData("https://cdn.example.com/cover.jpg", false)]
    [InlineData("https://evilsteamstatic.com/cover.jpg", false)]
    [InlineData("https://steamstatic.com.evil.tld/cover.jpg", false)]
    [InlineData("http://localhost/cover.jpg", true)]
    [InlineData("http://evil.example.com/cover.jpg", false)]
    [InlineData("file:///C:/temp/x.jpg", false)]
    [InlineData("not-a-url", false)]
    public void IsAllowedDownloadUrl_validates_hosts(string url, bool expected)
    {
        Assert.Equal(expected, SafeImageDownloader.IsAllowedDownloadUrl(url));
    }
}
