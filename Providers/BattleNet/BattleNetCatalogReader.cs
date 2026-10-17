using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;
using ProtoBuf;

namespace OpenGameHUB.Providers.BattleNet;

internal static class BattleNetCatalogReader
{
    private static readonly string DefaultProductDbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Battle.net",
        "Agent",
        "product.db");

    internal static string? ProductDbPathOverrideForTests { get; set; }

    private static string ProductDbPath => ProductDbPathOverrideForTests ?? DefaultProductDbPath;

    private static readonly HashSet<string> ExcludedUids = new(StringComparer.OrdinalIgnoreCase)
    {
        "agent",
        "bna",
        "battle.net"
    };

    private static readonly Regex TestSuffixRegex = new(
        @"(test|beta)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static BattleNetGamesCatalog? _catalog;

    public static bool IsLauncherInstalled() => FindLauncherExecutable() is not null;

    public static bool IsCloudLibraryAvailable() =>
        IsLauncherInstalled()
        && (File.Exists(ProductDbPath) || BattleNetLibraryCache.Exists());

    public static string? FindLauncherExecutable()
    {
        foreach (var path in BuildLauncherExecutableCandidates())
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    public static IReadOnlyList<BattleNetCatalogEntry> ReadLibraryEntriesFromProductDb()
    {
        if (!File.Exists(ProductDbPath))
            return [];

        var installs = ReadProductInstalls();
        var results = new Dictionary<string, BattleNetCatalogEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var install in installs)
        {
            if (string.IsNullOrWhiteSpace(install.Uid) || ExcludedUids.Contains(install.Uid))
                continue;

            if (IsExcludedTestUid(install.Uid))
                continue;

            var catalogGame = ResolveCatalogGame(install.Uid, install.ProductCode);
            var state = install.CachedProductState?.BaseProductState;
            var installPath = NullIfEmpty(install.Settings?.InstallPath);
            var pathInstalled = !string.IsNullOrWhiteSpace(installPath) && Directory.Exists(installPath);
            var playable = state?.Playable ?? pathInstalled;
            var installed = (state?.Installed ?? false) || pathInstalled;
            var productId = catalogGame?.ProductId
                ?? NullIfEmpty(install.ProductCode)
                ?? install.Uid;
            var title = catalogGame?.Name
                ?? NullIfEmpty(install.ProductCode)
                ?? install.Uid;

            if (results.TryGetValue(productId, out var existing))
            {
                if (!existing.Installed && installed)
                    results[productId] = existing with { Installed = true, InstallPath = installPath };
                continue;
            }

            results[productId] = new BattleNetCatalogEntry(
                catalogGame?.InternalId ?? install.Uid,
                productId,
                title,
                Playable: playable || installed,
                Installed: installed,
                installPath);
        }

        AppDiagnostics.ReportInfo(
            nameof(BattleNetCatalogReader),
            "ReadLibraryEntriesFromProductDb",
            Platform.BattleNet,
            $"rawInstalls={installs.Count} mapped={results.Count} uids={string.Join(",", installs.Select(i => i.Uid))}");

        return results.Values
            .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<BattleNetAccountLibraryEntry> ReadAccountLibraryEntries()
    {
        if (!BattleNetLibraryCache.TryLoad(out var cache))
            return [];

        var catalog = LoadCatalog();
        var byApiId = catalog.Games
            .Where(game => !string.IsNullOrWhiteSpace(game.ApiTitleId))
            .GroupBy(game => game.ApiTitleId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var results = new Dictionary<string, BattleNetAccountLibraryEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var accountGame in cache.OwnedGames)
        {
            if (string.IsNullOrWhiteSpace(accountGame.TitleId)
                && string.IsNullOrWhiteSpace(accountGame.LocalizedName))
            {
                continue;
            }

            var catalogGame = (!string.IsNullOrWhiteSpace(accountGame.TitleId)
                    && byApiId.TryGetValue(accountGame.TitleId, out var byId)
                    ? byId
                    : null)
                ?? FindByTitle(accountGame.LocalizedName);

            var productId = catalogGame?.ProductId
                ?? NullIfEmpty(accountGame.TitleId)
                ?? accountGame.LocalizedName;
            var title = NullIfEmpty(accountGame.LocalizedName)
                ?? catalogGame?.Name
                ?? productId;

            results[productId] = new BattleNetAccountLibraryEntry(productId, title);
        }

        foreach (var classic in cache.ClassicGames)
        {
            AddClassicEntries(classic, results);
        }

        return results.Values
            .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<BattleNetCatalogEntry> ReadMergedCloudEntries()
    {
        var merged = new Dictionary<string, BattleNetCatalogEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in ReadLibraryEntriesFromProductDb())
            merged[entry.ProductId] = entry;

        foreach (var accountEntry in ReadAccountLibraryEntries())
        {
            if (merged.ContainsKey(accountEntry.ProductId))
                continue;

            var catalogGame = FindByProductId(accountEntry.ProductId)
                ?? FindByTitle(accountEntry.Title);
            merged[accountEntry.ProductId] = new BattleNetCatalogEntry(
                catalogGame?.InternalId ?? accountEntry.ProductId,
                catalogGame?.ProductId ?? accountEntry.ProductId,
                accountEntry.Title,
                Playable: true,
                Installed: false,
                InstallPath: null);
        }

        foreach (var catalogGame in LoadCatalog().Games.Where(game => game.AlwaysVisible))
        {
            if (merged.ContainsKey(catalogGame.ProductId))
                continue;

            merged[catalogGame.ProductId] = new BattleNetCatalogEntry(
                catalogGame.InternalId,
                catalogGame.ProductId,
                catalogGame.Name,
                Playable: true,
                Installed: false,
                InstallPath: null);
        }

        return merged.Values
            .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool MatchesInstalledGame(UnifiedGame game, BattleNetCatalogEntry entry)
    {
        if (game.Platform != Platform.BattleNet)
            return false;

        if (!string.IsNullOrWhiteSpace(game.PlatformGameId)
            && string.Equals(game.PlatformGameId, entry.ProductId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var gameTitle = MetadataSearchHelper.NormalizeTitle(game.Title).ToLowerInvariant();
        var entryTitle = MetadataSearchHelper.NormalizeTitle(entry.Title).ToLowerInvariant();
        return gameTitle == entryTitle;
    }

    public static string BuildLaunchArguments(string productId) =>
        $"--exec=\"launch {ResolveOfficialProductId(productId)}\"";

    public static string BuildInstallArguments(string productId) =>
        $"--game={ResolveInstallGameCode(productId)} --install";

    public static string ResolveOfficialProductId(string productId)
    {
        var catalog = FindByProductId(productId) ?? FindByUid(productId);
        return catalog?.ProductId ?? productId;
    }

    public static string ResolveInstallGameCode(string productId)
    {
        var catalog = FindByProductId(productId) ?? FindByUid(productId);
        return catalog?.InternalId ?? productId;
    }

    public static string BuildProductUri(string productId) =>
        ProtocolUri.TryBattleNetProduct(productId, out var url)
            ? url
            : throw new InvalidOperationException(Loc.T("NoLaunchMethod"));

    public static string BuildUriArguments(string productId) =>
        $"--uri={BuildProductUri(productId)}";

    public static string FormatLauncherArgs(string launcherExe, string arguments) =>
        $"{launcherExe}|{arguments}";

    public static BattleNetCatalogGame? FindByProductId(string productId) =>
        LoadCatalog().Games.FirstOrDefault(game =>
            string.Equals(game.ProductId, productId, StringComparison.OrdinalIgnoreCase));

    public static BattleNetCatalogGame? FindByUid(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
            return null;

        var catalog = LoadCatalog();
        var exact = catalog.Games.FirstOrDefault(game =>
            string.Equals(game.InternalId, uid, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        return catalog.Games
            .Where(game => uid.StartsWith(game.InternalId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(game => game.InternalId.Length)
            .FirstOrDefault();
    }

    private static void AddClassicEntries(
        BattleNetClassicGameCache classic,
        IDictionary<string, BattleNetAccountLibraryEntry> results)
    {
        var icon = classic.RegionalGameFranchiseIconFilename ?? string.Empty;

        if (icon.Contains("warcraft-iii", StringComparison.OrdinalIgnoreCase))
        {
            AddClassicProduct(results, "W3", "Warcraft III: Reforged");
            return;
        }

        if (icon.Contains("diablo-ii", StringComparison.OrdinalIgnoreCase))
        {
            AddClassicProduct(results, "D2", "Diablo II");
            AddClassicProduct(results, "D2X", "Diablo II: Lord of Destruction");
        }
    }

    private static void AddClassicProduct(
        IDictionary<string, BattleNetAccountLibraryEntry> results,
        string productId,
        string title)
    {
        if (results.ContainsKey(productId))
            return;

        results[productId] = new BattleNetAccountLibraryEntry(productId, title);
    }

    public static BattleNetCatalogGame? FindByTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var normalized = MetadataSearchHelper.NormalizeTitle(title);
        return LoadCatalog().Games.FirstOrDefault(game =>
            string.Equals(MetadataSearchHelper.NormalizeTitle(game.Name), normalized, StringComparison.OrdinalIgnoreCase)
            || game.Aliases.Any(alias =>
                string.Equals(MetadataSearchHelper.NormalizeTitle(alias), normalized, StringComparison.OrdinalIgnoreCase)));
    }

    private static BattleNetCatalogGame? ResolveCatalogGame(string uid, string productCode)
    {
        var byUid = FindByUid(uid);
        if (byUid is not null)
            return byUid;

        if (string.IsNullOrWhiteSpace(productCode))
            return null;

        var catalog = LoadCatalog();
        return catalog.Games.FirstOrDefault(game =>
            string.Equals(game.InternalId, productCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(game.ProductId, productCode, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsExcludedTestUid(string uid)
    {
        if (!TestSuffixRegex.IsMatch(uid))
            return false;

        return FindByUid(uid) is null;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static IReadOnlyList<BattleNetProductInstall> ReadProductInstalls()
    {
        try
        {
            var bytes = File.ReadAllBytes(ProductDbPath);
            var parsed = BattleNetProductDbParser.Parse(bytes);
            if (parsed.Count > 0)
                return parsed;
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(BattleNetCatalogReader),
                operation: "ReadProductInstalls.Manual",
                exception: ex,
                platform: Platform.BattleNet);
        }

        try
        {
            using var file = File.OpenRead(ProductDbPath);
            var database = Serializer.Deserialize<BattleNetProductDatabase>(file);
            if (database.ProductInstalls.Count > 0)
                return database.ProductInstalls;
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(BattleNetCatalogReader),
                operation: "ReadProductInstalls.Database",
                exception: ex,
                platform: Platform.BattleNet);
        }

        return [];
    }

    private static BattleNetGamesCatalog LoadCatalog()
    {
        if (_catalog is not null)
            return _catalog;

        var assembly = typeof(BattleNetCatalogReader).Assembly;
        const string resourceName = "OpenGameHUB.Providers.BattleNet.Resources.BattleNetGames.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {resourceName}.");

        _catalog = JsonSerializer.Deserialize<BattleNetGamesCatalog>(stream)
            ?? throw new InvalidOperationException("Failed to deserialize Battle.net catalog.");

        return _catalog;
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
        string? installLocation = null;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Battle.net");
            installLocation = key?.GetValue("InstallLocation") as string;
            var displayIcon = key?.GetValue("DisplayIcon") as string;
            var fromIcon = ExtractExecutablePath(displayIcon);
            if (!string.IsNullOrWhiteSpace(fromIcon))
                paths.Add(fromIcon);
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(BattleNetCatalogReader),
                operation: "ReadRegistryLauncherPaths",
                exception: ex,
                platform: Platform.BattleNet);
        }

        if (!string.IsNullOrWhiteSpace(installLocation))
        {
            var trimmed = installLocation.Trim('"');
            paths.Add(Path.Combine(trimmed, "Battle.net.exe"));
        }

        return paths;
    }

    private static IEnumerable<string> DefaultLauncherPaths()
    {
        yield return @"C:\Program Files (x86)\Battle.net\Battle.net.exe";
        yield return @"C:\Program Files\Battle.net\Battle.net.exe";
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
}
