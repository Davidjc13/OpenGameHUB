# Data model

## `UnifiedGame`

File: `Models/UnifiedGame.cs`

**Single** representation of a game in the library, whether from Steam, Epic, EA, etc.

| Field | Type | Description |
|-------|------|-------------|
| `Id` | string | Stable primary key (see below) |
| `Platform` | enum | `Platform.Steam`, `Epic`, `Gog`, … |
| `PlatformGameId` | string | Platform ID (Steam AppId, Epic `app_name`, etc.) |
| `Title` | string | Display name |
| `IsInstalled` | bool | Whether there is an install path / GameLib marks it installed |
| `InstallPath` | string? | Folder or main executable |
| `CoverPath` | string? | Path to cover in local cache (persisted) |
| `HasCustomCover` | bool | User-uploaded cover; blocks auto-download overwrite |
| `CatalogCoverUrl` | string? | Temporary catalog URL (not stored in DB) |
| `PlaytimeMinutes` | int | Mainly Steam (API) |
| `LastPlayed` | DateTime? | Last session (Steam API) |
| `IsFavorite` | bool | User-marked |
| `LaunchSpec` | LaunchSpec | How to launch or install |

User-defined **collections** are stored separately (see below); favorites remain on `IsFavorite` and are not migrated into collection tables.

`PlatformLabel` is derived via `PlatformLabels.Get(Platform)` for the UI.

## `LaunchSpec`

File: `Models/LaunchSpec.cs`

```csharp
public sealed record LaunchSpec(string Kind, string Value);
```

| Kind | Value | Example |
|------|-------|---------|
| `protocol` | Full URL | `steam://install/570` |
| `executable` | Path or `path\|args` | `C:\...\game.exe` |
| `launcher-args` | `launcher\|arguments` | `C:\...\steam.exe\|-applaunch 570` |

Factory methods: `LaunchSpec.Protocol`, `.Executable`, `.LauncherArgs`.

**Why a single kind/value pair:** simple SQLite serialization (`launch_kind`, `launch_value`).

## `Platform`

File: `Models/Platform.cs`

Values: `Steam`, `Epic`, `Gog`, `Ubisoft`, `Ea`, `BattleNet`, `Rockstar`, `Riot`, `GamePass`, `Unknown`.

GameLib maps launcher names to this enum in `GameLibraryService.MapPlatform`.

## `AppSettings`

File: `Models/AppSettings.cs`

Persisted by `SettingsService`: preferences in `settings.json`; `SteamApiKey`, `IgdbClientSecret`, and `SteamGridDbApiKey` in encrypted `secrets.dat` (DPAPI).

| Field | Use |
|-------|-----|
| `Language` | `en` / `es` |
| `SteamApiKey` | Steam Web API (encrypted on disk) |
| `SteamId` | Steam Web API |
| `IgdbClientId` | IGDB covers |
| `IgdbClientSecret` | IGDB covers (encrypted on disk) |
| `SteamGridDbApiKey` | Alternative covers (encrypted on disk) |
| `ShowGridCovers` | Grid covers vs detail panel only |
| `LibraryViewMode` | `Grid` or `List` library layout |
| `DismissSteamApiKeyPrompt` | Do not show Steam prompt again |
| `DismissEaLibraryPrompt` | Do not show EA prompt again |
| `DismissLegendaryPrompt` | Do not show Epic prompt again |
| `EpicAccountId`, `EpicDisplayName` | Copy from legendary for UI (`HasEpicAuth`) |

## SQLite schema (`library.db`)

File: `Data/GameDatabase.cs`

Table `games`:

| Column | Type | Notes |
|--------|------|-------|
| `id` | TEXT PK | `UnifiedGame.Id` |
| `platform` | INTEGER | `Platform` enum |
| `platform_game_id` | TEXT | |
| `title` | TEXT | |
| `is_installed` | INTEGER | 0/1 |
| `install_path` | TEXT | |
| `cover_path` | TEXT | Path in `%LocalAppData%\OpenGameHUB\covers\` |
| `custom_cover` | INTEGER | 0/1 — user chose a custom image |
| `playtime_minutes` | INTEGER | |
| `last_played` | TEXT | ISO 8601 |
| `is_favorite` | INTEGER | 0/1 |
| `launch_kind` | TEXT | |
| `launch_value` | TEXT | |
| `updated_at` | TEXT | Last upsert timestamp |

### `SyncScannedGames`

Important logic on refresh:

1. Read existing favorites (by `id`, platform+path composite key, normalized title)
2. Re-apply `cover_path` if scan brings no cover but DB has one
3. `UpsertGames` — insert or replace
4. `DELETE FROM games WHERE id NOT IN (...)` — remove games no longer detected by scan

**Why delete stale entries:** if you uninstall a game, it should disappear from the library (except cloud-only entries from cloud providers).

**Why keep favorites:** the user does not lose stars on refresh even if `id` changes slightly in edge cases (alternate key by title/path).

### User collections

Table `collections`:

| Column | Type | Notes |
|--------|------|-------|
| `id` | TEXT PK | GUID (`N` format) |
| `name` | TEXT | Display name |
| `sort_order` | INTEGER | Sidebar order |
| `created_at` | TEXT | ISO 8601 |
| `updated_at` | TEXT | ISO 8601 |

Table `collection_games` (many-to-many):

| Column | Type | Notes |
|--------|------|-------|
| `collection_id` | TEXT FK | References `collections.id` (`ON DELETE CASCADE`) |
| `game_id` | TEXT | References `games.id` (orphans purged after sync) |
| `added_at` | TEXT | ISO 8601 |

**System views** (All games, Favorites, Installed) are not persisted — they are derived filters in the UI. Only user-created collections use these tables.

## ID generation (examples)

Defined in scanners and providers when creating `UnifiedGame`:

| Source | Pattern |
|--------|---------|
| Installed (GameLib) | Hash of normalized path |
| Steam cloud | `steam:{appId}` |
| Epic cloud | `epic:legendary:{appName}` |
| Ubisoft cloud | `ubisoft:{uplayId}` |
| EA cloud | `ea:catalog:{softwareId}@{slug}` |
| Riot cloud | `riot:catalog:{productId}@{patchline}` |
| GOG cloud | `gog:catalog:{gogId}@{releaseKey}` |
| GOG installed (scanner) | `gog:path:{sha256-prefix}` |

Changing these formats is a **breaking change** for users with an existing DB.
