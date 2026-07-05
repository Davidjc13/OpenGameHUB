using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using GameFinder.Common;
using GameFinder.StoreHandlers.Xbox;
using NexusMods.Paths;
using OpenGameHUB.Models;

namespace OpenGameHUB.Services;

internal static class XboxGamePassScanner
{
    private static readonly string[] UtilityExecutableTokens =
    [
        "unins", "install", "redist", "crash", "setup", "autorun", "createdump", "dotnet"
    ];

    public static IReadOnlyList<UnifiedGame> Scan()
    {
        var handler = new XboxHandler(FileSystem.Shared);
        var games = new List<UnifiedGame>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in handler.FindAllGames())
        {
            if (!result.TryGetGame(out var xboxGame))
                continue;

            var installPath = xboxGame.Path.ToString();
            var manifestPath = ResolveManifestPath(installPath);
            if (manifestPath is null || !IsGameManifest(manifestPath))
                continue;

            var normalizedPath = NormalizePath(installPath);
            if (!seenPaths.Add(normalizedPath))
                continue;

            var metadata = ReadManifestMetadata(manifestPath);
            var title = metadata.DisplayName ?? xboxGame.DisplayName;
            if (string.IsNullOrWhiteSpace(title))
                continue;

            games.Add(new UnifiedGame
            {
                Id = BuildStableId(normalizedPath),
                Platform = Platform.GamePass,
                PlatformGameId = xboxGame.Id.Value,
                Title = title,
                IsInstalled = true,
                InstallPath = installPath,
                LaunchSpec = BuildLaunchSpec(installPath, manifestPath, metadata)
            });
        }

        return games;
    }

    private static string? ResolveManifestPath(string installPath)
    {
        var direct = Path.Combine(installPath, "AppxManifest.xml");
        if (File.Exists(direct))
            return direct;

        var content = Path.Combine(installPath, "Content", "AppxManifest.xml");
        return File.Exists(content) ? content : null;
    }

    private static bool IsGameManifest(string manifestPath)
    {
        try
        {
            var metadata = ReadManifestMetadata(manifestPath);
            if (metadata.Categories.Any(category =>
                    category.Equals("games", StringComparison.OrdinalIgnoreCase) ||
                    category.Equals("game", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(metadata.Executable);
        }
        catch
        {
            return false;
        }
    }

    private static ManifestMetadata ReadManifestMetadata(string manifestPath)
    {
        var document = XDocument.Load(manifestPath);
        var root = document.Root ?? throw new InvalidDataException("Missing manifest root.");
        var ns = root.Name.Namespace;

        var displayName = root
            .Element(ns + "Properties")
            ?.Element(ns + "DisplayName")
            ?.Value;

        var categories = root
            .Element(ns + "Properties")
            ?.Elements(ns + "Category")
            .Select(element => element.Value.Trim())
            .Where(value => value.Length > 0)
            .ToList() ?? [];

        var application = root
            .Element(ns + "Applications")
            ?.Elements(ns + "Application")
            .FirstOrDefault(element => element.Attribute("Executable") is not null);

        var applicationId = application?.Attribute("Id")?.Value;
        var executable = application?.Attribute("Executable")?.Value;

        return new ManifestMetadata(displayName, categories, applicationId, executable);
    }

    private static LaunchSpec BuildLaunchSpec(
        string installPath,
        string manifestPath,
        ManifestMetadata metadata)
    {
        var executable = FindBestExecutable(installPath, metadata.Executable);
        if (executable is not null)
            return LaunchSpec.Executable(executable, installPath);

        var shellUri = ResolveShellUri(installPath, metadata.ApplicationId);
        if (shellUri is not null)
            return LaunchSpec.Protocol(shellUri);

        var xboxApp = FindXboxAppExecutable();
        if (xboxApp is not null && !string.IsNullOrWhiteSpace(metadata.ApplicationId))
            return LaunchSpec.LauncherArgs(xboxApp, metadata.ApplicationId);

        return LaunchSpec.Executable(installPath);
    }

    private static string? FindBestExecutable(string installPath, string? manifestExecutable)
    {
        if (!string.IsNullOrWhiteSpace(manifestExecutable))
        {
            var manifestExePath = Path.Combine(installPath, manifestExecutable);
            if (File.Exists(manifestExePath) && !IsUtilityExecutable(manifestExePath))
                return manifestExePath;

            var contentExePath = Path.Combine(installPath, "Content", manifestExecutable);
            if (File.Exists(contentExePath) && !IsUtilityExecutable(contentExePath))
                return contentExePath;
        }

        if (!Directory.Exists(installPath))
            return null;

        return Directory
            .EnumerateFiles(installPath, "*.exe", SearchOption.AllDirectories)
            .Where(path => !IsUtilityExecutable(path))
            .OrderByDescending(path => new FileInfo(path).Length)
            .FirstOrDefault();
    }

    private static string? ResolveShellUri(string installPath, string? applicationId)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
            return null;

        try
        {
            var normalizedInstallPath = Path.GetFullPath(installPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments =
                    "-NoProfile -NonInteractive -Command " +
                    $"\"$p=Get-AppxPackage | Where-Object {{ $_.InstallLocation -and ($_.InstallLocation -eq '{EscapePowerShellString(normalizedInstallPath)}' -or '{EscapePowerShellString(normalizedInstallPath)}'.StartsWith($_.InstallLocation, [System.StringComparison]::OrdinalIgnoreCase)) }} | Select-Object -First 1; " +
                    "if ($p) { $id=(Get-AppxPackageManifest $p).Package.Applications.Application.Id; " +
                    "if ($id -is [System.Array]) { $id = $id[0] }; " +
                    "Write-Output (\"shell:AppsFolder/$($p.PackageFamilyName)!$id\") }\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
                return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(TimeSpan.FromSeconds(8));

            return output.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase)
                ? output
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindXboxAppExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WindowsApps")
        };

        foreach (var root in candidates)
        {
            if (!Directory.Exists(root))
                continue;

            var match = Directory
                .EnumerateDirectories(root, "Microsoft.GamingApp_*", SearchOption.TopDirectoryOnly)
                .Select(path => Path.Combine(path, "XboxPcApp.exe"))
                .FirstOrDefault(File.Exists);

            if (match is not null)
                return match;
        }

        return null;
    }

    private static bool IsUtilityExecutable(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        return UtilityExecutableTokens.Any(token => name.Contains(token, StringComparison.Ordinal));
    }

    private static string BuildStableId(string normalizedPath)
    {
        var slug = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath)))[..16]
            .ToLowerInvariant();
        return $"gamepass:path:{slug}";
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string EscapePowerShellString(string value) =>
        value.Replace("'", "''");

    private sealed record ManifestMetadata(
        string? DisplayName,
        IReadOnlyList<string> Categories,
        string? ApplicationId,
        string? Executable);
}
