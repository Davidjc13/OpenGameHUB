# Metadata and covers

## Goal

Show covers in the library grid, list view, and detail panel without requiring the user to configure external APIs. Users can also **replace any cover** with a local image; that choice survives library refreshes.

---

## Storage layout

| Path | Role |
|------|------|
| `%LocalAppData%\OpenGameHUB\covers\{sanitizedGameId}.jpg` | Cached cover file (downloaded or user-uploaded) |
| `library.db` → `cover_path` | Last known path to the cover file |
| `library.db` → `custom_cover` | `1` if the user chose a custom image (blocks auto-download overwrite) |

`CoverPathHelper` (`Services/CoverPathHelper.cs`) resolves paths:

- `GetCachePath(gameId)` — deterministic filename from `UnifiedGame.Id`
- `ResolveExistingPath(game)` — prefers persisted `CoverPath`, falls back to cache file if it exists and passes validation

**Why one file per game ID:** simple upsert on refresh; no orphan cleanup beyond explicit reset.

---

## Orchestrator: `MetadataService`

File: `Services/MetadataService.cs`

### `ReconcileCachedCovers`

After loading or refreshing the library:

- For each game, if a file already exists in `covers/` or the install folder, update `CoverPath` in memory/DB
- Does not download anything — only aligns existing paths

### `EnrichCoversAsync`

Downloads missing covers (configurable limit, e.g. 32 in background from `MainWindowViewModel`):

```
For each game without valid CoverPath AND NOT HasCustomCover:
  1. LocalCoverScanner — search .jpg/.png in InstallPath
  2. If CatalogCoverUrl (Steam/Ubisoft CDN) → download
  3. If known Steam AppId → Steam CDN
  4. IgdbClient (if credentials)
  5. SteamGridDbClient (if API key)
  6. SteamStoreSearchClient — search Steam store by title
  7. WikipediaCoverClient
  8. RiotCoverClient — fixed IGDB image IDs for LoL, Valorant, etc.
```

Saves to `%LocalAppData%\OpenGameHUB\covers\` via `CoverPathHelper`.

### `EnsureCoverAsync`

Used when the UI loads a single game's cover (`GameItemViewModel.LoadCoverAsync`):

1. If `HasCustomCover` → only register existing file on disk; **never** auto-download
2. Else if file already exists → register and return
3. Else → run the download cascade once

### Custom covers

| Method | What it does |
|--------|--------------|
| `TrySetCustomCover(game, sourceImagePath)` | Resize/copy image to cache, set `CoverPath`, set `HasCustomCover = true` in DB |
| `TryResetCustomCoverAsync(game)` | Clear flag, delete cache file, re-run download cascade |

Flow when the user picks an image (`MainWindowViewModel.ChangeCustomCoverAsync`):

```
File picker (image/*)
  → GameLibraryService.TrySetCustomCover
       → CoverImageProcessor.TryResizeToCacheFile
       → MetadataService updates CoverPath + custom_cover in SQLite
  → GameItemViewModel.ApplyCoverFromPathAsync (reload bitmap)
  → ApplyVisibleCovers()
```

Reset (`ResetCustomCoverAsync`):

```
ReleaseCover() on selected item
  → TryResetCustomCoverAsync (delete custom file, HasCustomCover = false)
  → DownloadCoverAsync (automatic sources again)
  → ApplyVisibleCovers()
```

**Why `HasCustomCover` in DB:** `SyncScannedGames` re-upserts games from scan; the flag is preserved per `id` so a refresh does not wipe a user choice or trigger re-download over their image.

`GameDatabase.SyncScannedGames` reads existing `custom_cover` values before upsert and reapplies them to incoming scan rows.

---

## Image processing: `CoverImageProcessor`

File: `Services/CoverImageProcessor.cs`

When the user uploads a cover:

| Constraint | Value |
|------------|-------|
| Max width | 600 px |
| Max height | 900 px |
| Output format | JPEG, quality 85% |
| Upscale | Never — images smaller than max keep original dimensions |
| Fallback | If resize fails, copy source as-is if it passes `SafeImageValidator` |

Uses `System.Drawing.Common` (Windows-only build). Same validator as remote downloads.

**Why resize:** grid/list thumbnails do not need 4K posters; smaller files reduce RAM and disk use across large libraries.

---

## External clients

| Client | API | Credentials |
|--------|-----|-------------|
| `IgdbClient` | Twitch OAuth → `api.igdb.com` | Client ID + Secret (Settings) |
| `SteamGridDbClient` | `steamgriddb.com` | API key (Settings) |
| `WikipediaCoverClient` | Wikipedia REST | None |
| `SteamStoreSearchClient` | Steam store search | None |
| `RiotCoverClient` | Static IGDB image IDs | None |

Steam CDN, Ubisoft CDN, and Xbox manifest logos do not go through `MetadataService` the same way: they are used as `CatalogCoverUrl` during scanning (`SteamWebApiService.EnrichCatalogCoverUrls`, `UbisoftCatalogReader.EnrichCatalogCoverUrls`, `XboxManifestReader.EnrichCatalogCoverUrls`). Local Xbox logo paths are copied to the cover cache in `MetadataService.DownloadCoverAsync`.

See [riot.md](riot.md) for how `RiotCoverClient` maps product slugs to cover URLs.

---

## Download safety

- `SafeImageDownloader` + `SafeImageValidator` — validates image type/size before saving
- `HttpClient` with User-Agent `OpenGameHUB/1.0` and 8 s timeout

**Why validate:** URLs come from third-party searches and APIs; avoid saving HTML or huge files as "covers".

---

## `MetadataSearchHelper`

Title normalization for search and deduplication:

- Strips suffixes like ` - Windows`, `(PC)`, GOTY editions, etc.
- Used when comparing Epic vs Riot/Ubisoft and when matching favorites by title

---

## UI layer

### Settings

| Setting | Field | Effect |
|---------|-------|--------|
| Show grid covers | `AppSettings.ShowGridCovers` | If `false`, grid/list do not load bitmaps (saves RAM); detail panel can still load on selection |
| Library layout | `AppSettings.LibraryViewMode` | `Grid` or `List` — persisted across sessions |

### Main window commands

| Command | Binding | Action |
|---------|---------|--------|
| `ChangeCustomCoverCommand` | Detail panel | File picker → `TrySetCustomCover` |
| `ResetCustomCoverCommand` | Detail panel (visible when `SelectedGameHasCustomCover`) | Restore automatic cover |
| `SetGridView` / `SetListView` | Toolbar toggle | Switches `LibraryViewMode` |

List view uses the same `GameItemViewModel` cover properties (`DisplayListCover`) with a horizontal row template in `MainWindow.axaml`.

### `GameItemViewModel` and `CoverImageLoader`

| Property / method | Role |
|-------------------|------|
| `CoverImage` | Avalonia `Bitmap` for the tile |
| `ShowCoverInGrid` | Whether this row is on the current page and covers are enabled |
| `DisplayGridCover` / `DisplayListCover` | `ShowCoverInGrid && HasCover` |
| `LoadCoverAsync` | Cache-first, or `MetadataService.EnsureCoverAsync` when enriching |
| `ReleaseCover` | `Dispose()` bitmap and clear `HasCover` |

`CoverImageLoader` (legacy helper) may still be referenced for shared loading patterns; grid cells use `GameItemViewModel` directly.

---

## Memory management

Large libraries + covers can use significant RAM. Mitigations in `MainWindowViewModel`:

### Pagination

`PageSize = 24` — only the current page is bound to `Games`; covers load for visible rows only.

### `ApplyVisibleCovers`

Called on page change, cover setting toggle, and after custom cover update:

```
If _suppressCoverLoading:
  → all games ShowCoverInGrid = false (startup / refresh in progress)

If NOT ShowGridCovers:
  → hide all grid covers
  → ReleaseCover() on every game except SelectedGame
  → optional GC.Collect (optimized)

Else:
  → ReleaseCover() on games NOT on current page (except SelectedGame)
  → ShowCoverInGrid = true + LoadCoverAsync() for current page items without cover
```

### Refresh lifecycle

```
MainWindowViewModel starts with _suppressCoverLoading = true

RefreshLibraryAsync completes:
  → ReleaseAllGameCovers() (dispose all old bitmaps)
  → replace _allGames with new GameItemViewModel instances
  → _suppressCoverLoading = false
  → ApplyFilter() → ApplyVisibleCovers()
  → StartBackgroundCoverEnrichment() (batch download up to limit)
```

**Why suppress during refresh:** avoids loading covers for VMs that are about to be discarded, which caused ~40 MB extra peak RAM at startup.

### Selection behavior

When changing selection, the previous game's cover is released **only if** it is not on the current page (`OnSelectedGameChanged`). Clicking the same game again deselects it (toggle).

---

## Database persistence on refresh

`GameDatabase.SyncScannedGames`:

1. Preserves `is_favorite` and `custom_cover` from existing rows
2. Re-applies `cover_path` from DB if the scan row has no cover but DB still has a valid path
3. Deletes games no longer returned by scan (cloud providers re-add owned titles)

Custom cover files on disk are **not** deleted on refresh — only on explicit reset or if the game row is deleted.

---

## Why so many automatic sources

No single source covers all titles:

- Steam CDN only for Steam AppIds
- IGDB/SteamGridDB require registration
- Wikipedia is imprecise but free
- Local covers sometimes exist in game folders (especially GOG/older titles)
- Riot titles use a fixed IGDB map when GameLib reports generic names

The cascade pipeline maximizes coverage without requiring configuration. Custom covers are the escape hatch when automatic sources fail or the user prefers different art.

---

## Related files

| File | Role |
|------|------|
| `Services/MetadataService.cs` | Download cascade, custom cover API |
| `Services/CoverImageProcessor.cs` | User upload resize |
| `Services/CoverPathHelper.cs` | Cache paths |
| `Services/CoverImageLoader.cs` | Shared load helpers |
| `Services/LocalCoverScanner.cs` | Install-folder images |
| `Services/SafeImageDownloader.cs` / `SafeImageValidator.cs` | Validation |
| `ViewModels/GameItemViewModel.cs` | Per-tile bitmap lifecycle |
| `ViewModels/MainWindowViewModel.cs` | `ApplyVisibleCovers`, custom cover commands |
| `Data/GameDatabase.cs` | `custom_cover` column, migration |
| `Models/UnifiedGame.cs` | `HasCustomCover`, `CoverPath` |
| `Models/LibraryViewMode.cs` | Grid vs List enum |

See also [data-model.md](data-model.md) for schema fields and [ui-and-viewmodels.md](ui-and-viewmodels.md) for binding overview.
