using System.Text.Json;
using OpenGameHUB.Infrastructure.Browser;

namespace OpenGameHUB.Services.Auth;

internal sealed record BattleNetAuthCaptureResult(string DisplayName);

internal sealed class BattleNetAuthCaptureStrategy : IAuthCaptureStrategy
{
    /// <summary>
    /// Login uses oauth.battle.net and other *.battle.net hosts; a narrow allowlist blocks the second step.
    /// </summary>
    internal static readonly string[] Hosts =
    [
        "battle.net",
        "blizzard.com",
        "blzstatic.cn"
    ];

    /// <summary>
    /// Playnite-style: hit logout first so the user gets a clean email/password + MFA flow.
    /// </summary>
    private const string LogoutUrl = "https://account.battle.net/api/logout";

    public string StartUrl => LogoutUrl;

    public bool UseNavigationCompletedForCapture => true;

    public string WindowTitleKey => "EmbeddedBrowserBattleNetTitle";

    public string IntroKey => "EmbeddedBrowserBattleNetIntro";

    public IReadOnlyList<string> AllowedHosts => Hosts;

    public object? TryCaptureFromNavigation(string url) => null;

    public object? TryCaptureFromNavigationCompleted(string url)
    {
        if (!AuthUrl.TryParse(url, out var uri) || !AuthHostPolicy.IsHostAllowed(uri.Host, Hosts))
            return null;

        if (IsLoginOrLogoutPath(uri.AbsolutePath))
            return null;

        if (AuthUrl.PathMatches(uri, "/overview")
            || IsLoggedInAccountHome(uri))
        {
            return new BattleNetAuthCaptureResult(string.Empty);
        }

        return null;
    }

    private static bool IsLoggedInAccountHome(Uri uri)
    {
        if (!uri.Host.Contains("account.battle.net", StringComparison.OrdinalIgnoreCase))
            return false;

        return string.IsNullOrEmpty(uri.AbsolutePath)
               || uri.AbsolutePath == "/";
    }

    private static bool IsLoginOrLogoutPath(string path) =>
        path.Contains("/login", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/logout", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/recovery", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/challenge", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/authenticator", StringComparison.OrdinalIgnoreCase);

    public object? TryCaptureFromResponse(string requestUrl, string responseBody)
    {
        if (!requestUrl.Contains("account.battle.net/api", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("authenticated", out var authenticated)
                && authenticated.ValueKind == JsonValueKind.True)
            {
                return new BattleNetAuthCaptureResult(string.Empty);
            }
        }
        catch
        {
            // wait for a valid auth callback
        }

        return null;
    }
}
