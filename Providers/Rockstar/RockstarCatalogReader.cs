using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Providers.Rockstar;

internal sealed record RockstarCatalogEntry(string TitleId, string Title, string? InstallPath = null);

internal sealed record RockstarTitleCacheEntry(
    string TitleId,
    string? DisplayName,
    string? InstallPath,
    bool IsOwned);

internal static class RockstarCatalogReader
{
    private static readonly byte[] ZeroKey = new byte[32];
    private static readonly byte[] ZeroIv = new byte[16];

    private static readonly string TitlesDatPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Rockstar Games",
        "Launcher",
        "titles.dat");

    private static readonly string RecognisedTitlesDatPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Rockstar Games",
        "Launcher",
        "recognised_titles.dat");

    private static readonly HashSet<string> ExcludedTitleIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "launcher",
        "rdr2_sp_steam",
        "rdr2_sp_rgl",
        "rdr2_sp",
        "rdr2_rdo",
        "rdr2_sp_epic",
        "gtatrilogy",
        "rhf43f"
    };

    private static readonly RockstarKnownTitle[] KnownTitles =
    [
        new("gta5", "Grand Theft Auto V", "Grand Theft Auto V", "Grand Theft Auto V Legacy"),
        new("gta5_gen9", "Grand Theft Auto V Enhanced", "Grand Theft Auto V Enhanced"),
        new("rdr2", "Red Dead Redemption 2", "Red Dead Redemption 2"),
        new("rdr", "Red Dead Redemption", "Red Dead Redemption"),
        new("gta4", "Grand Theft Auto IV", "Grand Theft Auto IV"),
        new("gta3", "Grand Theft Auto III", "Grand Theft Auto III"),
        new("gtavc", "Grand Theft Auto: Vice City", "Grand Theft Auto: Vice City"),
        new("gtasa", "Grand Theft Auto: San Andreas", "Grand Theft Auto: San Andreas"),
        new("bully", "Bully: Scholarship Edition", "Bully: Scholarship Edition"),
        new("lanoire", "L.A. Noire", "L.A. Noire: Complete Edition", "L.A. Noire"),
        new("mp3", "Max Payne 3", "Max Payne 3"),
        new("lanoirevr", "L.A. Noire: The VR Case Files", "L.A. Noire: The VR Case Files"),
        new("gta3unreal", "Grand Theft Auto III - The Definitive Edition", "GTA III - Definitive Edition"),
        new("gtavcunreal", "Grand Theft Auto: Vice City - The Definitive Edition", "GTA Vice City - Definitive Edition"),
        new("gtasaunreal", "Grand Theft Auto: San Andreas - The Definitive Edition", "GTA San Andreas - Definitive Edition")
    ];

    public static bool IsLauncherInstalled() => FindLauncherExecutable() is not null;

    public static bool IsCloudLibraryAvailable() =>
        IsLauncherInstalled()
        && (File.Exists(TitlesDatPath) || File.Exists(RecognisedTitlesDatPath) || FindLauncherLogFile() is not null);

    public static string? FindLauncherExecutable()
    {
        foreach (var path in BuildLauncherExecutableCandidates())
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    public static IReadOnlyList<RockstarCatalogEntry> ReadLibraryEntries()
    {
        if (!IsLauncherInstalled())
            return [];

        var entries = new Dictionary<string, RockstarCatalogEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var cacheEntry in ReadOwnedTitlesFromCache())
            AddOrUpdateEntry(entries, cacheEntry.TitleId, cacheEntry.InstallPath, cacheEntry.DisplayName);

        foreach (var titleId in ReadOwnedTitleIdsFromLogs())
            AddOrUpdateEntry(entries, titleId, installPath: null, displayName: null);

        return entries.Values
            .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsTitleInstalled(string titleId, string? cachedInstallPath = null)
    {
        if (!string.IsNullOrWhiteSpace(cachedInstallPath) && Directory.Exists(cachedInstallPath))
            return true;

        if (TryReadRegistryInstallPath(titleId, out var registryPath))
            return true;

        return false;
    }

    public static bool MatchesInstalledGame(UnifiedGame game, RockstarCatalogEntry entry)
    {
        if (game.Platform != Platform.Rockstar)
            return false;

        if (!string.IsNullOrWhiteSpace(game.PlatformGameId)
            && string.Equals(game.PlatformGameId, entry.TitleId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var known = FindKnownTitle(entry.TitleId);
        if (known is not null && !string.IsNullOrWhiteSpace(game.Title))
        {
            foreach (var alias in known.RegistryAliases)
            {
                if (string.Equals(game.Title, alias, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        var gameTitle = MetadataSearchHelper.NormalizeTitle(game.Title).ToLowerInvariant();
        var entryTitle = MetadataSearchHelper.NormalizeTitle(entry.Title).ToLowerInvariant();
        return gameTitle == entryTitle;
    }

    public static string BuildInstallArguments(string titleId) =>
        $"-enableFullMode -install={titleId}";

    public static string BuildLaunchArguments(string installFolder) =>
        $"-launchTitleInFolder \"{installFolder}\"";

    public static string? TryResolveTitleId(string? platformGameId, string? title)
    {
        if (!string.IsNullOrWhiteSpace(platformGameId))
        {
            if (FindKnownTitle(platformGameId) is not null)
                return platformGameId;

            var byPlatform = KnownTitles.FirstOrDefault(entry =>
                entry.RegistryAliases.Any(alias =>
                    string.Equals(alias, platformGameId, StringComparison.OrdinalIgnoreCase)));

            if (byPlatform is not null)
                return byPlatform.TitleId;
        }

        if (string.IsNullOrWhiteSpace(title))
            return null;

        var normalized = MetadataSearchHelper.NormalizeTitle(title);
        var byTitle = KnownTitles.FirstOrDefault(entry =>
            string.Equals(entry.Title, normalized, StringComparison.OrdinalIgnoreCase)
            || entry.RegistryAliases.Any(alias =>
                string.Equals(alias, normalized, StringComparison.OrdinalIgnoreCase)));

        return byTitle?.TitleId;
    }

    public static void EnrichCatalogCoverUrls(IReadOnlyList<UnifiedGame> games)
    {
        foreach (var game in games)
        {
            if (game.Platform != Platform.Rockstar || !string.IsNullOrWhiteSpace(game.CoverPath))
                continue;

            if (!string.IsNullOrWhiteSpace(game.CatalogCoverUrl))
                continue;

            var coverUrl = RockstarCoverUrls.GetCoverUrl(game.PlatformGameId, game.Title);
            if (!string.IsNullOrWhiteSpace(coverUrl))
                game.CatalogCoverUrl = coverUrl;
        }
    }

    private static void AddOrUpdateEntry(
        IDictionary<string, RockstarCatalogEntry> entries,
        string titleId,
        string? installPath,
        string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(titleId) || ExcludedTitleIds.Contains(titleId))
            return;

        var title = ResolveTitle(titleId, displayName);
        if (entries.TryGetValue(titleId, out var existing))
        {
            if (string.IsNullOrWhiteSpace(existing.InstallPath) && !string.IsNullOrWhiteSpace(installPath))
                entries[titleId] = existing with { InstallPath = installPath };
            return;
        }

        entries[titleId] = new RockstarCatalogEntry(titleId, title, installPath);
    }

    private static string ResolveTitle(string titleId, string? displayName = null)
    {
        if (!string.IsNullOrWhiteSpace(displayName)
            && !string.Equals(displayName, titleId, StringComparison.OrdinalIgnoreCase))
        {
            return displayName;
        }

        var known = FindKnownTitle(titleId);
        return known?.Title ?? FormatTitleId(titleId);
    }

    private static RockstarKnownTitle? FindKnownTitle(string titleId) =>
        KnownTitles.FirstOrDefault(entry => string.Equals(entry.TitleId, titleId, StringComparison.OrdinalIgnoreCase));

    private static string FormatTitleId(string titleId) =>
        string.Join(' ',
            titleId.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    private static IReadOnlyList<RockstarTitleCacheEntry> ReadOwnedTitlesFromCache()
    {
        var owned = new Dictionary<string, RockstarTitleCacheEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in ReadEncryptedTitleFile(TitlesDatPath))
        {
            if (entry.IsOwned)
                owned[entry.TitleId] = entry;
        }

        foreach (var entry in ReadEncryptedTitleFile(RecognisedTitlesDatPath))
        {
            if (entry.IsOwned)
                owned[entry.TitleId] = entry;
        }

        return owned.Values.ToList();
    }

    private static IEnumerable<RockstarTitleCacheEntry> ReadEncryptedTitleFile(string path)
    {
        if (!File.Exists(path))
            return [];

        try
        {
            var json = DecryptRockstarDat(path);
            if (string.IsNullOrWhiteSpace(json))
                return [];

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("tl", out var titles)
                && titles.ValueKind == JsonValueKind.Array)
            {
                return ParseTitleArray(titles);
            }

            if (document.RootElement.TryGetProperty("titles", out var recognisedTitles))
            {
                return ParseRecognisedTitles(recognisedTitles);
            }

            return [];
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(RockstarCatalogReader),
                operation: "ReadEncryptedTitleFile",
                exception: ex,
                platform: Platform.Rockstar,
                details: Path.GetFileName(path));
            return [];
        }
    }

    private static List<RockstarTitleCacheEntry> ParseTitleArray(JsonElement titles)
    {
        var results = new List<RockstarTitleCacheEntry>();
        foreach (var title in titles.EnumerateArray())
        {
            var parsed = ParseTitleElement(title);
            if (parsed is not null)
                results.Add(parsed);
        }

        return results;
    }

    private static IEnumerable<RockstarTitleCacheEntry> ParseRecognisedTitles(JsonElement recognisedTitles)
    {
        if (recognisedTitles.ValueKind == JsonValueKind.Array)
            return ParseTitleArray(recognisedTitles);

        if (recognisedTitles.ValueKind != JsonValueKind.Object)
            return [];

        var results = new List<RockstarTitleCacheEntry>();
        foreach (var property in recognisedTitles.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                var parsed = ParseTitleElement(property.Value, property.Name);
                if (parsed is not null)
                    results.Add(parsed);
            }
        }

        return results;
    }

    private static RockstarTitleCacheEntry? ParseTitleElement(JsonElement title, string? fallbackTitleId = null)
    {
        var titleId = title.TryGetProperty("ti", out var titleIdElement)
            ? titleIdElement.GetString()
            : fallbackTitleId;

        if (string.IsNullOrWhiteSpace(titleId))
            return null;

        string? displayName = null;
        if (title.TryGetProperty("tn", out var titleNameElement))
            displayName = titleNameElement.GetString();

        string? installPath = null;
        if (title.TryGetProperty("il", out var installPathElement))
            installPath = installPathElement.GetString();

        var isOwned = title.TryGetProperty("own", out var ownerElement)
                      && ownerElement.ValueKind == JsonValueKind.String
                      && !string.IsNullOrWhiteSpace(ownerElement.GetString());

        return new RockstarTitleCacheEntry(titleId, displayName, installPath, isOwned);
    }

    private static string? DecryptRockstarDat(string path)
    {
        var encrypted = File.ReadAllBytes(path);
        if (encrypted.Length == 0)
            return null;

        using var aes = Aes.Create();
        aes.Key = ZeroKey;
        aes.IV = ZeroIv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
        if (decrypted.Length <= 16)
            return null;

        return Encoding.UTF8.GetString(decrypted, 16, decrypted.Length - 16);
    }

    private static IEnumerable<string> ReadOwnedTitleIdsFromLogs()
    {
        var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var checkedGames = 0;
        var totalGames = KnownTitles.Length;

        for (var logIndex = 0; logIndex < 10; logIndex++)
        {
            var logPath = FindLauncherLogFile(logIndex);
            if (logPath is null || !File.Exists(logPath))
                break;

            try
            {
                checkedGames = ParseLogFileBackwards(logPath, owned, checkedGames, totalGames);
                if (checkedGames >= totalGames)
                    break;
            }
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(RockstarCatalogReader),
                    operation: "ReadOwnedTitleIdsFromLogs.ParseLogFile",
                    exception: ex,
                    platform: Platform.Rockstar,
                    details: Path.GetFileName(logPath));
                break;
            }
        }

        return owned;
    }

    private static int ParseLogFileBackwards(
        string logPath,
        ISet<string> ownedTitleIds,
        int checkedGames,
        int totalGames)
    {
        using var stream = new FileStream(
            logPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
            lines.Add(line);

        for (var index = lines.Count - 1; index >= 0; index--)
        {
            if (checkedGames >= totalGames)
                break;

            var line = lines[index];
            if (line.Contains("launcher", StringComparison.OrdinalIgnoreCase))
                continue;

            if (line.Contains("on branch ", StringComparison.Ordinal))
            {
                if (!TryExtractLogTitleId(line, out var titleId))
                    continue;

                if (!ExcludedTitleIds.Contains(titleId))
                    ownedTitleIds.Add(titleId);

                checkedGames++;
                continue;
            }

            if (line.Contains("no branches!", StringComparison.Ordinal))
            {
                if (!TryExtractLogTitleId(line, out var titleId))
                    continue;

                if (!ExcludedTitleIds.Contains(titleId))
                    ownedTitleIds.Remove(titleId);

                checkedGames++;
            }
        }

        return checkedGames;
    }

    private static bool TryExtractLogTitleId(string line, out string titleId)
    {
        titleId = string.Empty;
        if (line.Length <= 65)
            return false;

        var suffix = line[65..];
        var colonIndex = suffix.IndexOf(':');
        if (colonIndex < 0)
            return false;

        titleId = suffix[..colonIndex].Trim();
        return titleId.Length > 0;
    }

    private static string? FindLauncherLogFile(int index = 0)
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var suffix = index == 0 ? string.Empty : $".0{index}";
        var path = Path.Combine(documents, "Rockstar Games", "Launcher", $"launcher{suffix}.log");
        return File.Exists(path) ? path : null;
    }

    private static bool TryReadRegistryInstallPath(string titleId, out string installPath)
    {
        installPath = string.Empty;
        var known = FindKnownTitle(titleId);

        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Rockstar Games");
            if (root is null)
                return false;

            foreach (var subKeyName in root.GetSubKeyNames())
            {
                if (string.Equals(subKeyName, "Launcher", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(subKeyName, "Rockstar Games Social Club", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var matchesKnown = known is not null
                                   && known.RegistryAliases.Any(alias =>
                                       string.Equals(subKeyName, alias, StringComparison.OrdinalIgnoreCase));
                var matchesTitleId = string.Equals(subKeyName, titleId, StringComparison.OrdinalIgnoreCase);

                if (!matchesKnown && !matchesTitleId)
                    continue;

                using var subKey = root.OpenSubKey(subKeyName);
                var folder = subKey?.GetValue("InstallFolder") as string;
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                    continue;

                installPath = folder;
                return true;
            }
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(RockstarCatalogReader),
                operation: "IsTitleInstalled.ReadRegistry",
                exception: ex,
                platform: Platform.Rockstar,
                details: titleId);
        }

        return false;
    }

    private static IEnumerable<string> BuildLauncherExecutableCandidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in ReadRegistryLauncherPaths())
        {
            if (seen.Add(path))
                yield return path;
        }

        foreach (var path in DefaultLauncherPaths())
        {
            if (seen.Add(path))
                yield return path;
        }
    }

    private static IEnumerable<string> ReadRegistryLauncherPaths()
    {
        var paths = new List<string>();
        string? installFolder = null;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Rockstar Games\Launcher");
            installFolder = key?.GetValue("InstallFolder") as string;
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(RockstarCatalogReader),
                operation: "ReadRegistryLauncherPaths.ReadInstallFolder",
                exception: ex,
                platform: Platform.Rockstar);
        }

        if (!string.IsNullOrWhiteSpace(installFolder))
            paths.Add(Path.Combine(installFolder.Trim('"'), "Launcher.exe"));

        foreach (var root in new[] { Registry.ClassesRoot, Registry.LocalMachine })
        {
            try
            {
                using var key = root.OpenSubKey(@"rockstar\shell\open\command");
                var command = key?.GetValue(null) as string;
                var executable = ExtractExecutablePath(command);
                if (!string.IsNullOrWhiteSpace(executable))
                    paths.Add(executable);
            }
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(RockstarCatalogReader),
                    operation: "ReadRegistryLauncherPaths.ReadCommand",
                    exception: ex,
                    platform: Platform.Rockstar,
                    details: root.Name);
            }
        }

        return paths;
    }

    private static IEnumerable<string> DefaultLauncherPaths()
    {
        yield return @"C:\Program Files\Rockstar Games\Launcher\Launcher.exe";
        yield return @"C:\Program Files (x86)\Rockstar Games\Launcher\Launcher.exe";
    }

    private static string? ExtractExecutablePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        command = command.Trim();
        if (command.StartsWith('"'))
        {
            var endQuote = command.IndexOf('"', 1);
            if (endQuote > 1)
                return command[1..endQuote];
        }

        var firstSpace = command.IndexOf(' ');
        return firstSpace > 0 ? command[..firstSpace].Trim('"') : command.Trim('"');
    }

    private sealed record RockstarKnownTitle(string TitleId, string Title, params string[] RegistryAliases);
}
