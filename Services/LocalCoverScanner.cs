namespace OpenGameHUB.Services;

internal static class LocalCoverScanner
{
    private static readonly string[] PreferredFileNames =
    [
        "cover.jpg", "cover.png", "cover.webp",
        "poster.jpg", "poster.png",
        "keyart.jpg", "keyart.png",
        "tile.jpg", "tile.png",
        "library_600x900.jpg", "header.jpg",
        "splash.jpg", "splash.png",
        "icon.png", "logo.png"
    ];

    private static readonly string[] PreferredNameFragments =
    [
        "cover", "poster", "keyart", "splash", "hero", "banner", "background", "tile"
    ];

    public static string? FindCover(string? installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
            return null;

        foreach (var fileName in PreferredFileNames)
        {
            var direct = Path.Combine(installPath, fileName);
            if (IsUsableImage(direct))
                return direct;
        }

        try
        {
            var candidates = Directory
                .EnumerateFiles(installPath, "*.*", SearchOption.AllDirectories)
                .Where(path => IsImageExtension(path) && IsUsableImage(path))
                .Where(path => PreferredNameFragments.Any(fragment =>
                    Path.GetFileName(path).Contains(fragment, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(path => new FileInfo(path).Length)
                .ToList();

            return candidates.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsImageExtension(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUsableImage(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            return new FileInfo(path).Length >= 20_000;
        }
        catch
        {
            return false;
        }
    }
}
