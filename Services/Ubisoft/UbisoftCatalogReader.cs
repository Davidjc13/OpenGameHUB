using Microsoft.Win32;
using ProtoBuf;
using YamlDotNet.Serialization;
using YamlDeserializer = YamlDotNet.Serialization.IDeserializer;

using OpenGameHUB.Models;

namespace OpenGameHUB.Services.Ubisoft;

internal static class UbisoftCatalogReader
{
    internal const string AssetUrlBase =
        "https://ubistatic3-a.akamaihd.net/orbit/uplay_launcher_3_0/assets/";

    private static readonly YamlDeserializer YamlDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    public static bool IsLauncherInstalled()
    {
        return !string.IsNullOrWhiteSpace(FindLauncherExecutable())
               || !string.IsNullOrWhiteSpace(FindLauncherInstallDir());
    }

    public static string? FindConfigurationsCachePath()
    {
        var candidates = new List<string>();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        candidates.Add(Path.Combine(
            localAppData,
            "Ubisoft Game Launcher",
            "cache",
            "configuration",
            "configurations"));

        var installDir = FindLauncherInstallDir();
        if (!string.IsNullOrWhiteSpace(installDir))
        {
            candidates.Add(Path.Combine(installDir, "cache", "configuration", "configurations"));
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    public static IReadOnlyList<UbisoftCatalogEntry> ReadLibraryEntries()
    {
        var cachePath = FindConfigurationsCachePath()
            ?? throw new FileNotFoundException(Loc.T("UbisoftCacheMissing"), "configurations");

        using var file = File.OpenRead(cachePath);
        var cacheData = ProtoBuf.Serializer.Deserialize<UbisoftCacheGameCollection>(file);
        if (cacheData?.Games is null || cacheData.Games.Count == 0)
            throw new InvalidDataException(Loc.T("UbisoftCacheMissing"));

        var dlcsToIgnore = new HashSet<uint>();
        var entries = new List<UbisoftCatalogEntry>();

        foreach (var item in cacheData.Games)
        {
            if (string.IsNullOrWhiteSpace(item.GameInfo))
                continue;

            UbisoftProductInformation productInfo;
            try
            {
                productInfo = YamlDeserializer.Deserialize<UbisoftProductInformation>(item.GameInfo);
            }
            catch
            {
                continue;
            }

            var root = productInfo.root;
            if (root is null)
                continue;

            if (root.addons is { Count: > 0 })
            {
                foreach (var addon in root.addons)
                    dlcsToIgnore.Add(addon.id);
            }

            if (root.third_party_platform is not null)
                continue;

            if (root.is_ulc)
            {
                dlcsToIgnore.Add(item.UplayId);
                continue;
            }

            if (dlcsToIgnore.Contains(item.UplayId))
                continue;

            if (root.start_game is null)
                continue;

            var title = ResolveLocalizedName(productInfo, root.name);
            if (string.IsNullOrWhiteSpace(title))
                title = root.name ?? string.Empty;

            if (title is "NAME" or "GAMENAME" || string.IsNullOrWhiteSpace(title))
                continue;

            var thumbUrl = ResolveAssetUrl(productInfo, root.thumb_image);
            entries.Add(new UbisoftCatalogEntry(item.UplayId, title.Trim(), thumbUrl));
        }

        return entries;
    }

    public static void EnrichCatalogCoverUrls(IReadOnlyList<UnifiedGame> games)
    {
        if (!IsLauncherInstalled())
            return;

        IReadOnlyList<UbisoftCatalogEntry> entries;
        try
        {
            entries = ReadLibraryEntries();
        }
        catch
        {
            return;
        }

        var byId = entries.ToDictionary(e => e.UplayId.ToString(), StringComparer.Ordinal);
        foreach (var game in games)
        {
            if (game.Platform != Platform.Ubisoft || !string.IsNullOrWhiteSpace(game.CoverPath))
                continue;

            if (byId.TryGetValue(game.PlatformGameId, out var entry)
                && !string.IsNullOrWhiteSpace(entry.ThumbImageUrl))
            {
                game.CatalogCoverUrl = entry.ThumbImageUrl;
            }
        }
    }

    public static string? FindLauncherExecutable()
    {
        var installDir = FindLauncherInstallDir();
        if (string.IsNullOrWhiteSpace(installDir))
            return null;

        foreach (var name in new[] { "UbisoftConnect.exe", "upc.exe", "Uplay.exe" })
        {
            var candidate = Path.Combine(installDir, name);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string? FindLauncherInstallDir()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var launcherKey = baseKey.OpenSubKey(@"SOFTWARE\Ubisoft\Launcher");
            var installDir = launcherKey?.GetValue("InstallDir") as string;
            if (!string.IsNullOrWhiteSpace(installDir) && Directory.Exists(installDir))
                return installDir.TrimEnd('\\', '/');
        }

        return null;
    }

    private static string ResolveLocalizedName(UbisoftProductInformation productInfo, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        if (productInfo.localizations?.@default is { } loc
            && loc.TryGetValue(name, out var localized)
            && !string.IsNullOrWhiteSpace(localized))
        {
            return localized;
        }

        return name;
    }

    private static string? ResolveAssetUrl(UbisoftProductInformation productInfo, string? assetKey)
    {
        if (string.IsNullOrWhiteSpace(assetKey))
            return null;

        var resolved = ResolveLocalizedName(productInfo, assetKey);
        if (string.IsNullOrWhiteSpace(resolved))
            return null;

        if (resolved.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || resolved.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return resolved;
        }

        return AssetUrlBase + resolved;
    }
}
