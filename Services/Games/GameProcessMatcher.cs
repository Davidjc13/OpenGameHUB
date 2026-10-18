namespace OpenGameHUB.Services.Games;

internal static class GameProcessMatcher
{
    private static readonly HashSet<string> LauncherNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "steam",
        "steamwebhelper",
        "steamservice",
        "steamerrorreporter",
        "epicgameslauncher",
        "epicwebhelper",
        "eadesktop",
        "origin",
        "originwebhelperservice",
        "battle.net",
        "agent",
        "ubisoftconnect",
        "upc",
        "uplay",
        "riotclientservices",
        "riotclientux",
        "riotclientcrashhandler",
        "xboxpcapp",
        "gamingapp",
        "socialclubhelper",
        "legendary",
        "opengamehub"
    };

    private static readonly string[] BackgroundHelperTokens =
    [
        "crashhandler",
        "crashreporter",
        "crashpad",
        "errorreporter",
        "easyanticheat",
        "battleye",
        "beservice",
        "vanguard",
        "cefprocess",
        "cefsubproc",
        "webhelper",
        " clean",
        "redist",
        "unins",
        "setup"
    ];

    public static bool IsKnownLauncher(string? processNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(processNameOrPath))
            return false;

        var name = Path.GetFileNameWithoutExtension(processNameOrPath.Trim());
        if (string.IsNullOrEmpty(name))
            return false;

        if (LauncherNames.Contains(name))
            return true;

        foreach (var token in BackgroundHelperTokens)
        {
            if (name.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool IsUnderInstallPath(string? imagePath, string? installPath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || string.IsNullOrWhiteSpace(installPath))
            return false;

        try
        {
            var image = Path.GetFullPath(imagePath);
            var root = Path.GetFullPath(installPath);
            if (string.Equals(image, root, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!root.EndsWith(Path.DirectorySeparatorChar) && !root.EndsWith(Path.AltDirectorySeparatorChar))
                root += Path.DirectorySeparatorChar;

            return image.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool IsGameProcess(GameProcessInfo process, string? installPath)
    {
        if (IsKnownLauncher(process.ProcessName) || IsKnownLauncher(process.ImagePath))
            return false;

        return IsUnderInstallPath(process.ImagePath, installPath);
    }
}
