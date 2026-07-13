using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Infrastructure.Database;
using OpenGameHUB.Providers.Ea;
using OpenGameHUB.Providers.Epic;
using OpenGameHUB.Providers.Rockstar;
using OpenGameHUB.Providers.Ubisoft;
using OpenGameHUB.Providers.Xbox;

namespace OpenGameHUB.Services.Games;

public sealed class GameLibraryService : IDisposable
{
    private GameDatabase _database = new();
    private MetadataService _metadataService;
    private UserCollectionService _collectionService;
    private readonly SteamWebApiService _steamWebApiService = new();
    private readonly SteamStoreClient _steamStoreClient = new();
    private readonly SettingsService _settingsService = new();
    private readonly InstalledGameScanner _installedGameScanner = new();
    private readonly SteamCloudLibraryProvider _steamCloudProvider;
    private readonly EpicCloudLibraryProvider _epicCloudProvider;
    private readonly XboxCloudLibraryProvider _xboxCloudProvider;
    private readonly IReadOnlyList<ICloudLibraryProvider> _cloudProviders;
    private readonly GameLaunchService _launchService;

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
            new RockstarCloudLibraryProvider(),
            _xboxCloudProvider
        ];
        _launchService = new GameLaunchService(_cloudProviders);
        _metadataService = new MetadataService(_database, _settingsService);
        _collectionService = new UserCollectionService(_database);
        _collectionService.Reload();
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

    public bool IsRockstarCloudAvailable =>
        _cloudProviders.Any(p => p.Platform == Platform.Rockstar && p.IsAvailable());

    public bool IsXboxCloudAvailable =>
        _cloudProviders.Any(p => p.Platform == Platform.GamePass && p.IsAvailable());

    public bool IsXboxConnected => XboxAccountClient.IsAuthenticated();

    public EaLibraryCacheStatus EaLibraryCacheStatus => EaCatalogReader.GetCacheStatus();

    public bool ShouldOfferEaLibraryPrompt => EaCatalogReader.ShouldOfferLibraryPrompt();

    public bool IsSteamCloudAvailable => _steamCloudProvider.IsAvailable();

    public bool IsSteamApiConfigured => _settingsService.Current.IsSteamApiConfigured;

    public SettingsService Settings => _settingsService;

    public MetadataService Metadata => _metadataService;

    public UserCollectionService Collections => _collectionService;

    public IReadOnlyList<UnifiedGame> LoadCachedGames()
    {
        _collectionService.Reload();
        var games = _database.GetAllGames().ToList();
        EnrichTransientCatalogCovers(games);
        _metadataService.ReconcileCachedCovers(games);
        return games;
    }

    public async Task<IReadOnlyList<UnifiedGame>> RefreshLibraryAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SteamWebApiService.SteamOwnedGameEntry> steamOwned = [];
        var failedCloudPlatforms = new HashSet<Platform>();

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
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(GameLibraryService),
                    operation: "RefreshLibraryAsync.SyncSteamApi",
                    exception: ex,
                    platform: Platform.Steam);
                failedCloudPlatforms.Add(Platform.Steam);
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
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(GameLibraryService),
                    operation: "RefreshLibraryAsync.SyncSteamLocal",
                    exception: ex,
                    platform: Platform.Steam);
                failedCloudPlatforms.Add(Platform.Steam);
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
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                AppDiagnostics.ReportError(
                    area: nameof(GameLibraryService),
                    operation: "RefreshLibraryAsync.PrepareEpicLibrary",
                    exception: ex,
                    platform: Platform.Epic,
                    details: "Timed out while ensuring Legendary bootstrap");
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
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(GameLibraryService),
                    operation: "RefreshLibraryAsync.SyncXboxLibrary",
                    exception: ex,
                    platform: Platform.GamePass);
                // optional cloud sync
            }
        }

        var existingGames = _database.GetAllGames();
        var games = await Task.Run(
            () => ScanAllGames(progress, cancellationToken, failedCloudPlatforms),
            cancellationToken);

        games = GameLibraryMerger.PreserveCatalogEntriesForFailedProviders(
            games, existingGames, failedCloudPlatforms);
        _database.SyncScannedGames(games);
        _collectionService.Reload();

        var stored = _database.GetAllGames();
        EnrichTransientCatalogCovers(stored);

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

    public void LaunchGame(UnifiedGame game)
    {
        _launchService.Launch(game);
        RecordLauncherLaunch(game);
    }

    public void RecordLauncherLaunch(UnifiedGame game)
    {
        var launchedAt = DateTime.UtcNow;
        game.LastPlayed = launchedAt;
        _database.RecordLauncherLaunch(game.Id, launchedAt);
    }

    public void ToggleFavorite(UnifiedGame game) =>
        _database.SetFavorite(game.Id, !game.IsFavorite);

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

    private List<UnifiedGame> ScanAllGames(
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        ISet<Platform> failedCloudPlatforms)
    {
        var games = _installedGameScanner.Scan(cancellationToken).ToList();
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
                else if (provider.Platform == Platform.Rockstar)
                    progress?.Report(Loc.T("SyncingRockstarLibrary"));
                else if (provider.Platform == Platform.GamePass)
                    progress?.Report(Loc.T("SyncingXboxLibrary"));

                games.AddRange(provider.GetUninstalledLibraryGames(games, cancellationToken));
            }
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(GameLibraryService),
                    operation: "ScanAllGames.GetUninstalledLibraryGames",
                    exception: ex,
                    platform: provider.Platform,
                    details: provider.GetType().Name);
                failedCloudPlatforms.Add(provider.Platform);
            }
        }

        return GameLibraryMerger.Deduplicate(games);
    }

    private static void EnrichTransientCatalogCovers(IReadOnlyList<UnifiedGame> games)
    {
        SteamWebApiService.EnrichCatalogCoverUrls(games);
        UbisoftCatalogReader.EnrichCatalogCoverUrls(games);
        XboxManifestReader.EnrichCatalogCoverUrls(games);
        EpicCatalogReader.EnrichCatalogCoverUrls(games);
        RockstarCatalogReader.EnrichCatalogCoverUrls(games);
    }

    public void ResetLocalCache()
    {
        _database.Dispose();
        DevModeService.ClearLocalLibraryCache();
        _database = new GameDatabase();
        _metadataService = new MetadataService(_database, _settingsService);
        _collectionService = new UserCollectionService(_database);
        _collectionService.Reload();
    }

    public void Dispose() => _database.Dispose();
}
