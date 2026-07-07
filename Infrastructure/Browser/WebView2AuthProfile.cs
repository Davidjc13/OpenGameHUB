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
        // Purge any profiles left behind by a crash/kill before starting a new session.
        CleanupOrphanedProfiles();

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

    /// <summary>
    /// Removes every leftover session profile. Safe to call at startup: auth sessions are
    /// modal and one-at-a-time, so any directory found here is orphaned (e.g. the app or
    /// WebView2 crashed before <see cref="DeleteSessionFolder"/> ran).
    /// </summary>
    public static void CleanupOrphanedProfiles()
    {
        try
        {
            if (!Directory.Exists(RootDirectory))
                return;

            foreach (var directory in Directory.EnumerateDirectories(RootDirectory))
                DeleteSessionFolder(directory);
        }
        catch
        {
            // best effort; a locked leftover folder will be retried on the next session
        }
    }
}
