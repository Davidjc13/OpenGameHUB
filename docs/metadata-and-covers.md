# Metadata and covers

## Goal

Show covers in the grid and detail panel without requiring the user to configure external APIs.

## Orchestrator: `MetadataService`

File: `Services/MetadataService.cs`

### `ReconcileCachedCovers`

After loading or refreshing the library:

- For each game, if a file already exists in `covers/` or the install folder, update `CoverPath` in memory/DB
- Does not download anything — only aligns existing paths

### `EnrichCoversAsync`

Downloads missing covers (configurable limit, e.g. 32 in background from `MainWindowViewModel`):

```
For each game without valid CoverPath:
  1. LocalCoverScanner — search .jpg/.png in InstallPath
  2. If CatalogCoverUrl (Steam/Ubisoft/GOG CDN or local webcache) → download or copy
  3. If known Steam AppId → Steam CDN
  4. IgdbClient (if credentials)
  5. SteamGridDbClient (if API key)
  6. SteamStoreSearchClient — search Steam store by title
  7. WikipediaCoverClient
  8. RiotCoverClient — fixed map for Valorant, LoL, etc.
```

Saves to `%LocalAppData%\OpenGameHUB\covers\` via `CoverPathHelper`.

## External clients

| Client | API | Credentials |
|--------|-----|-------------|
| `IgdbClient` | Twitch OAuth → `api.igdb.com` | Client ID + Secret (Settings) |
| `SteamGridDbClient` | `steamgriddb.com` | API key (Settings) |
| `WikipediaCoverClient` | Wikipedia REST | None |
| `SteamStoreSearchClient` | Steam store search | None |
| `RiotCoverClient` | Static URLs | None |

Steam CDN, Ubisoft CDN, and GOG CDN do not go through `MetadataService` the same way: they are used as `CatalogCoverUrl` during scanning (`SteamWebApiService.EnrichCatalogCoverUrls`, `UbisoftCatalogReader.EnrichCatalogCoverUrls`, `GogCatalogReader.EnrichCatalogCoverUrls`).

For GOG, `CatalogCoverUrl` may also be a **local file path** from Galaxy's webcache; `MetadataService` copies it directly instead of downloading via HTTP.

## Download safety

- `SafeImageDownloader` + `SafeImageValidator` — validates image type/size before saving
- `HttpClient` with User-Agent `OpenGameHUB/1.0` and 8 s timeout

**Why validate:** URLs come from third-party searches and APIs; avoid saving HTML or huge files as "covers".

## `MetadataSearchHelper`

Title normalization for search and deduplication:

- Strips suffixes like ` - Windows`, `(PC)`, GOTY editions, etc.
- Used when comparing Epic vs Riot/Ubisoft and when matching favorites by title

## UI: `ShowGridCovers`

Setting in `AppSettings` — if disabled, the grid does not load bitmaps (saves RAM); the detail panel can still show a cover when selecting a game.

## `CoverImageLoader` / `GameItemViewModel`

Lazy image loading for the grid; disposes bitmaps when recycling cells to avoid exhausting memory with large libraries.

## Why so many sources

No single source covers all titles:

- Steam CDN only for Steam AppIds
- IGDB/SteamGridDB require registration
- Wikipedia is imprecise but free
- Local covers sometimes exist in game folders (especially GOG/older titles)

The cascade pipeline maximizes coverage without requiring configuration.
