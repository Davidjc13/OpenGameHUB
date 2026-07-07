using OpenGameHUB.Infrastructure.Browser;

namespace OpenGameHUB.Tests;

public sealed class AuthHostPolicyTests
{
    private static readonly string[] Allowed = ["steamcommunity.com", "login.live.com"];

    [Theory]
    [InlineData("steamcommunity.com")]
    [InlineData("store.steamcommunity.com")]
    [InlineData("LOGIN.LIVE.COM")]
    public void IsHostAllowed_accepts_exact_and_subdomains(string host)
    {
        Assert.True(AuthHostPolicy.IsHostAllowed(host, Allowed));
    }

    [Theory]
    [InlineData("evilsteamcommunity.com")]
    [InlineData("steamcommunity.com.evil.tld")]
    [InlineData("notlogin.live.com.attacker.net")]
    [InlineData("")]
    [InlineData(null)]
    public void IsHostAllowed_rejects_lookalikes_and_empty(string? host)
    {
        Assert.False(AuthHostPolicy.IsHostAllowed(host, Allowed));
    }

    [Fact]
    public void IsHostAllowed_rejects_when_no_allowlist()
    {
        Assert.False(AuthHostPolicy.IsHostAllowed("steamcommunity.com", []));
    }
}
