# Cloud library providers

## Interface

File: `Services/LibraryProviders/ICloudLibraryProvider.cs`

```csharp
public interface ICloudLibraryProvider
{
    Platform Platform { get; }
    bool IsAvailable();
    IReadOnlyList<UnifiedGame> GetUninstalledLibraryGames(
        IReadOnlyList<UnifiedGame> currentGames, CancellationToken cancellationToken = default);
    IEnumerable<Action> GetInstallLaunchAttempts(UnifiedGame game);
}
```

### Responsibilities

| Method | What it does |
|--------|--------------|
| `IsAvailable()` | Can we try this integration now? (credentials, cache, legendary, etc.) |
| `GetUninstalledLibraryGames` | Adds **not installed** entries the user owns on that store |
| `GetInstallLaunchAttempts` | Sequence of actions to install a specific game (fallbacks) |

**Why `Action` and not async:** launch is synchronous (`Process.Start`); the attempt chain in `LaunchGame` runs until the first success.

## Registration in `GameLibraryService`

In the constructor:

```csharp
_cloudProviders = [
    _steamCloudProvider,
    _epicCloudProvider,
    new UbisoftCloudLibraryProvider(),
    new EaCloudLibraryProvider(),
    new GogCloudLibraryProvider()
];
```

Iteration order in `ScanAllGames`: Steam → Epic → Ubisoft → EA → GOG (each if `IsAvailable()`).

Errors: empty `try/catch` per provider — **silent failure** so the full scan is not broken.

---

## `SteamCloudLibraryProvider`

File: `Services/LibraryProviders/SteamCloudLibraryProvider.cs`

| | |
|--|--|
| **Available if** | Steam API configured **or** Steam installed |
| **Data source** | Pre-loaded list in `SetOwnedGames` from `RefreshLibraryAsync` |
| **Filtering** | Skips AppIds already present as installed |
| **LaunchSpec** | `protocol` → `steam://install/{appId}` |
| **Install attempts** | `steam://install`, Steam launcher with protocol |

The owned list is prepared **before** scanning in `GameLibraryService.RefreshLibraryAsync`, not inside the provider.

---

## `EpicCloudLibraryProvider`

File: `Services/LibraryProviders/EpicCloudLibraryProvider.cs`

| | |
|--|--|
| **Available if** | `LegendaryClient.IsAvailable()` |
| **Source** | `legendary list --json` → `LegendaryCatalogEntry` |
| **Filtering** | By `app_name` and normalized title vs installed |
| **LaunchSpec** | `protocol` with URL `com.epicgames.launcher://apps/{namespace}%3A{catalogId}%3A{appName}?action=install` |
| **Install attempts** | `legendary install`, protocol, Epic Launcher exe, `legendary launch` |

**Decision:** protocol-type `LaunchSpec` so `MainWindowViewModel` uses `EpicLauncherClient` (primary path). `GetInstallLaunchAttempts` is backup if `LaunchGame` is called directly.

---

## `UbisoftCloudLibraryProvider`

File: `Services/LibraryProviders/UbisoftCloudLibraryProvider.cs`

| | |
|--|--|
| **Available if** | Ubisoft Connect installed (GameLib) **and** configuration cache file exists |
| **Source** | `UbisoftCatalogReader.ReadCatalog()` |
| **LaunchSpec** | `uplay://install/{uplayId}` |

No real-time remote API — only what the launcher already synced to disk.

---

## `EaCloudLibraryProvider`

File: `Services/LibraryProviders/EaCloudLibraryProvider.cs`

| | |
|--|--|
| **Available if** | `EaCatalogReader` returns entries (decrypted cache or logs) |
| **Source** | `EaCatalogReader.ReadLibraryEntries()` |
| **LaunchSpec** | `link2ea://` / EA catalog install URLs |
| **Install attempts** | EA Desktop, origin/link2ea protocols |

Before reading catalog, `ScanAllGames` calls `EaCatalogReader.InvalidateCache()` to force re-read. See [ea-desktop.md](ea-desktop.md) for full EA flow.

---

## `GogCloudLibraryProvider`

File: `Services/LibraryProviders/GogCloudLibraryProvider.cs`

| | |
|--|--|
| **Available if** | GOG Galaxy installed **and** `galaxy-2.0.db` exists |
| **Source** | `GogCatalogReader.ReadLibraryEntries()` — SQLite `ProductPurchaseDates` + `GamePieces` |
| **Filtering** | `gog_*` releases only; skips hidden, DLC, and already-installed (by product ID or title) |
| **LaunchSpec** | `goggalaxy://openGameView/{releaseKey}` |
| **CatalogCoverUrl** | `originalImages` CDN URLs or local webcache path |
| **Install attempts** | `GogLauncherClient.StartInstall`, Galaxy exe with protocol |

No real-time remote API — only what GOG Galaxy already synced to `%ProgramData%`.

See [gog-galaxy.md](gog-galaxy.md) for database schema, cover pipeline, and install flow.

---

## Extending with a new platform

1. Implement `ICloudLibraryProvider`
2. Register in `GameLibraryService` ctor
3. Add `Platform` if needed
4. Document data source (API vs cache) in [platform-integrations.md](platform-integrations.md)
5. Add localization strings if there is platform-specific UI

No need to touch `GameDatabase` if `UnifiedGame` + `LaunchSpec` are sufficient.
