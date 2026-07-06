using OpenGameHUB.Providers.Xbox;

namespace OpenGameHUB.Tests;

public sealed class XboxAuthServiceTests
{
    [Fact]
    public void TryExtractAuthorizationCode_parses_query_parameter()
    {
        var code = XboxAuthService.TryExtractAuthorizationCode(
            "https://login.live.com/oauth20_desktop.srf?code=abc%2B123&state=x");

        Assert.Equal("abc+123", code);
    }

    [Fact]
    public void TryExtractAuthorizationCode_returns_null_for_missing_code()
    {
        Assert.Null(XboxAuthService.TryExtractAuthorizationCode("https://example.com/done"));
    }
}
