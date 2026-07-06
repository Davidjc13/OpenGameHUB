using OpenGameHUB.Providers.Rockstar;

namespace OpenGameHUB.Tests;

public sealed class RockstarCoverUrlsTests
{
    [Fact]
    public void GetOfficialCoverUrl_returns_fob_url_for_known_title()
    {
        var url = RockstarCoverUrls.GetOfficialCoverUrl("gta5");
        Assert.NotNull(url);
        Assert.Contains("gta.jpg", url);
    }

    [Fact]
    public void GetIgdbCoverUrls_yields_urls_for_known_title()
    {
        var urls = RockstarCoverUrls.GetIgdbCoverUrls("gta5", "Grand Theft Auto V").ToList();
        Assert.NotEmpty(urls);
        Assert.Contains("igdb.com", urls[0]);
    }
}
