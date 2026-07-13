using System.Runtime.Versioning;
using OpenGameHUB.Infrastructure.Windows;

namespace OpenGameHUB.Services.Games;

public sealed record InstalledProgramEntry(string DisplayName, string ExecutablePath);

[SupportedOSPlatform("windows")]
public static class InstalledProgramScanner
{
    private static readonly HashSet<string> IgnoredExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "uninstall.exe",
        "uninst.exe",
        "setup.exe",
        "install.exe",
        "update.exe",
        "launcher.exe",
        "helper.exe"
    };

    public static IReadOnlyList<InstalledProgramEntry> Scan()
    {
        var entries = new Dictionary<string, InstalledProgramEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in GetStartMenuRoots())
        {
            if (!Directory.Exists(root))
                continue;

            foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(file);
                if (!extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var executablePath = extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
                    ? file
                    : WindowsShortcutResolver.ResolveTargetPath(file);

                if (!TryNormalizeExecutable(executablePath, out var normalizedPath))
                    continue;

                if (entries.ContainsKey(normalizedPath))
                    continue;

                var displayName = Path.GetFileNameWithoutExtension(file);
                if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                    displayName = Path.GetFileNameWithoutExtension(file);

                entries[normalizedPath] = new InstalledProgramEntry(displayName, normalizedPath);
            }
        }

        return entries.Values
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> GetStartMenuRoots()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
        if (!string.IsNullOrWhiteSpace(programData))
            yield return programData;

        var userPrograms = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        if (!string.IsNullOrWhiteSpace(userPrograms))
            yield return userPrograms;
    }

    private static bool TryNormalizeExecutable(string? path, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            normalizedPath = Path.GetFullPath(path.Trim('"'));
        }
        catch
        {
            return false;
        }

        if (!normalizedPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!File.Exists(normalizedPath))
            return false;

        var fileName = Path.GetFileName(normalizedPath);
        return !IgnoredExecutables.Contains(fileName);
    }
}
