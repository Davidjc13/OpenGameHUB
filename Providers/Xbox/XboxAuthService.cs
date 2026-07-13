using Avalonia.Controls;
using OpenGameHUB.Infrastructure.Browser;
using OpenGameHUB.Services.Auth;
using OpenGameHUB.Services.Configuration;

namespace OpenGameHUB.Providers.Xbox;

internal static class XboxAuthService
{
    public static async Task SignInAsync(SettingsService settings, Window ownerWindow)
    {
        var session = XboxAccountClient.CreateOAuthSession();

        EmbeddedBrowserService.EnsureAvailable();
        var authorizationCode = await EmbeddedBrowserService.ShowCaptureAsync<string>(
            new XboxAuthCaptureStrategy(session),
            ownerWindow);

        if (string.IsNullOrWhiteSpace(authorizationCode))
            throw new InvalidOperationException(Loc.T("XboxAuthCancelled"));

        // Exchange the code over HTTP (with the PKCE verifier); tokens are persisted via DPAPI.
        var client = new XboxAccountClient();
        await client.CompleteLoginAsync(authorizationCode, session.CodeVerifier);
        var gamertag = await client.GetGamertagAsync();
        XboxAuthHelper.PersistProfile(settings, gamertag);
    }

    internal static bool IsExpectedRedirect(string? url) =>
        AuthUrl.TryParse(url, out var uri)
        && uri.Host.Equals("login.live.com", StringComparison.OrdinalIgnoreCase)
        && AuthUrl.PathMatches(uri, "/oauth20_desktop.srf");

    internal static bool IsMatchingState(string? url, string expectedState)
    {
        if (string.IsNullOrWhiteSpace(expectedState) || !AuthUrl.TryParse(url, out var uri))
            return false;

        var actualState = GetQueryValue(uri, "state");
        return actualState is not null && string.Equals(actualState, expectedState, StringComparison.Ordinal);
    }

    internal static string? TryExtractAuthorizationCode(string? url)
    {
        if (!AuthUrl.TryParse(url, out var uri))
            return null;

        var code = GetQueryValue(uri, "code");
        return string.IsNullOrWhiteSpace(code) ? null : code;
    }

    // Reads a query parameter from the already-parsed Uri.Query component (no manual URL scanning).
    private static string? GetQueryValue(Uri uri, string key)
    {
        var query = uri.Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var name = separator >= 0 ? pair[..separator] : pair;
            if (!name.Equals(key, StringComparison.OrdinalIgnoreCase))
                continue;

            var value = separator >= 0 ? pair[(separator + 1)..] : string.Empty;
            return Uri.UnescapeDataString(value);
        }

        return null;
    }
}
