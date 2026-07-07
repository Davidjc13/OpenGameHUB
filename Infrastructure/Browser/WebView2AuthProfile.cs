namespace OpenGameHUB.Infrastructure.Browser;

internal static class WebView2AuthProfile
{
    private static string RootDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenGameHUB",
            "AuthProfile");

    public static string CreateSessionFolder()
    {
        var path = Path.Combine(RootDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static void DeleteSessionFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best effort cleanup after auth
        }
    }
}
