namespace OpenGameHUB.Services;

internal static class MetadataSearchHelper
{
    public static string NormalizeTitle(string title)
    {
        var normalized = title.Trim();

        foreach (var suffix in new[] { "™", "®", "©", " - Steam", " (Steam)" })
            normalized = normalized.Replace(suffix, string.Empty, StringComparison.OrdinalIgnoreCase);

        return normalized.Trim();
    }
}
