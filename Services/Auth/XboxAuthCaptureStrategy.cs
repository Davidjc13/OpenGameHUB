using System.Text.Json;
using System.Text.RegularExpressions;
using OpenGameHUB.Providers.Xbox;

namespace OpenGameHUB.Services.Auth;

internal sealed class XboxAuthCaptureStrategy : IAuthCaptureStrategy
{
    public string StartUrl => XboxAccountClient.BuildAuthorizeUrl();

    public string WindowTitleKey => "EmbeddedBrowserXboxTitle";

    public string IntroKey => "EmbeddedBrowserXboxIntro";

    public string? WaitingStatusKey => null;

    public object? TryCaptureFromNavigation(string url)
    {
        if (!url.Contains("oauth20_desktop.srf", StringComparison.OrdinalIgnoreCase))
            return null;

        return XboxAuthService.TryExtractAuthorizationCode(url);
    }

    public Task<object?> TryCaptureFromDomAsync(
        Func<string, Task<string?>> executeScriptAsync,
        string currentUrl) =>
        Task.FromResult<object?>(null);
}
