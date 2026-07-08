using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Win32;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Providers.Ea;

internal static class EaDesktopScanner
{
    private static readonly string[] RegistryRoots =
    [
        @"SOFTWARE\EA Games",
        @"SOFTWARE\WOW6432Node\EA Games"
    ];

    public static IReadOnlyList<UnifiedGame> Scan()
    {
        var games = new List<UnifiedGame>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in RegistryRoots)
        {
            using var baseKey = Registry.LocalMachine.OpenSubKey(root);
            if (baseKey is null)
                continue;

            foreach (var subKeyName in baseKey.GetSubKeyNames())
            {
                using var gameKey = baseKey.OpenSubKey(subKeyName);
                var installDir = gameKey?.GetValue("Install Dir") as string;
                if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir))
                    continue;

                var normalizedPath = NormalizePath(installDir);
                if (!seenPaths.Add(normalizedPath))
                    continue;

                var metadata = ReadInstallerMetadata(installDir);
                var title = metadata.Title ?? subKeyName;
                var contentId = metadata.ContentId;
                var executable = metadata.ExecutablePath ?? FindBestExecutable(installDir);

                games.Add(new UnifiedGame
                {
                    Id = BuildStableId(normalizedPath, contentId),
                    Platform = Platform.Ea,
                    PlatformGameId = contentId ?? subKeyName,
                    Title = title,
                    IsInstalled = true,
                    InstallPath = installDir,
                    LaunchSpec = BuildLaunchSpec(contentId, executable, installDir)
                });
            }
        }

        foreach (var originGame in ScanOriginRegistryGames())
        {
            if (!seenPaths.Add(originGame.InstallPath!))
                continue;

            games.Add(originGame);
        }

        return games;
    }

    private static IEnumerable<UnifiedGame> ScanOriginRegistryGames()
    {
        using var originKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Origin Games");
        if (originKey is null)
            yield break;

        foreach (var offerId in originKey.GetSubKeyNames())
        {
            using var offerKey = originKey.OpenSubKey(offerId);
            var displayName = offerKey?.GetValue("DisplayName") as string;
            if (string.IsNullOrWhiteSpace(displayName))
                continue;

            var installDir = FindInstallDirForOffer(offerId, displayName)
                ?? FindInstallDirByContentId(offerId);
            if (installDir is null)
                continue;

            yield return new UnifiedGame
            {
                Id = BuildStableId(NormalizePath(installDir), offerId),
                Platform = Platform.Ea,
                PlatformGameId = offerId,
                Title = CleanTitle(displayName),
                IsInstalled = true,
                InstallPath = installDir,
                LaunchSpec = BuildLaunchSpec(offerId, FindBestExecutable(installDir), installDir)
            };
        }
    }

    private static string? FindInstallDirForOffer(string offerId, string displayName)
    {
        foreach (var root in RegistryRoots)
        {
            using var baseKey = Registry.LocalMachine.OpenSubKey(root);
            if (baseKey is null)
                continue;

            foreach (var subKeyName in baseKey.GetSubKeyNames())
            {
                using var gameKey = baseKey.OpenSubKey(subKeyName);
                var installDir = gameKey?.GetValue("Install Dir") as string;
                if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir))
                    continue;

                var metadata = ReadInstallerMetadata(installDir);
                if (metadata.ContentId == offerId
                    || metadata.ContentIds.Contains(offerId, StringComparer.OrdinalIgnoreCase)
                    || displayName.Contains(subKeyName, StringComparison.OrdinalIgnoreCase)
                    || subKeyName.Contains(displayName.Split(',')[0], StringComparison.OrdinalIgnoreCase))
                {
                    return installDir;
                }
            }
        }

        return null;
    }

    private static string? FindInstallDirByContentId(string contentId)
    {
        foreach (var root in new[]
                 {
                     @"C:\Program Files\EA Games",
                     @"C:\Program Files (x86)\EA Games",
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EA Games"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "EA Games")
                 })
        {
            if (!Directory.Exists(root))
                continue;

            foreach (var dir in Directory.GetDirectories(root))
            {
                var metadata = ReadInstallerMetadata(dir);
                if (metadata.ContentIds.Contains(contentId, StringComparer.OrdinalIgnoreCase))
                    return dir;
            }
        }

        return null;
    }

    private static InstallerMetadata ReadInstallerMetadata(string installDir)
    {
        var installerFile = Path.Combine(installDir, "__Installer", "installerdata.xml");
        if (!File.Exists(installerFile))
            return InstallerMetadata.Empty;

        try
        {
            var document = XDocument.Load(installerFile);
            var contentIds = document.Descendants("contentID")
                .Select(node => node.Value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var title = document.Descendants("gameTitle")
                .Select(node => node.Value.Trim())
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

            var launcherPath = document.Descendants("launcher")
                .Select(node => node.Element("filePath")?.Value)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

            return new InstallerMetadata(
                CleanTitle(title),
                contentIds.FirstOrDefault(),
                contentIds,
                ResolveRegistryPath(launcherPath));
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(EaDesktopScanner),
                operation: "ReadInstallerMetadata",
                exception: ex,
                platform: Platform.Ea,
                details: installerFile);
            return InstallerMetadata.Empty;
        }
    }

    private static string? ResolveRegistryPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = Regex.Match(value, @"\[(?<registry>.+)\](?<file>.*)$");
        if (!match.Success)
            return File.Exists(value) ? value : null;

        var registrySpec = match.Groups["registry"].Value;
        var fileName = match.Groups["file"].Value;
        var lastSeparator = registrySpec.LastIndexOf('\\');
        if (lastSeparator <= 0)
            return null;

        var registryPath = registrySpec[..lastSeparator];
        var valueName = registrySpec[(lastSeparator + 1)..];

        if (registryPath.StartsWith("HKEY_LOCAL_MACHINE\\", StringComparison.OrdinalIgnoreCase))
            registryPath = registryPath["HKEY_LOCAL_MACHINE\\".Length..];
        else if (registryPath.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase))
            registryPath = registryPath["HKLM\\".Length..];

        using var key = Registry.LocalMachine.OpenSubKey(registryPath);
        var installDir = key?.GetValue(valueName) as string;
        if (string.IsNullOrWhiteSpace(installDir))
            return null;

        var candidate = Path.Combine(installDir, fileName);
        return File.Exists(candidate) ? candidate : null;
    }

    private static LaunchSpec BuildLaunchSpec(string? contentId, string? executable, string installDir)
    {
        if (!string.IsNullOrWhiteSpace(executable) && File.Exists(executable))
            return LaunchSpec.Executable(executable);

        if (!string.IsNullOrWhiteSpace(contentId))
            return LaunchSpec.Protocol($"link2ea://launchgame/contentids/{contentId}");

        var eaDesktop = FindEaDesktopExecutable();
        if (eaDesktop is not null)
            return LaunchSpec.LauncherArgs(eaDesktop, string.Empty);

        return LaunchSpec.Executable(FindBestExecutable(installDir) ?? installDir);
    }

    private static string? FindEaDesktopExecutable()
    {
        var candidates = new[]
        {
            @"C:\Program Files\Electronic Arts\EA Desktop\EA Desktop\EADesktop.exe",
            @"C:\Program Files\Electronic Arts\EA Desktop\EADesktop.exe",
            @"C:\Program Files (x86)\Origin\Origin.exe"
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindBestExecutable(string installDir)
    {
        if (!Directory.Exists(installDir))
            return null;

        return Directory
            .EnumerateFiles(installDir, "*.exe", SearchOption.TopDirectoryOnly)
            .Where(path => !IsUtilityExecutable(path))
            .OrderByDescending(path => new FileInfo(path).Length)
            .FirstOrDefault();
    }

    private static bool IsUtilityExecutable(string path)
    {
        var name = Path.GetFileName(path);
        return name.Contains("unins", StringComparison.OrdinalIgnoreCase)
               || name.Contains("setup", StringComparison.OrdinalIgnoreCase)
               || name.Contains("redist", StringComparison.OrdinalIgnoreCase)
               || name.Contains("eac", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildStableId(string normalizedPath, string? contentId)
    {
        if (!string.IsNullOrWhiteSpace(normalizedPath))
        {
            var slug = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(normalizedPath)))[..16]
                .ToLowerInvariant();
            return $"ea:path:{slug}";
        }

        return $"ea:{contentId ?? Guid.NewGuid().ToString("N")}";
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToLowerInvariant();

    private static string CleanTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "Juego de EA";

        return title
            .Replace("™", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("®", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace('\u201c', ' ')
            .Replace('\u201d', ' ')
            .Trim();
    }

    private sealed record InstallerMetadata(
        string? Title,
        string? ContentId,
        IReadOnlyList<string> ContentIds,
        string? ExecutablePath)
    {
        public static InstallerMetadata Empty { get; } = new(null, null, [], null);
    }
}
