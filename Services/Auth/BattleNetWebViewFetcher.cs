using Avalonia;
using Avalonia.Controls;
using OpenGameHUB.Infrastructure.Browser;
using OpenGameHUB.Services.Auth;
using OpenGameHUB.Views.EmbeddedBrowser;

namespace OpenGameHUB.Services.Auth;

internal static class BattleNetWebViewFetcher
{
    public static async Task RunAsync(
        string profilePath,
        Window owner,
        Func<WebView2Host, Task> action,
        CancellationToken cancellationToken = default)
    {
        var window = new Window
        {
            Width = 800,
            Height = 600,
            Opacity = 0,
            ShowInTaskbar = false,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = new PixelPoint(-10000, -10000)
        };

        var host = new WebView2Host
        {
            UserDataFolder = profilePath,
            AllowedHosts = BattleNetAuthCaptureStrategy.Hosts
        };

        window.Content = host;
        window.Show();

        try
        {
            await host.EnsureInitializedAsync(cancellationToken).ConfigureAwait(true);
            await action(host).ConfigureAwait(true);
        }
        finally
        {
            host.Dispose();
            window.Close();
        }
    }
}
