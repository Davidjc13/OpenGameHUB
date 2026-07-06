using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace OpenGameHUB.Providers.Steam;

public static class SteamLocalAccountReader
{
    private static readonly Regex SteamIdRegex = new(@"^\""(\d{17})\""\s*$", RegexOptions.Compiled);

    public sealed record SteamLocalAccount(
        string SteamId64,
        string? AccountName,
        string? PersonaName);

    public static bool IsSteamInstalled => FindSteamInstallPath() is not null;

    public static SteamLocalAccount? DetectActiveAccount()
    {
        var installPath = FindSteamInstallPath();
        if (installPath is null)
            return null;

        var loginUsersPath = Path.Combine(installPath, "config", "loginusers.vdf");
        if (!File.Exists(loginUsersPath))
            return null;

        return ParseLoginUsers(loginUsersPath);
    }

    private static SteamLocalAccount? ParseLoginUsers(string path)
    {
        SteamLocalAccount? mostRecent = null;
        SteamLocalAccount? fallback = null;

        string? currentId = null;
        string? accountName = null;
        string? personaName = null;
        var isMostRecent = false;

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();

            var idMatch = SteamIdRegex.Match(line);
            if (idMatch.Success)
            {
                currentId = idMatch.Groups[1].Value;
                accountName = null;
                personaName = null;
                isMostRecent = false;
                continue;
            }

            if (currentId is null)
                continue;

            if (line.StartsWith("\"AccountName\"", StringComparison.Ordinal))
                accountName = ReadVdfValue(line);
            else if (line.StartsWith("\"PersonaName\"", StringComparison.Ordinal))
                personaName = ReadVdfValue(line);
            else if (line.StartsWith("\"MostRecent\"", StringComparison.Ordinal))
                isMostRecent = ReadVdfValue(line) == "1";

            if (line != "}")
                continue;

            var account = new SteamLocalAccount(currentId, accountName, personaName);
            fallback = account;
            if (isMostRecent)
                mostRecent = account;

            currentId = null;
        }

        return mostRecent ?? fallback;
    }

    private static string? ReadVdfValue(string line)
    {
        var parts = line.Split('"', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2 ? parts[^1] : null;
    }

    public static string? FindSteamInstallPath()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
            using var steamKey = baseKey.OpenSubKey(@"Software\Valve\Steam");
            var steamPath = steamKey?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(steamPath))
            {
                var normalized = steamPath.Replace('/', Path.DirectorySeparatorChar).TrimEnd('\\', '/');
                if (Directory.Exists(normalized))
                    return normalized;
            }
        }

        const string defaultPath = @"C:\Program Files (x86)\Steam";
        return Directory.Exists(defaultPath) ? defaultPath : null;
    }
}
