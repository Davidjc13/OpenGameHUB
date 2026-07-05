using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using OpenGameHUB.Models;
using OpenGameHUB.Services.Epic;

namespace OpenGameHUB.Services;

public static class LegendaryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly object ExecutableLock = new();
    private static string? _cachedExecutable;
    private static bool _executableResolved;

    public static bool IsAvailable() => FindExecutable() is not null;

    public static bool IsEpicLauncherInstalled() => FindEpicLauncherExecutable() is not null;

    public static bool ShouldOfferAuthPrompt() =>
        IsEpicLauncherInstalled()
        && IsAvailable()
        && !HasStoredCredentials();

    public static bool HasStoredCredentials()
    {
        return TryReadUserData(out _);
    }

    public static string? GetDisplayName()
    {
        return TryReadUserData(out var userData)
            && userData.TryGetProperty("displayName", out var name)
            && name.ValueKind == JsonValueKind.String
            ? name.GetString()
            : null;
    }

    private static bool TryReadUserData(out JsonElement userData)
    {
        userData = default;

        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "legendary",
            "config.json");

        if (!File.Exists(configPath))
            return false;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configPath));
            if (!document.RootElement.TryGetProperty("userData", out userData)
                || userData.ValueKind != JsonValueKind.Object
                || !userData.EnumerateObject().Any())
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<IReadOnlyList<LegendaryCatalogEntry>> ListCatalogEntriesAsync(
        CancellationToken cancellationToken = default)
    {
        var legendary = FindExecutable();
        if (legendary is null)
            return [];

        try
        {
            var output = await RunAsync(legendary, "list-games --json", cancellationToken);
            if (string.IsNullOrWhiteSpace(output))
                return [];

            return ParseCatalogJson(output);
        }
        catch
        {
            return [];
        }
    }

    public static IReadOnlyList<LegendaryCatalogEntry> ListCatalogEntries(
        CancellationToken cancellationToken = default) =>
        ListCatalogEntriesAsync(cancellationToken).GetAwaiter().GetResult();

    public static void RunInstall(string appName) =>
        RunInConsole(FindExecutable(), $"install {appName}");

    public static void RunLaunch(string appName) =>
        RunInConsole(FindExecutable(), $"launch {appName}");

    public static void RunAuth() =>
        RunHidden(FindExecutable(), "auth");

    public static void RunDisconnect()
    {
        var legendary = FindExecutable();
        if (legendary is not null)
        {
            try
            {
                RunAsync(legendary, "auth --delete", CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
            catch
            {
                // fall back to removing local credentials
            }
        }

        TryDeleteCredentialsFile();
    }

    private static void TryDeleteCredentialsFile()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "legendary",
            "config.json");

        try
        {
            if (File.Exists(configPath))
                File.Delete(configPath);
        }
        catch
        {
            // optional
        }
    }

    public static void InvalidateExecutableCache()
    {
        lock (ExecutableLock)
        {
            _cachedExecutable = null;
            _executableResolved = false;
        }
    }

    private static void RunHidden(string? executable, string arguments)
    {
        if (string.IsNullOrWhiteSpace(executable))
            throw new InvalidOperationException(Loc.T("CannotRunLegendary"));

        var psi = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(executable) ?? string.Empty,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        if (Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("CannotRunLegendary"));
    }

    private static void RunInConsole(string? executable, string arguments)
    {
        if (string.IsNullOrWhiteSpace(executable))
            throw new InvalidOperationException(Loc.T("CannotRunLegendary"));

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c start \"legendary\" \"{executable}\" {arguments}",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("CannotRunLegendary"));
    }

    public static string? FindExecutable()
    {
        lock (ExecutableLock)
        {
            if (_executableResolved)
                return _cachedExecutable;

            _cachedExecutable = ResolveExecutable();
            _executableResolved = true;
            return _cachedExecutable;
        }
    }

    private static string? ResolveExecutable()
    {
        var bundled = LegendaryBootstrap.BundledExecutablePath;
        if (!string.IsNullOrWhiteSpace(bundled))
            return bundled;

        if (File.Exists(LegendaryBootstrap.ManagedExecutablePath))
            return LegendaryBootstrap.ManagedExecutablePath;

        var onPath = TryFindOnPath();
        if (!string.IsNullOrWhiteSpace(onPath))
            return onPath;

        foreach (var candidate in BuildKnownExecutableCandidates())
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string? TryFindOnPath()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = "legendary",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
                return null;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);

            if (process.ExitCode != 0)
                return null;

            foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = line.Trim().Trim('"');
                if (candidate.EndsWith("legendary.exe", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        catch
        {
            // optional
        }

        return null;
    }

    public static string? FindEpicLauncherExecutable()
    {
        var candidates = new[]
        {
            @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win32\EpicGamesLauncher.exe",
            @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe",
            @"C:\Program Files\Epic Games\Launcher\Portal\Binaries\Win32\EpicGamesLauncher.exe",
            @"C:\Program Files\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe"
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\EpicGames\EpicGamesLauncher");
            var launcherPath = key?.GetValue("AppPath") as string;
            if (!string.IsNullOrWhiteSpace(launcherPath))
            {
                var exe = Path.Combine(launcherPath.TrimEnd('\\'), "Portal", "Binaries", "Win32", "EpicGamesLauncher.exe");
                if (File.Exists(exe))
                    return exe;
            }
        }
        catch
        {
            // optional
        }

        return null;
    }

    private static IEnumerable<string> BuildKnownExecutableCandidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        foreach (var candidate in TryAddCandidate(seen, Path.Combine(userProfile, ".local", "bin", "legendary.exe")))
            yield return candidate;
        foreach (var candidate in TryAddCandidate(seen, Path.Combine(userProfile, "AppData", "Roaming", "Python", "Scripts", "legendary.exe")))
            yield return candidate;
        foreach (var candidate in TryAddCandidate(seen, Path.Combine(localAppData, "Programs", "Python", "Scripts", "legendary.exe")))
            yield return candidate;
        foreach (var candidate in TryAddCandidate(seen, Path.Combine(localAppData, "pipx", "venvs", "legendary", "Scripts", "legendary.exe")))
            yield return candidate;
    }

    private static IEnumerable<string> TryAddCandidate(HashSet<string> seen, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            yield break;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch
        {
            yield break;
        }

        if (seen.Add(fullPath))
            yield return fullPath;
    }

    private static IReadOnlyList<LegendaryCatalogEntry> ParseCatalogJson(string output)
    {
        try
        {
            var entries = JsonSerializer.Deserialize<List<LegendaryGame>>(output, JsonOptions) ?? [];
            return entries
                .Where(entry => !entry.IsDlc && !string.IsNullOrWhiteSpace(entry.AppName))
                .Select(entry => new LegendaryCatalogEntry(
                    entry.AppName.Trim(),
                    string.IsNullOrWhiteSpace(entry.AppTitle) ? entry.AppName.Trim() : entry.AppTitle.Trim()))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static async Task<string> RunAsync(
        string executable,
        string arguments,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException(Loc.T("CannotRunLegendary"));

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                ? Loc.T("LegendaryFailed", process.ExitCode)
                : error.Trim());

        return output;
    }

    private sealed class LegendaryGame
    {
        [JsonPropertyName("app_name")]
        public string AppName { get; set; } = string.Empty;

        [JsonPropertyName("app_title")]
        public string AppTitle { get; set; } = string.Empty;

        [JsonPropertyName("is_dlc")]
        public bool IsDlc { get; set; }
    }
}

public sealed record LegendaryCatalogEntry(string AppName, string AppTitle);
