using OpenGameHUB.Services.Auth;

namespace OpenGameHUB.Tests;

public sealed class EpicAuthCaptureStrategyTests
{
    [Fact]
    public void TryParseAuthorizationCode_reads_json_property()
    {
        var code = EpicAuthCaptureStrategy.TryParseAuthorizationCode(
            """{"authorizationCode":"abc123","expiresIn":300}""");

        Assert.Equal("abc123", code);
    }

    [Fact]
    public void TryParseAuthorizationCode_reads_regex_fallback()
    {
        var code = EpicAuthCaptureStrategy.TryParseAuthorizationCode(
            """some text "authorizationCode":"xyz789" trailing""");

        Assert.Equal("xyz789", code);
    }

    [Fact]
    public void TryParseAuthorizationCode_returns_null_for_missing_code()
    {
        Assert.Null(EpicAuthCaptureStrategy.TryParseAuthorizationCode("not auth data"));
    }
}
