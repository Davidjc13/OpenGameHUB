using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;
using YamlDotNet.Serialization;
using YamlDeserializer = YamlDotNet.Serialization.IDeserializer;

namespace OpenGameHUB.Providers.Riot;

internal sealed record RiotCatalogEntry(string ProductId, string Title, string Patchline = "live");

internal static class RiotCatalogReader
{
    private static readonly Regex LaunchPatchlineRegex = new(
        @"--launch-patchline=(?<patchline>[a-z0-9_]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LaunchProductRegex = new(
        @"--launch-product=(?<slug>[a-z0-9_]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly YamlDeserializer YamlDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly RiotCatalogEntry[] KnownProducts =
    [
        new("league_of_legends", "League of Legends"),
        new("valorant", "VALORANT"),
        new("bacon", "Legends of Runeterra"),
        new("lion", "2XKO"),
        new("tft", "Teamfight Tactics")
    ];

    private static readonly Regex MetadataFolderRegex = new(
        @"^(?<product>[a-z0-9_]+)\.(?<patchline>[a-z0-9_]+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<string, string> ProductAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["teamfighttactics"] = "tft"
    };

    private static readonly HashSet<string> AllowedPatchlines = new(StringComparer.OrdinalIgnoreCase)
    {
        "live"
    };

    private static readonly HashSet<string> ExcludedProductIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "riot_client",
        "riotclient"
    };

    public static bool IsLauncherInstalled() => FindClientServicesExecutable() is not null;

    public static string? FindClientServicesExecutable()
    {
        foreach (var path in BuildClientExecutableCandidates())
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    public static string? FindClientInstallDirectory()
    {
        var executable = FindClientServicesExecutable();
        return string.IsNullOrWhiteSpace(executable)
            ? null
            : Path.GetDirectoryName(executable);
    }

    public static IReadOnlyList<RiotCatalogEntry> ReadLibraryEntries()
    {
        if (!IsLauncherInstalled())
            return [];

        var entries = new Dictionary<string, RiotCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var product in KnownProducts)
            entries[product.ProductId] = product;

        foreach (var discovered in DiscoverMetadataProducts())
        {
            if (ExcludedProductIds.Contains(discovered.ProductId))
                continue;

            if (!entries.ContainsKey(discovered.ProductId))
                entries[discovered.ProductId] = discovered;
        }

        return entries.Values
            .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsProductInstalled(string productId, string patchline = "live")
    {
        if (string.IsNullOrWhiteSpace(productId))
            return false;

        var settingsPath = GetProductSettingsPath(productId, patchline);
        if (settingsPath is null || !File.Exists(settingsPath))
            return false;

        try
        {
            var settings = YamlDeserializer.Deserialize<RiotProductSettings>(File.ReadAllText(settingsPath));
            return !string.IsNullOrWhiteSpace(settings.ProductInstallFullPath)
                   && Directory.Exists(settings.ProductInstallFullPath);
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(RiotCatalogReader),
                operation: "IsProductInstalled.ReadSettings",
                exception: ex,
                platform: Platform.Riot,
                details: $"{productId}@{patchline}");
            return TryReadInstallPathFromYaml(settingsPath) is not null;
        }
    }

    public static bool MatchesInstalledGame(UnifiedGame game, RiotCatalogEntry entry)
    {
        if (game.Platform != Platform.Riot)
            return false;

        if (!string.IsNullOrWhiteSpace(game.PlatformGameId)
            && string.Equals(game.PlatformGameId, entry.ProductId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (TryGetLaunchProduct(game, out var launchProduct)
            && string.Equals(launchProduct, entry.ProductId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var gameTitle = MetadataSearchHelper.NormalizeTitle(game.Title).ToLowerInvariant();
        var entryTitle = MetadataSearchHelper.NormalizeTitle(entry.Title).ToLowerInvariant();
        return gameTitle == entryTitle;
    }

    public static string BuildLaunchArguments(string productId, string patchline = "live", bool install = false)
    {
        var args = $"--launch-product={productId} --launch-patchline={patchline}";
        if (install)
            args += " --skip-to-install";

        return args;
    }

    private static IEnumerable<string> BuildClientExecutableCandidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in ReadInstallsJsonPaths())
        {
            if (seen.Add(path))
                yield return path;
        }

        foreach (var path in ReadRegistryPaths())
        {
            if (seen.Add(path))
                yield return path;
        }

        foreach (var path in DefaultInstallPaths())
        {
            if (seen.Add(path))
                yield return path;
        }
    }

    private static IEnumerable<string> ReadInstallsJsonPaths()
    {
        var installsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Riot Games",
            "RiotClientInstalls.json");

        if (!File.Exists(installsPath))
            return [];

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(installsPath));
            var root = document.RootElement;
            var paths = new List<string>();

            foreach (var key in new[] { "rc_live", "rc_default", "rc_beta" })
            {
                if (root.TryGetProperty(key, out var directPath)
                    && directPath.ValueKind == JsonValueKind.String)
                {
                    AddClientPath(paths, directPath.GetString());
                }
            }

            if (root.TryGetProperty("associated_client", out var associatedClient)
                && associatedClient.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in associatedClient.EnumerateObject())
                {
                    AddClientPath(paths, property.Name);
                    if (property.Value.ValueKind == JsonValueKind.String)
                        AddClientPath(paths, property.Value.GetString());
                }
            }

            return paths;
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(RiotCatalogReader),
                operation: "ReadInstallsJsonPaths",
                exception: ex,
                platform: Platform.Riot,
                details: installsPath);
            return [];
        }
    }

    private static void AddClientPath(ICollection<string> paths, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return;

        candidate = candidate.Trim().Trim('"');
        if (candidate.EndsWith("RiotClientServices.exe", StringComparison.OrdinalIgnoreCase))
        {
            paths.Add(candidate);
            return;
        }

        if (!Directory.Exists(candidate))
            return;

        var nested = Path.Combine(candidate, "RiotClientServices.exe");
        if (File.Exists(nested))
            paths.Add(nested);
    }

    private static IEnumerable<string> ReadRegistryPaths()
    {
        var paths = new List<string>();
        foreach (var root in new[] { Registry.ClassesRoot, Registry.LocalMachine })
        {
            try
            {
                using var key = root.OpenSubKey(@"riotclient\shell\open\command");
                var command = key?.GetValue(null) as string;
                var executable = ExtractExecutablePath(command);
                if (!string.IsNullOrWhiteSpace(executable))
                    paths.Add(executable);
            }
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(RiotCatalogReader),
                    operation: "ReadRegistryPaths",
                    exception: ex,
                    platform: Platform.Riot,
                    details: root.Name);
            }
        }

        foreach (var path in paths)
            yield return path;
    }

    private static IEnumerable<string> DefaultInstallPaths()
    {
        foreach (var root in new[]
                 {
                     @"C:\Riot Games\Riot Client\RiotClientServices.exe",
                     @"D:\Riot Games\Riot Client\RiotClientServices.exe",
                     @"E:\Riot Games\Riot Client\RiotClientServices.exe"
                 })
        {
            yield return root;
        }
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

    private static IEnumerable<RiotCatalogEntry> DiscoverMetadataProducts()
    {
        var metadataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Riot Games",
            "Metadata");

        if (!Directory.Exists(metadataRoot))
            yield break;

        foreach (var directory in Directory.EnumerateDirectories(metadataRoot))
        {
            var folderName = Path.GetFileName(directory);
            var match = MetadataFolderRegex.Match(folderName);
            if (!match.Success)
                continue;

            var productId = match.Groups["product"].Value;
            if (ProductAliases.TryGetValue(productId, out var canonicalId))
                productId = canonicalId;

            var patchline = match.Groups["patchline"].Value;
            if (!AllowedPatchlines.Contains(patchline))
                continue;

            if (ExcludedProductIds.Contains(productId))
                continue;

            var known = KnownProducts.FirstOrDefault(
                entry => string.Equals(entry.ProductId, productId, StringComparison.OrdinalIgnoreCase));
            var title = known?.Title ?? FormatProductTitle(productId);
            yield return new RiotCatalogEntry(productId, title, patchline);
        }
    }

    private static string FormatProductTitle(string productId) =>
        string.Join(' ',
            productId.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    private static string? GetProductSettingsPath(string productId, string patchline)
    {
        var settingsFile = $"{productId}.{patchline}.product_settings.yaml";
        var metadataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Riot Games",
            "Metadata",
            $"{productId}.{patchline}",
            settingsFile);

        return File.Exists(metadataRoot) ? metadataRoot : null;
    }

    private static string? TryReadInstallPathFromYaml(string settingsPath)
    {
        try
        {
            foreach (var line in File.ReadLines(settingsPath))
            {
                if (!line.TrimStart().StartsWith("product_install_full_path:", StringComparison.Ordinal))
                    continue;

                var value = line.Split(':', 2)[1].Trim().Trim('"');
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(RiotCatalogReader),
                operation: "TryReadInstallPathFromYaml",
                exception: ex,
                platform: Platform.Riot,
                details: settingsPath);
        }

        return null;
    }

    public static bool TryGetLaunchProductFromSpec(LaunchSpec launchSpec, out string productId) =>
        TryParseLaunchSpec(launchSpec, out productId, out _);

    public static bool TryGetLaunchPatchlineFromSpec(LaunchSpec launchSpec, out string patchline)
    {
        if (TryParseLaunchSpec(launchSpec, out _, out var parsed) && !string.IsNullOrWhiteSpace(parsed))
        {
            patchline = parsed;
            return true;
        }

        patchline = string.Empty;
        return false;
    }

    private static bool TryGetLaunchProduct(UnifiedGame game, out string productId) =>
        TryParseLaunchSpec(game.LaunchSpec, out productId, out _);

    private static bool TryParseLaunchSpec(LaunchSpec launchSpec, out string productId, out string? patchline)
    {
        productId = string.Empty;
        patchline = null;

        if (launchSpec.Kind != "launcher-args")
            return false;

        var parts = launchSpec.Value.Split('|', 2);
        if (parts.Length < 2)
            return false;

        var args = parts[1];
        var productMatch = LaunchProductRegex.Match(args);
        if (!productMatch.Success)
            return false;

        productId = productMatch.Groups["slug"].Value;
        if (productId.Length == 0)
            return false;

        var patchlineMatch = LaunchPatchlineRegex.Match(args);
        if (patchlineMatch.Success)
            patchline = patchlineMatch.Groups["patchline"].Value;

        return true;
    }

    private sealed class RiotProductSettings
    {
        [YamlDotNet.Serialization.YamlMember(Alias = "product_install_full_path")]
        public string? ProductInstallFullPath { get; set; }
    }
}
