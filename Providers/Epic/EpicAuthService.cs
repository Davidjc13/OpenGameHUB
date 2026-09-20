using Avalonia.Controls;
using OpenGameHUB.Services.Auth;
using OpenGameHUB.Services.Configuration;

namespace OpenGameHUB.Providers.Epic;

internal static class EpicAuthService
{
    public static async Task SignInAsync(SettingsService settings, Window ownerWindow)
    {
        EmbeddedBrowserService.EnsureAvailable();

        using var downloadCts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await LegendaryBootstrap.EnsureInstalledAsync(null, downloadCts.Token);
        LegendaryClient.InvalidateExecutableCache();

        if (!LegendaryClient.IsAvailable())
            throw new InvalidOperationException(Loc.T("EpicHelperUnavailable"));

        var authCode = await EmbeddedBrowserService.ShowCaptureAsync<string>(
            new EpicAuthCaptureStrategy(),
            ownerWindow);

        if (string.IsNullOrWhiteSpace(authCode))
            throw new InvalidOperationException(Loc.T("EpicAuthCancelled"));

        using var authCts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await LegendaryClient.RunAuthWithCodeAsync(authCode, authCts.Token);
        EpicAuthHelper.PersistFromLegendary(settings);
    }
}
