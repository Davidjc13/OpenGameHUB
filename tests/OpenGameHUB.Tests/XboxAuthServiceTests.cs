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

    [Fact]
    public void IsMatchingState_returns_true_for_identical_state()
    {
        var url = "https://login.live.com/oauth20_desktop.srf?code=abc&state=xyz123";

        Assert.True(XboxAuthService.IsMatchingState(url, "xyz123"));
    }

    [Fact]
    public void IsMatchingState_returns_false_for_mismatched_state()
    {
        var url = "https://login.live.com/oauth20_desktop.srf?code=abc&state=other";

        Assert.False(XboxAuthService.IsMatchingState(url, "xyz123"));
    }

    [Fact]
    public void IsMatchingState_returns_false_when_state_absent()
    {
        var url = "https://login.live.com/oauth20_desktop.srf?code=abc";

        Assert.False(XboxAuthService.IsMatchingState(url, "xyz123"));
    }

    [Fact]
    public void IsExpectedRedirect_accepts_live_host_only()
    {
        Assert.True(XboxAuthService.IsExpectedRedirect(
            "https://login.live.com/oauth20_desktop.srf?code=abc"));
        Assert.False(XboxAuthService.IsExpectedRedirect(
            "https://evil.example.com/oauth20_desktop.srf?code=abc"));
        Assert.False(XboxAuthService.IsExpectedRedirect("not a url"));
    }

    [Fact]
    public void ComputeCodeChallenge_matches_rfc7636_test_vector()
    {
        // From RFC 7636, Appendix B.
        var challenge = XboxAccountClient.ComputeCodeChallenge(
            "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk");

        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", challenge);
    }

    [Fact]
    public void CreateOAuthSession_includes_pkce_and_state_in_url()
    {
        var session = XboxAccountClient.CreateOAuthSession();

        Assert.False(string.IsNullOrWhiteSpace(session.State));
        Assert.False(string.IsNullOrWhiteSpace(session.CodeVerifier));
        Assert.Contains("code_challenge_method=S256", session.AuthorizeUrl);
        Assert.Contains("code_challenge=" + XboxAccountClient.ComputeCodeChallenge(session.CodeVerifier), session.AuthorizeUrl);
        Assert.Contains("state=" + session.State, session.AuthorizeUrl);
    }
}
