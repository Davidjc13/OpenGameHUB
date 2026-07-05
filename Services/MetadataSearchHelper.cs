namespace OpenGameHUB.Services;

internal static class MetadataSearchHelper
{
    public static string NormalizeTitle(string title)
    {
        var normalized = title.Trim();

        foreach (var suffix in new[]
                 {
                     "™", "®", "©",
                     " - Steam", " (Steam)",
                     " - PC", " (PC)",
                     " - Windows", " (Windows)",
                     " Game Preview", " - Game Preview"
                 })
            normalized = normalized.Replace(suffix, string.Empty, StringComparison.OrdinalIgnoreCase);

        return normalized.Trim();
    }
}
