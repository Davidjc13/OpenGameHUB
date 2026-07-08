using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using GameFinder.Common;
using GameFinder.StoreHandlers.Xbox;
using NexusMods.Paths;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Providers.Xbox;

namespace OpenGameHUB.Providers.Xbox;

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
            var manifestPath = XboxManifestReader.ResolveManifestPath(installPath);
            if (manifestPath is null)
                continue;

            XboxManifestReader.ManifestInfo metadata;
            try
            {
                metadata = XboxManifestReader.Read(manifestPath);
            }
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(XboxGamePassScanner),
                    operation: "Scan.ReadManifest",
                    exception: ex,
                    platform: Platform.GamePass,
                    details: manifestPath);
                continue;
            }

            if (!XboxManifestReader.IsGameManifest(metadata))
                continue;

            var normalizedPath = NormalizePath(installPath);
            if (!seenPaths.Add(normalizedPath))
                continue;

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
                CatalogCoverUrl = metadata.CoverPath,
                LaunchSpec = BuildLaunchSpec(installPath, manifestPath, metadata)
            });
        }

        return games;
    }

    private static LaunchSpec BuildLaunchSpec(
        string installPath,
        string manifestPath,
        XboxManifestReader.ManifestInfo metadata)
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
        return XboxInstallClient.FindXboxAppExecutable();
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
}
