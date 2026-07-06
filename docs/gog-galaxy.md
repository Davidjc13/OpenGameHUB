# GOG Galaxy

## Why no public API

GOG does not expose a REST API for "my library" aimed at third-party desktop apps. Real options:

1. **Local SQLite database** — GOG Galaxy 2.0 stores the synced library in `galaxy-2.0.db`
2. **Web scraping** — fragile, requires browser session, not suitable for a background scan
3. **Installed games only** — GameLib detects GOG installs but cannot list owned-but-uninstalled titles

OpenGameHUB reads the **local Galaxy database** (same approach as community export tools) and delegates installation to the **official GOG Galaxy client** via protocol URLs and optional CLI arguments.

There is **no GOG account linking** in OpenGameHUB — the user must already be signed in to GOG Galaxy on the same PC so the library syncs to disk.

---

## Components

| File | Role |
|------|------|
| `Services/Gog/GogCatalogReader.cs` | Read owned games and cover URLs from `galaxy-2.0.db` |
| `Services/Gog/GogLauncherClient.cs` | Open GOG Galaxy to install a game |
| `Services/LibraryProviders/GogCloudLibraryProvider.cs` | `ICloudLibraryProvider` for uninstalled owned games |

GameLib's `LauncherManager` also detects installed GOG games; the cloud provider adds entries the user owns but has not installed.

---

## Data sources

### Library database

```
%ProgramData%\GOG.com\Galaxy\storage\galaxy-2.0.db
```

SQLite database maintained by GOG Galaxy 2.0. Updated when the user signs in and the client syncs the library.

**Why `ProductPurchaseDates`:** since Galaxy 2.0.44 the old `GameLinks` table was removed. `ProductPurchaseDates` joined with `GamePieces` is the stable source for owned titles (see [GOG-Galaxy-Export-Script](https://github.com/AB1908/GOG-Galaxy-Export-Script)).

### Launcher detection

Registry keys (same as Playnite's GOG integration):

- `HKLM\SOFTWARE\WOW6432Node\GOG.com\GalaxyClient\paths`
- `HKLM\SOFTWARE\GOG.com\GalaxyClient\paths`

Value `client` → install directory containing `GalaxyClient.exe`.

### Image webcache (optional fallback)

```
%ProgramData%\GOG.com\Galaxy\webcache\{overlay}\gog\{productId}\*.webp
```

Galaxy downloads cover art here when the user views a game in the client. Used when `originalImages` URLs are missing from the database.

---

## Cloud library: `GogCatalogReader`

### Availability

`IsCloudLibraryAvailable()` is true when:

- GOG Galaxy is installed (registry or `GalaxyClient.exe` found), **and**
- `galaxy-2.0.db` exists

If either is missing, the GOG cloud provider is skipped silently during scan.

### SQL query (simplified)

Owned GOG releases with title and optional cover metadata:

```sql
SELECT DISTINCT ppd.gameReleaseKey, titlePiece.value, imagesPiece.value
FROM ProductPurchaseDates ppd
INNER JOIN GamePieces titlePiece ON titlePiece.releaseKey = ppd.gameReleaseKey
INNER JOIN GamePieceTypes titleType
    ON titleType.id = titlePiece.gamePieceTypeId AND titleType.type = 'title'
LEFT JOIN GamePieces imagesPiece ON imagesPiece.releaseKey = ppd.gameReleaseKey
LEFT JOIN GamePieceTypes imagesType
    ON imagesType.id = imagesPiece.gamePieceTypeId AND imagesType.type = 'originalImages'
WHERE ppd.gameReleaseKey GLOB 'gog_*';
```

### Filtering

| Filter | How |
|--------|-----|
| Non-GOG platforms | `releaseKey GLOB 'gog_*'` (excludes Steam/Epic imports in Galaxy) |
| Hidden games | `UserReleaseProperties.isHidden = 1` |
| DLCs | Release keys listed in any game's `dlcs` game piece |
| Duplicate product IDs | Keep first entry per numeric product ID |

### Title and product ID

- **Title** — JSON in `GamePieces` (`{"title":"Game Name"}`), game piece type `title`
- **Product ID** — numeric ID extracted from `releaseKey` (`gog_{productId}` or `gog_{productId}_{suffix}`)
- **Release key** — full Galaxy identifier, e.g. `gog_1207659693`

### Stable IDs

| Entry type | ID pattern |
|------------|------------|
| Cloud (not installed) | `gog:catalog:{releaseKey}` |
| Installed (GameLib) | `gog:path:{hash}` or GameLib id |

`PlatformGameId` is always the numeric GOG product ID (matches `goggame-{id}.info` in install folders).

### Matching installed vs catalog

`MatchesInstalledGame` compares:

1. `PlatformGameId` (numeric), or
2. Normalized title (`MetadataSearchHelper`)

---

## Covers

### Source 1: `originalImages` in database

Game piece type `originalImages` contains JSON with CDN URLs on `images.gog.com`:

| JSON field | Use |
|------------|-----|
| `verticalCover` | Preferred (poster / library tile) |
| `squareIcon` | Fallback |
| `background` | Last resort |

Example URL shape:

```
https://images.gog.com/{hash}_glx_vertical_cover.webp?namespace=gamesdb
```

### Source 2: Local webcache

`FindWebcacheCover(productId)` searches:

```
%ProgramData%\GOG.com\Galaxy\webcache\*\gog\{productId}\*vertical_cover*
```

Then `*square_icon*` if no vertical cover file exists. Files must be ≥ 1 KB (skip empty/corrupt downloads).

### Enrichment pipeline

After each library refresh (`GameLibraryService.RefreshLibraryAsync`):

```
GogCatalogReader.EnrichCatalogCoverUrls(stored)
```

Sets `CatalogCoverUrl` on GOG games without a cached `CoverPath` — same pattern as Steam and Ubisoft.

`MetadataService` then:

1. Copies local webcache file if `CatalogCoverUrl` is a path, or
2. Downloads remote CDN URL via `SafeImageDownloader`

See [metadata-and-covers.md](metadata-and-covers.md).

**Tip:** if covers are missing, open the game once in GOG Galaxy so it syncs metadata and downloads images to webcache.

---

## Installation

### User flow

```
MainWindowViewModel.LaunchSelectedGame
│
├─ GOG special case (not installed):
│   Platform == Gog
│   + LaunchSpec.Kind == "protocol"
│   + GOG Galaxy installed
│   → GogLauncherClient.StartInstall(releaseKey, productId)
│   → Status: "Opening GOG Galaxy to install {title}..."
│   → App does NOT wait for download
│
└─ Otherwise → GameLibraryService.LaunchGame
     └─ GogCloudLibraryProvider.GetInstallLaunchAttempts (fallback chain)
```

### `GogLauncherClient.StartInstall`

| Condition | Action |
|-----------|--------|
| Galaxy running (`GOG Galaxy Notifications Renderer` process) | `GalaxyClient.exe /gameId={id} /command=installGame` |
| Otherwise | `goggalaxy://openGameView/{releaseKey}` protocol |

Fallback in `GetInstallLaunchAttempts`:

- Protocol URL via `GalaxyClient.exe` with protocol as argument

**Why protocol as primary when Galaxy is closed:** opens the game page where the user can install; the `installGame` command only works reliably when Galaxy core is initialized.

### `LaunchSpec` for cloud entries

```
protocol → goggalaxy://openGameView/{releaseKey}
```

---

## `GogCloudLibraryProvider`

File: `Services/LibraryProviders/GogCloudLibraryProvider.cs`

| | |
|--|--|
| **Available if** | `GogCatalogReader.IsCloudLibraryAvailable()` |
| **Source** | `GogCatalogReader.ReadLibraryEntries()` |
| **Filtering** | Skips titles already installed (by product ID or title) |
| **LaunchSpec** | `goggalaxy://openGameView/{releaseKey}` |
| **CatalogCoverUrl** | From `GogCatalogEntry.CoverUrl` (CDN or webcache path) |
| **Install attempts** | `GogLauncherClient`, then Galaxy exe with protocol |

Registered in `GameLibraryService` constructor alongside Steam, Epic, Ubisoft, and EA providers.

Progress message during scan: `SyncingGogLibrary` (`Strings.resx` / `Strings.es.resx`).

---

## UI hints

After refresh, if the GOG cloud provider is available, the status bar appends:

- EN: ` · GOG library via GOG Galaxy`
- ES: ` · Biblioteca GOG vía GOG Galaxy`

Property: `MainWindowViewModel.IsGogCloudAvailable` → `GameLibraryService.IsGogCloudAvailable`.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| No uninstalled GOG games | Galaxy not signed in or DB empty | Open GOG Galaxy, sign in, wait for library sync |
| `GogCacheMissing` during dev | `galaxy-2.0.db` not present | Install Galaxy, sign in at least once |
| Install opens Galaxy but no download | Galaxy not fully started | Wait for Galaxy to finish loading, try again |
| Missing covers | No `originalImages` in DB, webcache empty | Open game in Galaxy; refresh library in OpenGameHUB |
| Grey/broken cover images | Known GOG CDN HTTPS issues on some images | Webcache fallback; MetadataService may try other sources |

---

## References

- [GOG-Galaxy-Export-Script](https://github.com/AB1908/GOG-Galaxy-Export-Script) — SQL patterns for `galaxy-2.0.db`
- [Playnite GogLibrary](https://github.com/JosefNemec/PlayniteExtensions/tree/master/source/Libraries/GogLibrary) — install protocol and registry paths
- [GameLauncherResearch — GoG: Installing games](https://github.com/Lariaa/GameLauncherResearch/wiki/GoG-:-Installing-games) — depot system (not used by OpenGameHUB)

---

## Related docs

- [cloud-providers.md](cloud-providers.md) — `ICloudLibraryProvider` pattern
- [platform-integrations.md](platform-integrations.md) — summary table by platform
- [metadata-and-covers.md](metadata-and-covers.md) — cover download pipeline
