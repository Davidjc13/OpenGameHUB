using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Providers.Gog;

namespace OpenGameHUB.Providers.Gog;

internal static class GogDesktopScanner
{
    public static IReadOnlyList<UnifiedGame> Scan()
    {
        if (!GogCatalogReader.IsCloudLibraryAvailable())
            return [];

        var clientExe = GogCatalogReader.FindGalaxyClientExecutable();
        if (clientExe is null)
            return [];

        var results = new List<UnifiedGame>();
        foreach (var entry in GogCatalogReader.ReadInstalledEntries())
        {
            var launchArgs = GogCatalogReader.BuildLaunchArguments(entry.GogId, entry.InstallPath);
            var game = new UnifiedGame
            {
                Id = BuildStableId(entry),
                Platform = Platform.Gog,
                PlatformGameId = entry.GogId.ToString(),
                Title = entry.Title,
                IsInstalled = true,
                InstallPath = entry.InstallPath,
                LaunchSpec = LaunchSpec.LauncherArgs(clientExe, launchArgs)
            };

            if (!GameEntryFilter.IsExcluded(game))
                results.Add(game);
        }

        return results;
    }

    private static string BuildStableId(GogCatalogEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.InstallPath))
        {
            var normalizedPath = Path.GetFullPath(entry.InstallPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToLowerInvariant();
            var slug = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(normalizedPath)))[..16]
                .ToLowerInvariant();
            return $"gog:path:{slug}";
        }

        return $"gog:catalog:{entry.GogId}";
    }
}
