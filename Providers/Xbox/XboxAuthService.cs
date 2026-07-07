using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using OpenGameHUB.Services.Auth;
using OpenGameHUB.Services.Configuration;
using OpenGameHUB.ViewModels;
using OpenGameHUB.Views;

namespace OpenGameHUB.Providers.Xbox;

internal static class XboxAuthService
{
    private static readonly Regex AuthorizationCodeRegex =
        new(@"[?&]code=([^&]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StateRegex =
        new(@"[?&]state=([^&]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static async Task SignInAsync(SettingsService settings, Window ownerWindow)
    {
        var session = XboxAccountClient.CreateOAuthSession();

        var authorizationCode = await TryCaptureAuthorizationCodeAsync(session, ownerWindow);
        if (string.IsNullOrWhiteSpace(authorizationCode))
            throw new InvalidOperationException(Loc.T("XboxAuthCancelled"));

        // Exchange the code over HTTP (with the PKCE verifier); tokens are persisted via DPAPI.
        var client = new XboxAccountClient();
        await client.CompleteLoginAsync(authorizationCode, session.CodeVerifier);
        var gamertag = await client.GetGamertagAsync();
        XboxAuthHelper.PersistProfile(settings, gamertag);
    }

    private static async Task<string?> TryCaptureAuthorizationCodeAsync(
        XboxOAuthSession session,
        Window ownerWindow)
    {
        if (EmbeddedBrowserService.IsAvailable)
        {
            return await EmbeddedBrowserService.ShowCaptureAsync<string>(
                new XboxAuthCaptureStrategy(session),
                ownerWindow);
        }

        return await SignInWithPasteFallbackAsync(session, ownerWindow);
    }

    private static async Task<string?> SignInWithPasteFallbackAsync(
        XboxOAuthSession session,
        Window ownerWindow)
    {
        Process.Start(new ProcessStartInfo(session.AuthorizeUrl)
        {
            UseShellExecute = true
        });

        var viewModel = new XboxPasteAuthViewModel();
        var window = new XboxPasteAuthWindow(viewModel);
        await window.ShowDialog(ownerWindow);

        var redirectUrl = viewModel.RedirectUrl.Trim();

        // The pasted URL must be the Microsoft redirect and carry our state.
        if (!IsExpectedRedirect(redirectUrl) || !IsMatchingState(redirectUrl, session.State))
            return null;

        return TryExtractAuthorizationCode(redirectUrl);
    }

    internal static bool IsExpectedRedirect(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Host.Equals("login.live.com", StringComparison.OrdinalIgnoreCase);

    internal static bool IsMatchingState(string? url, string expectedState)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(expectedState))
            return false;

        var match = StateRegex.Match(url);
        if (!match.Success)
            return false;

        var actualState = Uri.UnescapeDataString(match.Groups[1].Value);
        return string.Equals(actualState, expectedState, StringComparison.Ordinal);
    }

    internal static string? TryExtractAuthorizationCode(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var match = AuthorizationCodeRegex.Match(url);
        if (!match.Success)
            return null;

        return Uri.UnescapeDataString(match.Groups[1].Value);
    }
}
