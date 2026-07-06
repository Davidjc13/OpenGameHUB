using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenGameHUB.Providers.Ea;

internal static class EaLogCatalogReader
{
    private static readonly Regex InstallInfoLine = new(
        @"\[(?<ts>\d{4}-\d{2}-\d{2}T[^\]]+)\].*?IS update: set installInfo for softwareId=\[(?<id>[^\]]+)\] baseSlug=\[(?<slug>[^\]]*)\] installedStatus=\[(?<status>[^\]]+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<EaCatalogEntry> ReadLibraryEntries()
    {
        var latestBySlug = new Dictionary<string, InstallInfoSnapshot>(StringComparer.OrdinalIgnoreCase);

        foreach (var logPath in FindLogPaths())
        {
            string content;
            try
            {
                content = ReadSharedLogContent(logPath);
            }
            catch
            {
                continue;
            }

            foreach (Match match in InstallInfoLine.Matches(content))
            {
                var softwareId = match.Groups["id"].Value.Trim();
                var slug = match.Groups["slug"].Value.Trim();
                var status = match.Groups["status"].Value.Trim();

                if (string.IsNullOrWhiteSpace(softwareId) || !EaCatalogReader.IsValidGameSlug(slug))
                    continue;

                if (!DateTimeOffset.TryParse(
                        match.Groups["ts"].Value.Trim(),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var timestamp))
                {
                    continue;
                }

                if (latestBySlug.TryGetValue(slug, out var existing) && existing.Timestamp > timestamp)
                    continue;

                latestBySlug[slug] = new InstallInfoSnapshot(softwareId, slug, status, timestamp);
            }
        }

        var entries = new List<EaCatalogEntry>();

        foreach (var snapshot in latestBySlug.Values)
        {
            if (!string.Equals(snapshot.Status, "NotInstalled", StringComparison.OrdinalIgnoreCase))
                continue;

            if (EaCatalogReader.IsLikelyDlcOrAddon(snapshot.BaseSlug))
                continue;

            entries.Add(new EaCatalogEntry(
                snapshot.SoftwareId,
                snapshot.BaseSlug,
                EaCatalogReader.SlugToTitle(snapshot.BaseSlug)));
        }

        return DeduplicateByPreferredSoftwareId(entries);
    }

    private static IReadOnlyList<EaCatalogEntry> DeduplicateByPreferredSoftwareId(IEnumerable<EaCatalogEntry> entries)
    {
        return entries
            .GroupBy(entry => entry.BaseSlug, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Aggregate(EaCatalogReader.PreferCatalogEntry))
            .ToList();
    }

    private static IEnumerable<string> FindLogPaths()
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Electronic Arts", "EA Desktop", "Logs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EADesktop", "Logs")
        };

        var names = new[] { "EADesktop.log", "EADesktop.bak", "EADesktopVerbose.log", "EADesktopVerbose.bak" };
        var paths = new List<string>();

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (var name in names)
            {
                var path = Path.Combine(root, name);
                if (File.Exists(path))
                    paths.Add(path);
            }
        }

        return paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
            .ToList();
    }

    private static string ReadSharedLogContent(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed record InstallInfoSnapshot(
        string SoftwareId,
        string BaseSlug,
        string Status,
        DateTimeOffset Timestamp);
}
