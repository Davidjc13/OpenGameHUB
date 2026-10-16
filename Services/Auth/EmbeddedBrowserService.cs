using Avalonia.Controls;
using OpenGameHUB.Infrastructure.Browser;
using OpenGameHUB.ViewModels;
using OpenGameHUB.Views;

namespace OpenGameHUB.Services.Auth;

internal static class EmbeddedBrowserService
{
    public static bool IsAvailable => WebView2Runtime.IsAvailable();

    public static void EnsureAvailable()
    {
        if (!IsAvailable)
            throw new InvalidOperationException(Loc.T("EmbeddedBrowserRuntimeMissing"));
    }

    public static Task<T?> ShowCaptureAsync<T>(
        IAuthCaptureStrategy strategy,
        Window owner,
        Func<object, T?>? convert = null) where T : class =>
        ShowCaptureAsync(strategy, owner, profilePath: null, deleteProfileOnClose: true, convert);

    public static async Task<T?> ShowCaptureAsync<T>(
        IAuthCaptureStrategy strategy,
        Window owner,
        string? profilePath,
        bool deleteProfileOnClose,
        Func<object, T?>? convert = null) where T : class
    {
        EnsureAvailable();

        var ownsProfile = string.IsNullOrWhiteSpace(profilePath);
        profilePath = ownsProfile
            ? WebView2AuthProfile.CreateSessionFolder()
            : profilePath!;

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
            if (ownsProfile || deleteProfileOnClose)
                WebView2AuthProfile.DeleteSessionFolder(profilePath);
        }
    }
}
