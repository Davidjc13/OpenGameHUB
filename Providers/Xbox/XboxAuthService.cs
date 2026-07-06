using System.Text.RegularExpressions;
using System.Diagnostics;
using Avalonia.Controls;
using OpenGameHUB.ViewModels;
using OpenGameHUB.Views;

namespace OpenGameHUB.Providers.Xbox;

internal static class XboxAuthService
{
    private static readonly Regex AuthorizationCodeRegex =
        new(@"[?&]code=([^&]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static async Task SignInAsync(SettingsService settings, Window ownerWindow)
    {
        Process.Start(new ProcessStartInfo(XboxAccountClient.BuildAuthorizeUrl())
        {
            UseShellExecute = true
        });

        var viewModel = new XboxPasteAuthViewModel();
        var window = new XboxPasteAuthWindow(viewModel);
        await window.ShowDialog(ownerWindow);

        var authorizationCode = TryExtractAuthorizationCode(viewModel.RedirectUrl.Trim());
        if (string.IsNullOrWhiteSpace(authorizationCode))
            throw new InvalidOperationException(Loc.T("XboxAuthCancelled"));

        var client = new XboxAccountClient();
        await client.CompleteLoginAsync(authorizationCode);
        var gamertag = await client.GetGamertagAsync();
        XboxAuthHelper.PersistProfile(settings, gamertag);
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
