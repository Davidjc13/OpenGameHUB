using OpenGameHUB.Infrastructure.Browser;

namespace OpenGameHUB.Tests;

public sealed class AuthUrlTests
{
    [Theory]
    [InlineData("https://login.live.com/oauth20_desktop.srf?code=abc")]
    [InlineData("https://steamcommunity.com/dev/apikey")]
    public void TryParse_accepts_absolute_https(string url)
    {
        Assert.True(AuthUrl.TryParse(url, out var uri));
        Assert.Equal("https", uri.Scheme);
    }

    [Theory]
    [InlineData("http://login.live.com/oauth20_desktop.srf?code=abc")]
    [InlineData("ftp://example.com/x")]
    [InlineData("/relative/path")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_rejects_non_https_or_malformed(string? url)
    {
        Assert.False(AuthUrl.TryParse(url, out _));
    }

    [Fact]
    public void PathMatches_compares_whole_segments()
    {
        Assert.True(AuthUrl.TryParse("https://steamcommunity.com/dev/apikey", out var uri));
        Assert.True(AuthUrl.PathMatches(uri, "/dev/apikey"));
        Assert.True(AuthUrl.PathMatches(uri, "dev/apikey"));

        Assert.True(AuthUrl.TryParse("https://steamcommunity.com/dev/apikey/extra", out var nested));
        Assert.True(AuthUrl.PathMatches(nested, "/dev/apikey"));

        Assert.True(AuthUrl.TryParse("https://steamcommunity.com/dev/apikey-evil", out var lookalike));
        Assert.False(AuthUrl.PathMatches(lookalike, "/dev/apikey"));
    }
}
