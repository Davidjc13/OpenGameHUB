using System.Diagnostics;
using Microsoft.Win32;
using OpenGameHUB.Data;
using OpenGameHUB.Models;
using OpenGameHUB.Services.Ea;
using OpenGameHUB.Services.Epic;
using OpenGameHUB.Services.LibraryProviders;
using OpenGameHUB.Services.Ubisoft;
using OpenGameHUB.Services.Xbox;
using GameLib;
using GameLib.Core;
using GameLib.Plugin.Steam.Model;

namespace OpenGameHUB.Services;

public sealed class GameLibraryService : IDisposable
{
    private GameDatabase _database = new();
    private MetadataService _metadataService;
    private readonly SteamWebApiService _steamWebApiService = new();
    private readonly SteamStoreClient _steamStoreClient = new();
    private readonly SettingsService _settingsService = new();
    private readonly LauncherManager _launcherManager = new(new LauncherOptions
    {
        QueryOnlineData = false,
        LoadLocalCatalogData = true,
        SearchExecutables = true
    });
    private readonly SteamCloudLibraryProvider _steamCloudProvider;
    private readonly EpicCloudLibraryProvider _epicCloudProvider;
    private readonly XboxCloudLibraryProvider _xboxCloudProvider;
    private readonly IReadOnlyList<ICloudLibraryProvider> _cloudProviders;

    public GameLibraryService()
    {
        _steamCloudProvider = new SteamCloudLibraryProvider(_settingsService, _steamWebApiService);
        _epicCloudProvider = new EpicCloudLibraryProvider();
        _xboxCloudProvider = new XboxCloudLibraryProvider();
        _cloudProviders =
        [
            _steamCloudProvider,
            _epicCloudProvider,
            new UbisoftCloudLibraryProvider(),
            new EaCloudLibraryProvider(),
            new RiotCloudLibraryProvider(),
            new GogCloudLibraryProvider(),
            _xboxCloudProvider
        ];
        _metadataService = new MetadataService(_database, _settingsService);
    }

    public bool IsEpicCloudAvailable => _epicCloudProvider.IsAvailable();

    public bool IsEpicConnected =>
        LegendaryClient.HasStoredCredentials()
        || _settingsService.Current.HasEpicAuth;

    public bool ShouldOfferLegendaryPrompt =>
        LegendaryClient.IsEpicLauncherInstalled()
        && LegendaryClient.IsAvailable()
        && !IsEpicConnected
        && !_settingsService.Current.DismissLegendaryPrompt;

    public bool IsUbisoftCloudAvailable =>
        _cloudProviders.Any(p => p.Platform == Platform.Ubisoft && p.IsAvailable());

    public bool IsEaCloudAvailable =>
        _cloudProviders.Any(p => p.Platform == Platform.Ea && p.IsAvailable());

    public bool IsRiotCloudAvailable =>
        _cloudProviders.Any(p => p.Platform == Platform.Riot && p.IsAvailable());

    public bool IsGogCloudAvailable =>
        _cloudProviders.Any(p => p.Platform == Platform.Gog && p.IsAvailable());

    public bool IsXboxCloudAvailable =>
        _cloudProviders.Any(p => p.Platform == Platform.GamePass && p.IsAvailable());

    public bool IsXboxConnected => XboxAccountClient.IsAuthenticated();

    public EaLibraryCacheStatus EaLibraryCacheStatus => EaCatalogReader.GetCacheStatus();

    public bool ShouldOfferEaLibraryPrompt => EaCatalogReader.ShouldOfferLibraryPrompt();

    public bool IsSteamCloudAvailable => _steamCloudProvider.IsAvailable();

    public bool IsSteamApiConfigured => _settingsService.Current.IsSteamApiConfigured;

    public SettingsService Settings => _settingsService;

    public IReadOnlyList<UnifiedGame> LoadCachedGames()
    {
        var games = _database.GetAllGames().ToList();
        _metadataService.ReconcileCachedCovers(games);
        return games;
    }

    public async Task<IReadOnlyList<UnifiedGame>> RefreshLibraryAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SteamWebApiService.SteamOwnedGameEntry> steamOwned = [];

        if (_settingsService.Current.IsSteamApiConfigured)
        {
            progress?.Report(Loc.T("SyncingSteamLibrary"));
            try
            {
                steamOwned = await _steamWebApiService.GetOwnedGamesAsync(
                    _settingsService.Current.SteamApiKey,
                    _settingsService.Current.SteamId,
                    cancellationToken);
                _steamCloudProvider.SetOwnedGames(steamOwned);
            }
            catch
            {
                _steamCloudProvider.ClearOwnedGames();
            }
        }
        else if (SteamLocalAccountReader.IsSteamInstalled)
        {
            progress?.Report(Loc.T("SyncingSteamLocalLibrary"));
            try
            {
                steamOwned = await LoadLocalSteamLibraryAsync(cancellationToken);
                _steamCloudProvider.SetOwnedGames(steamOwned);
            }
            catch
            {
                _steamCloudProvider.ClearOwnedGames();
            }
        }
        else
        {
            _steamCloudProvider.ClearOwnedGames();
        }

        if (LegendaryClient.IsEpicLauncherInstalled())
        {
            progress?.Report(Loc.T("PreparingEpicLibrary"));
            using var downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            downloadCts.CancelAfter(TimeSpan.FromSeconds(90));
            try
            {
                await LegendaryBootstrap.EnsureInstalledAsync(progress, downloadCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Epic helper download timed out; continue without cloud Epic.
            }

            LegendaryClient.InvalidateExecutableCache();
            EpicAuthHelper.PersistFromLegendary(_settingsService);
        }

        if (XboxAccountClient.IsAuthenticated())
        {
            progress?.Report(Loc.T("SyncingXboxLibrary"));
            try
            {
                await _xboxCloudProvider.LoadLibraryAsync(cancellationToken);
                var gamertag = await new XboxAccountClient().GetGamertagAsync(cancellationToken);
                XboxAuthHelper.PersistProfile(_settingsService, gamertag);
            }
            catch
            {
                // optional cloud sync
            }
        }

        var games = await Task.Run(
            () => ScanAllGames(progress, cancellationToken),
            cancellationToken);

        _database.SyncScannedGames(games);

        var stored = _database.GetAllGames();
        SteamWebApiService.EnrichCatalogCoverUrls(stored);
        UbisoftCatalogReader.EnrichCatalogCoverUrls(stored);
        XboxManifestReader.EnrichCatalogCoverUrls(stored);

        if (steamOwned.Count > 0)
        {
            progress?.Report(Loc.T("SyncingSteamPlaytime"));
            _steamWebApiService.EnrichPlaytimeFromOwned(stored, steamOwned);
            _database.PersistPlaytimes(stored);
        }

        _metadataService.ReconcileCachedCovers(stored);
        return _database.GetAllGames();
    }

    public async Task EnrichCoversAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default,
        int maxCovers = 32)
    {
        var stored = _database.GetAllGames();
        await _metadataService.EnrichCoversAsync(stored, progress, cancellationToken, maxCovers);
    }

    public bool TrySetCustomCover(UnifiedGame game, string sourceImagePath) =>
        _metadataService.TrySetCustomCover(game, sourceImagePath);

    public Task<string?> TryResetCustomCoverAsync(
        UnifiedGame game,
        CancellationToken cancellationToken = default) =>
        _metadataService.TryResetCustomCoverAsync(game, cancellationToken);

    private async Task<IReadOnlyList<SteamWebApiService.SteamOwnedGameEntry>> LoadLocalSteamLibraryAsync(
        CancellationToken cancellationToken)
    {
        var localApps = await Task.Run(SteamLocalLibraryReader.ReadOwnedApps, cancellationToken);
        if (localApps.Count == 0)
            return [];

        var names = await _steamStoreClient.GetAppNamesAsync(
            localApps.Select(app => app.AppId),
            cancellationToken);

        return localApps
            .Select(app => new SteamWebApiService.SteamOwnedGameEntry(
                app.AppId,
                names.TryGetValue(app.AppId, out var name)
                    ? name
                    : $"Steam App {app.AppId}",
                app.PlaytimeMinutes,
                app.LastPlayed))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .ToList();
    }

    public void LaunchGame(UnifiedGame game)
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

    public void ToggleFavorite(UnifiedGame game)
    {
        _database.SetFavorite(game.Id, !game.IsFavorite);
    }

    private IReadOnlyList<UnifiedGame> ScanInstalledGames(CancellationToken cancellationToken)
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
            other.Platform is Platform.Riot or Platform.Ubisoft or Platform.Ea or Platform.BattleNet
            && MetadataSearchHelper.NormalizeTitle(other.Title).ToLowerInvariant() == titleKey);
    }

    private List<UnifiedGame> ScanAllGames(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var games = ScanInstalledGames(cancellationToken).ToList();
        games.AddRange(EaDesktopScanner.Scan());
        games.AddRange(GogDesktopScanner.Scan());
        games.AddRange(XboxGamePassScanner.Scan());
        games.AddRange(EpicManifestScanner.ScanInstalled());

        foreach (var provider in _cloudProviders)
        {
            if (!provider.IsAvailable())
                continue;

            try
            {
                if (provider.Platform == Platform.Ea)
                    EaCatalogReader.InvalidateCache();

                if (provider.Platform == Platform.Ubisoft)
                    progress?.Report(Loc.T("SyncingUbisoftLibrary"));
                else if (provider.Platform == Platform.Ea)
                    progress?.Report(Loc.T("SyncingEaLibrary"));
                else if (provider.Platform == Platform.Epic)
                    progress?.Report(Loc.T("SyncingEpicLibrary"));
                else if (provider.Platform == Platform.Riot)
                    progress?.Report(Loc.T("SyncingRiotLibrary"));
                else if (provider.Platform == Platform.Gog)
                    progress?.Report(Loc.T("SyncingGogLibrary"));
                else if (provider.Platform == Platform.GamePass)
                    progress?.Report(Loc.T("SyncingXboxLibrary"));

                games.AddRange(provider.GetUninstalledLibraryGames(games, cancellationToken));
            }
            catch
            {
                // cloud library providers are optional
            }
        }

        return DeduplicateGames(games).ToList();
    }

    private static IReadOnlyList<UnifiedGame> DeduplicateGames(List<UnifiedGame> games)
    {
        var pathMerged = new List<UnifiedGame>();
        var withoutPath = new List<UnifiedGame>();

        foreach (var group in games.GroupBy(g => NormalizeInstallPath(g.InstallPath) ?? string.Empty))
        {
            if (string.IsNullOrEmpty(group.Key))
                withoutPath.AddRange(group);
            else
                pathMerged.Add(PickPreferredDuplicate(group));
        }

        return pathMerged
            .Concat(withoutPath)
            .GroupBy(GetDedupKey, StringComparer.OrdinalIgnoreCase)
            .Select(PickPreferredDuplicate)
            .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetDedupKey(UnifiedGame game)
    {
        var installPath = NormalizeInstallPath(game.InstallPath);
        if (!string.IsNullOrEmpty(installPath))
            return installPath;

        if (game.Id.StartsWith("ea:catalog:", StringComparison.OrdinalIgnoreCase)
            || game.Id.StartsWith("ubisoft:catalog:", StringComparison.OrdinalIgnoreCase)
            || game.Id.StartsWith("gog:catalog:", StringComparison.OrdinalIgnoreCase)
            || game.Id.StartsWith("steam:", StringComparison.OrdinalIgnoreCase)
            || game.Id.StartsWith("epic:legendary:", StringComparison.OrdinalIgnoreCase)
            || game.Id.StartsWith("epic:manifest:", StringComparison.OrdinalIgnoreCase)
            || game.Id.StartsWith("gamepass:catalog:", StringComparison.OrdinalIgnoreCase))
        {
            return game.Id.ToLowerInvariant();
        }

        return NormalizeTitleKey(game.Title);
    }

    private static UnifiedGame PickPreferredDuplicate(IEnumerable<UnifiedGame> group) =>
        group
            .OrderByDescending(g => g.IsInstalled)
            .ThenByDescending(g => GetPlatformPriority(g.Platform))
            .ThenBy(g => g.Id, StringComparer.Ordinal)
            .First();

    private static int GetPlatformPriority(Platform platform) => platform switch
    {
        Platform.Riot => 100,
        Platform.Steam => 95,
        Platform.Ubisoft => 90,
        Platform.Ea => 88,
        Platform.Gog => 80,
        Platform.BattleNet => 75,
        Platform.Rockstar => 70,
        Platform.GamePass => 68,
        Platform.Epic => 60,
        _ => 0
    };

    private static string NormalizeTitleKey(string title) =>
        MetadataSearchHelper.NormalizeTitle(title).ToLowerInvariant();

    private static string? NormalizeInstallPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return null;

        try
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToLowerInvariant();
        }
        catch
        {
            return path.ToLowerInvariant();
        }
    }

    private static string BuildStableId(Platform platform, IGame game)
    {
        var normalizedPath = NormalizeInstallPath(game.InstallDir);
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
        catch
        {
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

        if (game.Platform == Platform.Ea && !string.IsNullOrWhiteSpace(game.PlatformGameId))
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

    public void ResetLocalCache()
    {
        _database.Dispose();
        DevModeService.ClearLocalLibraryCache();
        _database = new GameDatabase();
        _metadataService = new MetadataService(_database, _settingsService);
    }

    public void Dispose() => _database.Dispose();
}
