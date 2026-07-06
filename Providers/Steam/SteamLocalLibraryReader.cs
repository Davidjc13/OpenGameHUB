using System.Text.RegularExpressions;

namespace OpenGameHUB.Providers.Steam;

public static class SteamLocalLibraryReader
{
    private static readonly Regex AppsSectionRegex =
        new(@"""apps""\s*\{", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AppIdLineRegex =
        new(@"^\s*""(\d{1,7})""\s*\{", RegexOptions.Compiled);

    private static readonly HashSet<int> IgnoredAppIds = [0, 7, 469, 753, 760, 767];

    public sealed record LocalAppEntry(int AppId, int PlaytimeMinutes, DateTime? LastPlayed);

    public static IReadOnlyList<LocalAppEntry> ReadOwnedApps()
    {
        var account = SteamLocalAccountReader.DetectActiveAccount();
        var installPath = SteamLocalAccountReader.FindSteamInstallPath();
        if (account is null || installPath is null)
            return [];

        var accountId = ToAccountId(account.SteamId64);
        var userDataPath = Path.Combine(installPath, "userdata", accountId.ToString());
        if (!Directory.Exists(userDataPath))
            return [];

        var apps = new Dictionary<int, LocalAppEntry>();

        var localConfigPath = Path.Combine(userDataPath, "config", "localconfig.vdf");
        if (File.Exists(localConfigPath))
        {
            foreach (var entry in ParseLocalConfigApps(localConfigPath))
                apps[entry.AppId] = entry;
        }

        var libraryCachePath = Path.Combine(userDataPath, "config", "librarycache");
        if (Directory.Exists(libraryCachePath))
        {
            foreach (var file in Directory.EnumerateFiles(libraryCachePath, "*.json"))
            {
                if (!int.TryParse(Path.GetFileNameWithoutExtension(file), out var appId)
                    || IgnoredAppIds.Contains(appId))
                {
                    continue;
                }

                apps.TryAdd(appId, new LocalAppEntry(appId, 0, null));
            }
        }

        return apps.Values
            .Where(entry => !IgnoredAppIds.Contains(entry.AppId))
            .OrderBy(entry => entry.AppId)
            .ToList();
    }

    public static long ToAccountId(string steamId64) =>
        long.Parse(steamId64) & 0xFFFFFFFFL;

    private static IEnumerable<LocalAppEntry> ParseLocalConfigApps(string path)
    {
        var text = File.ReadAllText(path);
        var match = AppsSectionRegex.Match(text);
        if (!match.Success)
            yield break;

        var section = ExtractBalancedBlock(text, match.Index + match.Length - 1);
        if (section is null)
            yield break;

        string? currentAppId = null;
        var playtime = 0;
        long lastPlayedUnix = 0;

        foreach (var rawLine in section.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var appMatch = AppIdLineRegex.Match(line);
            if (appMatch.Success)
            {
                if (currentAppId is not null
                    && int.TryParse(currentAppId, out var finishedAppId)
                    && !IgnoredAppIds.Contains(finishedAppId))
                {
                    yield return CreateEntry(finishedAppId, playtime, lastPlayedUnix);
                }

                currentAppId = appMatch.Groups[1].Value;
                playtime = 0;
                lastPlayedUnix = 0;
                continue;
            }

            if (currentAppId is null)
                continue;

            if (line.StartsWith("\"Playtime\"", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(ReadVdfValue(line), out var minutes))
                    playtime = minutes;
            }
            else if (line.StartsWith("\"LastPlayed\"", StringComparison.OrdinalIgnoreCase))
            {
                if (long.TryParse(ReadVdfValue(line), out var unix))
                    lastPlayedUnix = unix;
            }
            else if (line == "}" && currentAppId is not null)
            {
                if (int.TryParse(currentAppId, out var appId) && !IgnoredAppIds.Contains(appId))
                    yield return CreateEntry(appId, playtime, lastPlayedUnix);

                currentAppId = null;
                playtime = 0;
                lastPlayedUnix = 0;
            }
        }
    }

    private static LocalAppEntry CreateEntry(int appId, int playtimeMinutes, long lastPlayedUnix) =>
        new(
            appId,
            playtimeMinutes,
            lastPlayedUnix > 0
                ? DateTimeOffset.FromUnixTimeSeconds(lastPlayedUnix).UtcDateTime
                : null);

    private static string? ExtractBalancedBlock(string text, int openBraceIndex)
    {
        if (openBraceIndex < 0 || openBraceIndex >= text.Length || text[openBraceIndex] != '{')
            return null;

        var depth = 0;
        for (var i = openBraceIndex; i < text.Length; i++)
        {
            if (text[i] == '{')
                depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return text[(openBraceIndex + 1)..i];
            }
        }

        return null;
    }

    private static string? ReadVdfValue(string line)
    {
        var parts = line.Split('"', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2 ? parts[^1] : null;
    }
}
