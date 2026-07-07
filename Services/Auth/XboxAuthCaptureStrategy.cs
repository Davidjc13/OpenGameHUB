using OpenGameHUB.Providers.Xbox;

namespace OpenGameHUB.Services.Auth;

internal sealed class XboxAuthCaptureStrategy : IAuthCaptureStrategy
{
    private static readonly string[] Hosts =
    [
        "login.live.com",
        "login.microsoftonline.com",
        "account.live.com",
        "signup.live.com"
    ];

    private readonly XboxOAuthSession _session;

    public XboxAuthCaptureStrategy(XboxOAuthSession session) => _session = session;

    public string StartUrl => _session.AuthorizeUrl;

    public string WindowTitleKey => "EmbeddedBrowserXboxTitle";

    public string IntroKey => "EmbeddedBrowserXboxIntro";

    public IReadOnlyList<string> AllowedHosts => Hosts;

    public object? TryCaptureFromNavigation(string url)
    {
        if (!url.Contains("oauth20_desktop.srf", StringComparison.OrdinalIgnoreCase))
            return null;

        // Reject the redirect unless the returned state matches our request (CSRF binding).
        if (!XboxAuthService.IsMatchingState(url, _session.State))
            return null;

        return XboxAuthService.TryExtractAuthorizationCode(url);
    }

    public object? TryCaptureFromResponse(string requestUrl, string responseBody) => null;
}
