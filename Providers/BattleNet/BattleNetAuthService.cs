using Avalonia.Controls;
using OpenGameHUB.Infrastructure.Browser;
using OpenGameHUB.Services.Auth;
namespace OpenGameHUB.Providers.BattleNet;

internal static class BattleNetAuthService
{
    public static async Task SignInAsync(SettingsService settings, Window ownerWindow)
    {
        EmbeddedBrowserService.EnsureAvailable();

        var profilePath = WebView2AuthProfile.GetBattleNetProfilePath();
        var capture = await EmbeddedBrowserService.ShowCaptureAsync<BattleNetAuthCaptureResult>(
            new BattleNetAuthCaptureStrategy(),
            ownerWindow,
            profilePath,
            deleteProfileOnClose: false).ConfigureAwait(true);

        if (capture is null)
            throw new InvalidOperationException(Loc.T("BattleNetAuthCancelled"));

        // Library sync runs in the login WebView before the dialog closes (same cookie jar).
        BattleNetAuthHelper.PersistProfile(settings, capture.DisplayName);
    }
}
