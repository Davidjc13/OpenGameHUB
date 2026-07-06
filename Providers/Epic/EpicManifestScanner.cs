using System.Text.Json;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Providers.Epic;

internal static class EpicManifestScanner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] ManifestRoots =
    [
        @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests",
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests")
    ];

    public static IReadOnlyList<UnifiedGame> ScanInstalled()
    {
        var games = new List<UnifiedGame>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in ManifestRoots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(root))
                continue;

            IEnumerable<string> itemFiles;
            try
            {
                itemFiles = Directory.EnumerateFiles(root, "*.item", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var itemFile in itemFiles)
            {
                try
                {
                    var game = TryParseInstalledManifest(itemFile);
                    if (game is null)
                        continue;

                    var pathKey = game.InstallPath ?? game.Id;
                    if (!seenPaths.Add(pathKey))
                        continue;

                    if (!GameEntryFilter.IsExcluded(game))
                        games.Add(game);
                }
                catch
                {
                    // optional per file
                }
            }
        }

        return games;
    }

    private static UnifiedGame? TryParseInstalledManifest(string itemFile)
    {
        var json = File.ReadAllText(itemFile);
        var manifest = JsonSerializer.Deserialize<EpicItemManifest>(json, JsonOptions);
        if (manifest is null
            || manifest.IsIncomplete
            || string.IsNullOrWhiteSpace(manifest.InstallLocation)
            || !Directory.Exists(manifest.InstallLocation))
        {
            return null;
        }

        var appName = string.IsNullOrWhiteSpace(manifest.AppName)
            ? Path.GetFileNameWithoutExtension(itemFile)
            : manifest.AppName.Trim();

        var title = string.IsNullOrWhiteSpace(manifest.DisplayName)
            ? appName
            : manifest.DisplayName.Trim();

        var installPath = manifest.InstallLocation.Trim();
        var executable = FindBestExecutable(installPath);

        return new UnifiedGame
        {
            Id = $"epic:manifest:{appName}@{NormalizePath(installPath)}",
            Platform = Platform.Epic,
            PlatformGameId = appName,
            Title = title,
            IsInstalled = true,
            InstallPath = installPath,
            LaunchSpec = BuildLaunchSpec(appName, executable, installPath)
        };
    }

    private static LaunchSpec BuildLaunchSpec(string appName, string? executable, string installPath)
    {
        if (!string.IsNullOrWhiteSpace(executable) && File.Exists(executable))
            return LaunchSpec.Executable(executable);

        return LaunchSpec.Protocol($"com.epicgames.launcher://apps/{appName}?action=launch&silent=true");
    }

    private static string? FindBestExecutable(string installPath)
    {
        if (!Directory.Exists(installPath))
            return null;

        try
        {
            return Directory
                .GetFiles(installPath, "*.exe", SearchOption.TopDirectoryOnly)
                .Where(path => !IsUtilityExecutable(path))
                .OrderByDescending(path => new FileInfo(path).Length)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsUtilityExecutable(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        return name is "uninstall.exe" or "uninst.exe" or "setup.exe" or "launcher.exe"
               || name.StartsWith("easyanticheat", StringComparison.OrdinalIgnoreCase)
               || name is "unitycrashhandler64.exe" or "unitycrashhandler32.exe";
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private sealed class EpicItemManifest
    {
        public string AppName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string InstallLocation { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("bIsIncompleteInstall")]
        public bool IsIncomplete { get; set; }
    }
}
