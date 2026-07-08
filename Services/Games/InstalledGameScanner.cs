using GameLib;
using GameLib.Core;
using GameLib.Plugin.Steam.Model;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Services.Covers;

namespace OpenGameHUB.Services.Games;

internal sealed class InstalledGameScanner
{
    private readonly LauncherManager _launcherManager = new(new LauncherOptions
    {
        QueryOnlineData = false,
        LoadLocalCatalogData = true,
        SearchExecutables = true
    });

    public IReadOnlyList<UnifiedGame> Scan(CancellationToken cancellationToken)
    {
        _launcherManager.Refresh(cancellationToken);

        var results = new List<UnifiedGame>();
        foreach (var launcher in _launcherManager.GetLaunchers())
        {
            if (!launcher.IsInstalled)
                continue;

            var platform = MapPlatform(launcher.Name);
            foreach (var game in launcher.Games)
            {
                var unified = MapGame(platform, launcher, game);
                if (unified is not null && !GameEntryFilter.IsExcluded(unified))
                    results.Add(unified);
            }
        }

        return results
            .Where(game => !IsEpicWrapperForNativeLauncher(game, results))
            .ToList();
    }

    private static bool IsEpicWrapperForNativeLauncher(UnifiedGame game, IReadOnlyList<UnifiedGame> allGames)
    {
        if (game.Platform != Platform.Epic)
            return false;

        var titleKey = MetadataSearchHelper.NormalizeTitle(game.Title).ToLowerInvariant();
        return allGames.Any(other =>
            other.Platform is Platform.Riot or Platform.Ubisoft or Platform.Ea or Platform.BattleNet or Platform.Rockstar
            && MetadataSearchHelper.NormalizeTitle(other.Title).ToLowerInvariant() == titleKey);
    }

    private static string BuildStableId(Platform platform, IGame game)
    {
        var normalizedPath = GameLibraryMerger.NormalizeInstallPath(game.InstallDir);
        if (!string.IsNullOrEmpty(normalizedPath))
        {
            var slug = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(normalizedPath)))[..16]
                .ToLowerInvariant();
            return $"{platform.ToString().ToLowerInvariant()}:path:{slug}";
        }

        if (!string.IsNullOrWhiteSpace(game.Id))
            return $"{platform.ToString().ToLowerInvariant()}:{game.Id}";

        var titleSlug = string.Concat(game.Name.Where(char.IsLetterOrDigit)).ToLowerInvariant();
        return $"{platform.ToString().ToLowerInvariant()}:title:{titleSlug}";
    }

    private static UnifiedGame? MapGame(Platform platform, ILauncher launcher, IGame game)
    {
        if (string.IsNullOrWhiteSpace(game.Name))
            return null;

        try
        {
            var executable = ResolveExecutable(game);
            var launchSpec = BuildLaunchSpec(platform, launcher, game, executable);
            var isInstalled = !string.IsNullOrWhiteSpace(game.InstallDir) && Directory.Exists(game.InstallDir);

            return new UnifiedGame
            {
                Id = BuildStableId(platform, game),
                Platform = platform,
                PlatformGameId = game.Id,
                Title = game.Name,
                IsInstalled = isInstalled,
                InstallPath = isInstalled ? game.InstallDir : null,
                PlaytimeMinutes = 0,
                LaunchSpec = launchSpec
            };
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(InstalledGameScanner),
                operation: "MapGame",
                exception: ex,
                platform: platform,
                details: game.Name);
            return null;
        }
    }

    private static LaunchSpec BuildLaunchSpec(
        Platform platform,
        ILauncher launcher,
        IGame game,
        string? executable)
    {
        if (platform == Platform.Steam && game is SteamGame steamGame && !string.IsNullOrWhiteSpace(steamGame.Id))
        {
            var steamExe = ResolveLauncherExecutable(launcher, "steam.exe");
            if (steamExe is not null)
                return LaunchSpec.LauncherArgs(steamExe, $"-applaunch {steamGame.Id}");
        }

        if (platform == Platform.Ea && !string.IsNullOrWhiteSpace(game.LaunchString))
        {
            var launchString = game.LaunchString.Trim();
            if (launchString.Contains("://", StringComparison.Ordinal))
                return LaunchSpec.Protocol(launchString);

            var eaLauncher = ResolveLauncherExecutable(launcher, "EADesktop.exe")
                ?? ResolveLauncherExecutable(launcher, "EALauncher.exe");
            if (eaLauncher is not null)
                return LaunchSpec.LauncherArgs(eaLauncher, launchString);
        }

        if (!string.IsNullOrWhiteSpace(game.LaunchString))
        {
            var launchString = game.LaunchString.Trim();

            if (launchString.Contains("://", StringComparison.Ordinal))
                return LaunchSpec.Protocol(launchString);

            if (launchString.StartsWith('-') || launchString.StartsWith("--", StringComparison.Ordinal))
            {
                var launcherExe = ResolveLauncherExecutable(launcher);
                if (launcherExe is not null)
                    return LaunchSpec.LauncherArgs(launcherExe, launchString);
            }

            var launchExe = launchString.Split(' ')[0];
            if (File.Exists(launchExe))
                return LaunchSpec.Executable(launchString);
        }

        if (!string.IsNullOrWhiteSpace(executable))
            return LaunchSpec.Executable(executable);

        if (!string.IsNullOrWhiteSpace(game.Executable) && File.Exists(game.Executable))
            return LaunchSpec.Executable(game.Executable);

        throw new InvalidOperationException(Loc.T("CannotDetermineLaunch", game.Name));
    }

    private static string? ResolveExecutable(IGame game)
    {
        if (!string.IsNullOrWhiteSpace(game.Executable) && File.Exists(game.Executable))
            return game.Executable;

        foreach (var exe in game.Executables)
        {
            if (File.Exists(exe) && !IsUtilityExecutable(exe))
                return exe;
        }

        if (string.IsNullOrWhiteSpace(game.InstallDir) || !Directory.Exists(game.InstallDir))
            return null;

        return FindBestGameExecutable(game.InstallDir);
    }

    private static string? ResolveLauncherExecutable(ILauncher launcher, string? preferredName = null)
    {
        if (!string.IsNullOrWhiteSpace(launcher.Executable) && File.Exists(launcher.Executable))
            return launcher.Executable;

        if (!string.IsNullOrWhiteSpace(preferredName) &&
            !string.IsNullOrWhiteSpace(launcher.InstallDir))
        {
            var candidate = Path.Combine(launcher.InstallDir, preferredName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
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

    private static Platform MapPlatform(string launcherName) => launcherName.ToLowerInvariant() switch
    {
        "steam" => Platform.Steam,
        "epic games" or "epic" => Platform.Epic,
        "gog galaxy 2.0" or "gog galaxy" or "gog" => Platform.Gog,
        "ubisoft connect" or "ubisoft" => Platform.Ubisoft,
        "origin" or "ea app" or "ea" => Platform.Ea,
        "battle.net" or "battlenet" => Platform.BattleNet,
        "rockstar" => Platform.Rockstar,
        "riot games" or "riot" => Platform.Riot,
        _ => Platform.Unknown
    };
}
