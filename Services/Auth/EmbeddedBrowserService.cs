using Avalonia.Controls;
using OpenGameHUB.Infrastructure.Browser;
using OpenGameHUB.ViewModels;
using OpenGameHUB.Views;

namespace OpenGameHUB.Services.Auth;

internal static class EmbeddedBrowserService
{
    public static bool IsAvailable => WebView2Runtime.IsAvailable();

    public static async Task<T?> ShowCaptureAsync<T>(
        IAuthCaptureStrategy strategy,
        Window owner,
        Func<object, T?>? convert = null) where T : class
    {
        if (!IsAvailable)
            return null;

        var profilePath = WebView2AuthProfile.CreateSessionFolder();
        try
        {
            // OAuth codes are exchanged over HTTP; resulting tokens stay in DPAPI-backed stores.
            var viewModel = new EmbeddedBrowserViewModel(strategy, profilePath);
            var window = new EmbeddedBrowserWindow(viewModel);
            await window.ShowDialog(owner).ConfigureAwait(true);

            if (viewModel.Result is null)
                return null;

            if (viewModel.Result is T typed)
                return typed;

            return convert?.Invoke(viewModel.Result);
        }
        finally
        {
            WebView2AuthProfile.DeleteSessionFolder(profilePath);
        }
    }
}
