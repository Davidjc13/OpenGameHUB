using System.Diagnostics;
using Microsoft.Win32;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Providers.Epic;

namespace OpenGameHUB.Services.Games;

internal sealed class GameLaunchService
{
    private readonly IReadOnlyList<ICloudLibraryProvider> _cloudProviders;

    public GameLaunchService(IReadOnlyList<ICloudLibraryProvider> cloudProviders)
    {
        _cloudProviders = cloudProviders;
    }

    public void Launch(UnifiedGame game)
    {
        var attempts = BuildLaunchAttempts(game);
        var errors = new List<string>();

        foreach (var attempt in attempts)
        {
            try
            {
                attempt();
                return;
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }

        throw new InvalidOperationException(
            errors.Count == 0
                ? Loc.T("NoLaunchMethod")
                : string.Join(" | ", errors));
    }

    private List<Action> BuildLaunchAttempts(UnifiedGame game)
    {
        var attempts = new List<Action>();

        if (!game.IsInstalled)
        {
            foreach (var provider in _cloudProviders.Where(p => p.Platform == game.Platform))
            {
                attempts.AddRange(provider.GetInstallLaunchAttempts(game));
            }
        }

        if (!string.IsNullOrWhiteSpace(game.LaunchSpec.Kind) &&
            !string.IsNullOrWhiteSpace(game.LaunchSpec.Value))
        {
            switch (game.LaunchSpec.Kind)
            {
                case "executable":
                    attempts.Add(() => StartExecutable(game.LaunchSpec.Value, game.InstallPath));
                    break;
                case "launcher-args":
                    attempts.Add(() => StartLauncherArgs(game.LaunchSpec.Value, game.InstallPath));
                    break;
                case "protocol":
                    attempts.Add(() => StartProtocol(game.LaunchSpec.Value));
                    break;
            }
        }

        if (game.Platform == Platform.Steam &&
            int.TryParse(game.PlatformGameId, out var appId))
        {
            var steamExe = FindSteamExecutable();
            if (steamExe is not null)
                attempts.Add(() => StartProcess(steamExe, $"-applaunch {appId}", Path.GetDirectoryName(steamExe)));
        }

        if (game.Platform == Platform.Ea
            && game.IsInstalled
            && !string.IsNullOrWhiteSpace(game.PlatformGameId))
        {
            attempts.Add(() => StartProtocol($"link2ea://launchgame/contentids/{game.PlatformGameId}"));
            attempts.Add(() => StartProtocol($"origin2://game/launch?offerIds={game.PlatformGameId}"));
        }

        if (!string.IsNullOrWhiteSpace(game.InstallPath) && Directory.Exists(game.InstallPath))
        {
            var exe = FindBestGameExecutable(game.InstallPath);
            if (exe is not null)
                attempts.Add(() => StartExecutable(exe, game.InstallPath));
        }

        return attempts;
    }

    private static string? FindBestGameExecutable(string installPath)
    {
        if (!Directory.Exists(installPath))
            return null;

        return Directory
            .GetFiles(installPath, "*.exe", SearchOption.TopDirectoryOnly)
            .Where(path => !IsUtilityExecutable(path))
            .OrderByDescending(path => new FileInfo(path).Length)
            .FirstOrDefault();
    }

    private static void StartExecutable(string value, string? workingDirectory)
    {
        var parts = value.Split('|', 2);
        var fileName = Path.GetFullPath(parts[0]);
        if (!File.Exists(fileName))
            throw new FileNotFoundException(Loc.T("ExecutableNotFound", fileName));

        var arguments = parts.Length > 1 ? parts[1] : null;
        StartProcess(fileName, arguments, workingDirectory ?? Path.GetDirectoryName(fileName), useShellExecute: false);
    }

    private static void StartLauncherArgs(string value, string? workingDirectory)
    {
        var separator = value.IndexOf('|');
        if (separator <= 0)
            throw new InvalidOperationException(Loc.T("InvalidLaunchFormat"));

        var launcher = Path.GetFullPath(value[..separator]);
        if (!File.Exists(launcher))
            throw new FileNotFoundException(Loc.T("LauncherNotFound", launcher));

        var arguments = value[(separator + 1)..];
        var hideWindow = LegendaryClient.IsLegendaryExecutable(launcher);
        StartProcess(
            launcher,
            arguments,
            workingDirectory ?? Path.GetDirectoryName(launcher),
            useShellExecute: false,
            hideWindow: hideWindow);
    }

    private static void StartProtocol(string url)
    {
        StartProcess(url, null, null, useShellExecute: true);
    }

    private static void StartProcess(
        string fileName,
        string? arguments,
        string? workingDirectory,
        bool useShellExecute = true,
        bool hideWindow = false)
    {
        if (!useShellExecute && !File.Exists(fileName))
            throw new FileNotFoundException(Loc.T("ExecutableNotFound", fileName));

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? string.Empty,
            WorkingDirectory = workingDirectory ?? string.Empty,
            UseShellExecute = useShellExecute
        };

        if (!useShellExecute && hideWindow)
        {
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
        }

        if (string.IsNullOrWhiteSpace(workingDirectory))
            psi.WorkingDirectory = string.Empty;

        if (Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("ProcessStartFailed", fileName));
    }

    private static bool IsUtilityExecutable(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        return name.Contains("unins", StringComparison.Ordinal)
               || name.Contains("install", StringComparison.Ordinal)
               || name.Contains("redist", StringComparison.Ordinal)
               || name.Contains("crash", StringComparison.Ordinal)
               || name.Contains("setup", StringComparison.Ordinal)
               || name.Contains("autorun", StringComparison.Ordinal)
               || name.Contains("uplayinstaller", StringComparison.Ordinal)
               || name.Contains("createdump", StringComparison.Ordinal)
               || name.Contains("dotnet", StringComparison.Ordinal)
               || name is "unitycrashhandler64.exe" or "unitycrashhandler32.exe";
    }

    private static string? FindSteamExecutable()
    {
        const string defaultPath = @"C:\Program Files (x86)\Steam\steam.exe";
        if (File.Exists(defaultPath))
            return defaultPath;

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
            using var steamKey = baseKey.OpenSubKey(@"Software\Valve\Steam");
            var steamPath = steamKey?.GetValue("SteamPath") as string;
            if (string.IsNullOrWhiteSpace(steamPath))
                continue;

            var candidate = Path.Combine(steamPath.Replace('/', Path.DirectorySeparatorChar), "steam.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
