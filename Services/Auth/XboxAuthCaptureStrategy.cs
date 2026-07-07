using OpenGameHUB.Providers.Xbox;

namespace OpenGameHUB.Services.Auth;

internal sealed class XboxAuthCaptureStrategy : IAuthCaptureStrategy
{
    private static readonly string[] Hosts =
    [
        "login.live.com",
        "login.microsoftonline.com",
        "account.live.com",
        "signup.live.com",
        "microsoft.com"
    ];

    public string StartUrl => XboxAccountClient.BuildAuthorizeUrl();

    public string WindowTitleKey => "EmbeddedBrowserXboxTitle";

    public string IntroKey => "EmbeddedBrowserXboxIntro";

    public IReadOnlyList<string> AllowedHosts => Hosts;

    public object? TryCaptureFromNavigation(string url)
    {
        if (!url.Contains("oauth20_desktop.srf", StringComparison.OrdinalIgnoreCase))
            return null;

        return XboxAuthService.TryExtractAuthorizationCode(url);
    }

    public object? TryCaptureFromResponse(string requestUrl, string responseBody) => null;
}