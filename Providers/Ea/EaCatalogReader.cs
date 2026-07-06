using System.Text.Json;
using System.Text.RegularExpressions;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Providers.Ea;

internal static class EaCatalogReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex GameSlugPattern = new(
        @"^[a-z0-9]+(-[a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex InternalCatalogIdPattern = new(
        @"^(Origin|Sims|OBS|SIMS)\.[A-Za-z0-9_.-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static EaLibraryCacheStatus? _cachedStatus;

    public static bool IsEaAppInstalled() => FindEaDesktopExecutable() is not null;

    public static string? FindEaDesktopExecutable()
    {
        var candidates = new[]
        {
            @"C:\Program Files\Electronic Arts\EA Desktop\EA Desktop\EADesktop.exe",
            @"C:\Program Files\Electronic Arts\EA Desktop\EADesktop.exe",
            @"C:\Program Files (x86)\Origin\Origin.exe"
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Origin");
            var clientPath = key?.GetValue("ClientPath") as string;
            if (!string.IsNullOrWhiteSpace(clientPath) && File.Exists(clientPath))
                return clientPath;
        }
        catch
        {
            // optional
        }

        return null;
    }

    public static string? GetInstallInfoPath()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "EA Desktop",
            "530c11479fe252fc5aabc24935b9776d4900eb3ba58fdc271e0d6229413ad40e",
            "IS");

        return File.Exists(path) ? path : null;
    }

    public static EaLibraryCacheStatus GetCacheStatus()
    {
        if (!IsEaAppInstalled())
            return EaLibraryCacheStatus.NotInstalled;

        _cachedStatus ??= EvaluateCacheStatus();
        return _cachedStatus.Value;
    }

    public static void InvalidateCache() => _cachedStatus = null;

    public static bool IsCloudLibraryAvailable()
    {
        var status = GetCacheStatus();
        return status is EaLibraryCacheStatus.Available or EaLibraryCacheStatus.DecryptFailedUsingLogs;
    }

    public static bool ShouldOfferLibraryPrompt()
    {
        var status = GetCacheStatus();
        return status is EaLibraryCacheStatus.Unavailable or EaLibraryCacheStatus.NoCache;
    }

    public static int GetLogLibraryEntryCount() => EaLogCatalogReader.ReadLibraryEntries().Count;

    public static IReadOnlyList<EaCatalogEntry> ReadLibraryEntries()
    {
        var status = GetCacheStatus();

        return status switch
        {
            EaLibraryCacheStatus.Available => MergeCatalogEntries(
                ReadEntriesFromDecryptedCache(),
                EaLogCatalogReader.ReadLibraryEntries()),
            EaLibraryCacheStatus.DecryptFailedUsingLogs => EaLogCatalogReader.ReadLibraryEntries(),
            _ => throw new InvalidDataException(status switch
            {
                EaLibraryCacheStatus.NotInstalled => Loc.T("EaAppNotInstalled"),
                EaLibraryCacheStatus.NoCache => Loc.T("EaCacheMissing"),
                EaLibraryCacheStatus.Unavailable => Loc.T("EaCacheDecryptFailed"),
                _ => Loc.T("EaCacheDecryptFailed")
            })
        };
    }

    private static IReadOnlyList<EaCatalogEntry> MergeCatalogEntries(
        IReadOnlyList<EaCatalogEntry> primary,
        IReadOnlyList<EaCatalogEntry> supplemental)
    {
        if (supplemental.Count == 0)
            return primary;

        var merged = new Dictionary<string, EaCatalogEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in primary)
            merged[entry.BaseSlug] = entry;

        foreach (var entry in supplemental)
        {
            if (merged.TryGetValue(entry.BaseSlug, out var existing))
                merged[entry.BaseSlug] = PreferCatalogEntry(existing, entry);
            else
                merged[entry.BaseSlug] = entry;
        }

        return merged.Values.ToList();
    }

    internal static EaCatalogEntry PreferCatalogEntry(EaCatalogEntry left, EaCatalogEntry right) =>
        CompareCatalogEntryPriority(left) >= CompareCatalogEntryPriority(right) ? left : right;

    private static int CompareCatalogEntryPriority(EaCatalogEntry entry)
    {
        if (entry.SoftwareId.StartsWith("Origin.SFT.", StringComparison.OrdinalIgnoreCase))
            return 3;

        if (entry.SoftwareId.StartsWith("SIMS", StringComparison.OrdinalIgnoreCase))
            return 2;

        if (Guid.TryParse(entry.SoftwareId, out _))
            return 0;

        return 1;
    }

    private static EaLibraryCacheStatus EvaluateCacheStatus()
    {
        var installInfoPath = GetInstallInfoPath();
        if (TryReadEntriesFromDecryptedCache(installInfoPath, out var decryptedEntries) && decryptedEntries.Count > 0)
            return EaLibraryCacheStatus.Available;

        var logEntries = EaLogCatalogReader.ReadLibraryEntries();
        if (logEntries.Count > 0)
            return EaLibraryCacheStatus.DecryptFailedUsingLogs;

        if (string.IsNullOrWhiteSpace(installInfoPath))
            return EaLibraryCacheStatus.NoCache;

        return EaLibraryCacheStatus.Unavailable;
    }

    private static IReadOnlyList<EaCatalogEntry> ReadEntriesFromDecryptedCache()
    {
        if (!TryReadEntriesFromDecryptedCache(GetInstallInfoPath(), out var entries))
            throw new InvalidDataException(Loc.T("EaCacheDecryptFailed"));

        return entries;
    }

    private static bool TryReadEntriesFromDecryptedCache(string? installInfoPath, out List<EaCatalogEntry> entries)
    {
        entries = [];

        var plaintext = installInfoPath is null ? null : EaInstallInfoDecryptor.TryDecrypt(installInfoPath);
        if (plaintext is null)
            return false;

        var root = JsonSerializer.Deserialize<InstallInfoRoot>(plaintext, JsonOptions);
        if (root?.InstallInfos is null || root.InstallInfos.Count == 0)
            return false;

        var dlcSoftwareIds = CollectDlcSoftwareIds(root.InstallInfos);

        foreach (var info in root.InstallInfos)
        {
            if (string.IsNullOrWhiteSpace(info.SoftwareId) || !IsValidGameSlug(info.BaseSlug))
                continue;

            if (dlcSoftwareIds.Contains(info.SoftwareId))
                continue;

            if (IsLikelyDlcOrAddon(info.BaseSlug!))
                continue;

            var installStatus = info.DetailedState?.InstallStatus ?? -1;
            if (installStatus != 0)
                continue;

            if (!string.IsNullOrWhiteSpace(info.BaseInstallPath) && Directory.Exists(info.BaseInstallPath))
                continue;

            entries.Add(new EaCatalogEntry(
                info.SoftwareId,
                info.BaseSlug!,
                SlugToTitle(info.BaseSlug!)));
        }

        return entries.Count > 0;
    }

    private static HashSet<string> CollectDlcSoftwareIds(IEnumerable<InstallInfoEntry> installInfos)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var info in installInfos)
        {
            if (info.Dlcs is null)
                continue;

            foreach (var dlc in info.Dlcs)
            {
                if (!string.IsNullOrWhiteSpace(dlc.SoftwareId))
                    ids.Add(dlc.SoftwareId);
            }
        }

        return ids;
    }

    internal static bool IsValidGameSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return false;

        if (Guid.TryParse(slug, out _))
            return false;

        if (InternalCatalogIdPattern.IsMatch(slug))
            return false;

        return GameSlugPattern.IsMatch(slug);
    }

    internal static bool IsLikelyDlcOrAddon(string slug)
    {
        return slug.Contains("-dlc", StringComparison.OrdinalIgnoreCase)
               || slug.Contains("-addon", StringComparison.OrdinalIgnoreCase)
               || slug.Contains("-expansion", StringComparison.OrdinalIgnoreCase)
               || slug.Contains("-season-pass", StringComparison.OrdinalIgnoreCase)
               || slug.Contains("-upgrade", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly HashSet<string> UppercaseSlugTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "ea", "fc", "f1", "f2", "f3", "f4", "nba", "nfl", "ufc", "vr", "gt", "dlc", "iv", "sims", "sports"
    };

    internal static string SlugToTitle(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return "EA Game";

        var words = slug
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(FormatSlugWord);

        return string.Join(' ', words);
    }

    private static string FormatSlugWord(string word)
    {
        if (word.Length <= 3 && word.All(char.IsDigit))
            return word;

        if (UppercaseSlugTokens.Contains(word))
            return word.ToUpperInvariant();

        if (word.Equals("ii", StringComparison.OrdinalIgnoreCase)
            || word.Equals("iii", StringComparison.OrdinalIgnoreCase)
            || word.Equals("iv", StringComparison.OrdinalIgnoreCase))
        {
            return word.ToUpperInvariant();
        }

        return char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();
    }

    internal static bool MatchesInstalledGame(UnifiedGame installed, EaCatalogEntry entry)
    {
        if (!installed.IsInstalled)
            return false;

        if (!string.IsNullOrWhiteSpace(installed.InstallPath) && !Directory.Exists(installed.InstallPath))
            return false;

        if (string.Equals(installed.PlatformGameId, entry.SoftwareId, StringComparison.OrdinalIgnoreCase))
            return true;

        var installedTitle = MetadataSearchHelper.NormalizeTitle(installed.Title).ToLowerInvariant();
        var entryTitle = MetadataSearchHelper.NormalizeTitle(entry.Title).ToLowerInvariant();
        if (string.Equals(installedTitle, entryTitle, StringComparison.Ordinal))
            return true;

        return TitlesMatchByCompactForm(installedTitle, entryTitle);
    }

    private static bool TitlesMatchByCompactForm(string left, string right)
    {
        var compactLeft = new string(left.Where(char.IsLetterOrDigit).ToArray());
        var compactRight = new string(right.Where(char.IsLetterOrDigit).ToArray());
        return compactLeft.Length > 0
               && compactRight.Length > 0
               && string.Equals(compactLeft, compactRight, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class InstallInfoRoot
    {
        public List<InstallInfoEntry>? InstallInfos { get; set; }
    }

    private sealed class InstallInfoEntry
    {
        public string? SoftwareId { get; set; }
        public string? BaseSlug { get; set; }
        public string? BaseInstallPath { get; set; }
        public DetailedStateInfo? DetailedState { get; set; }
        public List<DlcInfo>? Dlcs { get; set; }
    }

    private sealed class DetailedStateInfo
    {
        public int InstallStatus { get; set; }
    }

    private sealed class DlcInfo
    {
        public string? SoftwareId { get; set; }
    }
}

internal sealed record EaCatalogEntry(string SoftwareId, string BaseSlug, string Title);
