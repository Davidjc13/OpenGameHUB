using System.Diagnostics;
using Avalonia.Controls;
using OpenGameHUB.Services.Auth;
using OpenGameHUB.ViewModels;
using OpenGameHUB.Views;

namespace OpenGameHUB.Providers.Xbox;

internal static class XboxAuthService
{
    public static async Task SignInAsync(SettingsService settings, Window ownerWindow)
    {
        var authorizationCode = await TryCaptureAuthorizationCodeAsync(ownerWindow);
        if (string.IsNullOrWhiteSpace(authorizationCode))
            throw new InvalidOperationException(Loc.T("XboxAuthCancelled"));

        var client = new XboxAccountClient();
        await client.CompleteLoginAsync(authorizationCode);
        var gamertag = await client.GetGamertagAsync();
        XboxAuthHelper.PersistProfile(settings, gamertag);
    }

    private static async Task<string?> TryCaptureAuthorizationCodeAsync(Window ownerWindow)
    {
        if (EmbeddedBrowserService.IsAvailable)
        {
            return await EmbeddedBrowserService.ShowCaptureAsync<string>(
                new XboxAuthCaptureStrategy(),
                ownerWindow);
        }

        return await SignInWithPasteFallbackAsync(ownerWindow);
    }

    private static async Task<string?> SignInWithPasteFallbackAsync(Window ownerWindow)
    {
        Process.Start(new ProcessStartInfo(XboxAccountClient.BuildAuthorizeUrl())
        {
            UseShellExecute = true
        });

        var viewModel = new XboxPasteAuthViewModel();
        var window = new XboxPasteAuthWindow(viewModel);
        await window.ShowDialog(ownerWindow);

        return TryExtractAuthorizationCode(viewModel.RedirectUrl.Trim());
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

    private static readonly System.Text.RegularExpressions.Regex AuthorizationCodeRegex =
        new(@"[?&]code=([^&]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
}
