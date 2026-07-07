using Microsoft.Web.WebView2.Core;

namespace OpenGameHUB.Infrastructure.Browser;

internal static class WebView2Runtime
{
    public static bool IsAvailable()
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return !string.IsNullOrWhiteSpace(version);
        }
        catch
        {
            return false;
        }
    }

    public static string GetUserDataFolder()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenGameHUB",
            "WebView2");
        Directory.CreateDirectory(root);
        return root;
    }
}
