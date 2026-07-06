# Riot Games (Riot Client)

## Why no public library API

Riot does not expose a third-party REST API for "games I own" like Steam's Web API. Practical options on Windows:

1. **GameLib** — detects Riot Client and installed products when metadata is complete
2. **Local metadata** — `%ProgramData%\Riot Games\Metadata\` with per-product YAML settings
3. **Known catalog** — fixed list of live games (LoL, VALORANT, TFT, LoR, 2XKO)

OpenGameHUB combines GameLib's installed scan with **`RiotCloudLibraryProvider`**, which lists **owned but not installed** titles when Riot Client is present. Install and launch go through **`RiotClientServices.exe`** with `--launch-product` / `--skip-to-install` flags.

There is **no Riot account linking** in OpenGameHUB — the user must already use Riot Client on the same PC.

---

## Components

| File | Role |
|------|------|
| `Services/Riot/RiotCatalogReader.cs` | Find Riot Client, read catalog, detect installs |
| `Services/LibraryProviders/RiotCloudLibraryProvider.cs` | `ICloudLibraryProvider` for uninstalled catalog entries |
| `Services/RiotCoverClient.cs` | Static IGDB cover URLs for Riot titles |
| `Services/GameEntryFilter.cs` | Filters bogus metadata folder names |
| `Services/GameLibraryService.cs` | Registers provider, dedup key `riot:catalog:` |

GameLib's `LauncherManager` still contributes **installed** Riot games when it can parse them; the cloud provider fills gaps for uninstallable-but-owned entries.

---

## Finding Riot Client

`RiotCatalogReader.FindClientServicesExecutable()` searches in order:

1. **`RiotClientInstalls.json`** — `%ProgramData%\Riot Games\RiotClientInstalls.json` → `associated_client` paths ending in `RiotClientServices.exe`
2. **Registry** — `riotclient\shell\open\command` under `HKCR` and `HKLM`
3. **Default paths** — `C:\`, `D:\`, `E:\` `Riot Games\Riot Client\RiotClientServices.exe`

`IsLauncherInstalled()` is true when any candidate exists. `IsAvailable()` on the cloud provider uses the same check.

---

## Catalog: known products + metadata discovery

### Built-in catalog

| `productId` | Display title |
|-------------|---------------|
| `league_of_legends` | League of Legends |
| `valorant` | VALORANT |
| `bacon` | Legends of Runeterra |
| `lion` | 2XKO |
| `tft` | Teamfight Tactics |

### Metadata folders

Riot stores per-product data under:

```
%ProgramData%\Riot Games\Metadata\{product}.{patchline}\
  {product}.{patchline}.product_settings.yaml
```

`DiscoverMetadataProducts()` scans directory names matching:

```
^(?<product>[a-z0-9_]+)\.(?<patchline>[a-z0-9_]+)$
```

Rules:

| Rule | Reason |
|------|--------|
| Only patchline **`live`** | Avoid PBE/test entries in the library |
| Alias `teamfighttactics` → `tft` | Riot sometimes uses long folder names |
| Exclude `riot_client`, `riotclient` | Not games |
| Merge with known catalog | Known titles win for display names; unknown products get title-cased slug |

**Important:** folders like `league_of_legends.live.game_patch` do **not** match the regex (more than one dot segment in the product part), so they are ignored.

---

## Installed detection

`IsProductInstalled(productId, patchline)` reads:

```
%ProgramData%\Riot Games\Metadata\{product}.{patchline}\{product}.{patchline}.product_settings.yaml
```

Looks for `product_install_full_path` (YAML). Install counts only if the path exists on disk.

Fallback: line-by-line scan for `product_install_full_path:` if YAML deserialization fails.

---

## Cloud library: `RiotCloudLibraryProvider`

Registered in `GameLibraryService` alongside Steam, Epic, Ubisoft, EA, GOG.

### Availability

| Condition | Result |
|-----------|--------|
| `RiotClientServices.exe` found | Provider active |
| Not installed | Skipped silently |

### `GetUninstalledLibraryGames`

For each `RiotCatalogEntry` from `ReadLibraryEntries()`:

1. Skip if an installed Riot game in the current list **matches** (`MatchesInstalledGame`)
2. Skip if `IsProductInstalled` says the product is on disk
3. Create cloud entry with install launch spec

### Stable IDs

```
riot:catalog:{productId}@{patchline}
```

Example: `riot:catalog:valorant@live`

`PlatformGameId` = `productId` (e.g. `valorant`).

### `LaunchSpec`

```
Kind: launcher-args
Value: {RiotClientServices.exe}|--launch-product={productId} --launch-patchline={patchline} --skip-to-install
```

### Matching installed vs cloud

`MatchesInstalledGame` checks in order:

1. `PlatformGameId` equals `productId`
2. `--launch-product=` slug in existing game's `LaunchSpec`
3. Normalized title equality

---

## Install and launch

### Install (not installed game)

Primary path — `GetInstallLaunchAttempts`:

```text
RiotClientServices.exe --launch-product={id} --launch-patchline=live --skip-to-install
```

Fallbacks in the same chain:

- Same without `--skip-to-install` (opens product in client)
- Original `LaunchSpec` from the catalog entry

`GameLibraryService.LaunchGame` tries each `Action` until one succeeds.

### Play (installed via GameLib)

GameLib-generated `LaunchSpec` typically uses `launcher-args` without `--skip-to-install`. OpenGameHUB does not special-case Riot in `MainWindowViewModel` (unlike Epic's protocol install flow).

---

## Filtering junk entries (`GameEntryFilter`)

Riot metadata can produce phantom library rows. Excluded when `platform == Riot`:

| Condition | Example |
|-----------|---------|
| `PlatformGameId` contains `.` | `league_of_legends.live` |
| Title contains `.live`, `.pbe`, `.game_patch` | Folder names surfaced as titles |

Combined with strict metadata folder regex, this removes duplicate "League of Legends.live" style ghosts.

---

## Covers: `RiotCoverClient`

Riot games rarely have Steam AppIds. `MetadataService` calls `RiotCoverClient` late in the cascade.

### Product slug detection

Parses `--launch-product={slug}` from `LaunchSpec` when `Kind == launcher-args"`.

### Static IGDB image IDs

Maps slug or normalized title → IGDB `t_cover_big` URLs, e.g.:

| Product / title | IGDB image IDs |
|-----------------|----------------|
| `league_of_legends` | `co49wj`, `cobpn7` |
| `valorant` | `cobtjo` |
| `bacon` (LoR) | `co3wnv` |
| `tft` | `co8jux` |
| `lion` (2XKO) | `cobwkh` |

Then falls back to Wikipedia and Steam store search like other platforms.

**No Riot CDN integration** — covers are community IGDB assets or search results.

See [metadata-and-covers.md](metadata-and-covers.md) for the full cover pipeline and custom cover behavior.

---

## Deduplication

In `GameLibraryService.GetDedupKey`, cloud Riot entries are keyed by full `id` (same pattern as EA/Ubisoft catalog IDs) so they do not collapse with GameLib path-based installed rows incorrectly.

Platform priority in `PickPreferredDuplicate` assigns Riot weight **80** (same tier as other launcher-backed platforms).

---

## UI and localization

After refresh, status bar may append:

- EN: ` · Riot library via Riot Client`
- ES: ` · Biblioteca Riot vía Riot Client`

Progress during scan: `SyncingRiotLibrary` / `Sincronizando biblioteca de Riot...`

Error string: `RiotClientNotInstalled` if the provider throws during scan (client removed mid-session).

---

## Data flow (summary)

```
RefreshLibraryAsync
  └─ ScanAllGames
       ├─ GameLib → installed Riot games (if any)
       └─ RiotCloudLibraryProvider.GetUninstalledLibraryGames
            ├─ RiotCatalogReader.ReadLibraryEntries()
            ├─ filter installed / duplicates
            └─ LaunchSpec → RiotClientServices.exe + --skip-to-install

DeduplicateGames → SyncScannedGames → MetadataService.ReconcileCachedCovers
  └─ EnrichCoversAsync → RiotCoverClient for Riot titles
```

---

## Legal note

OpenGameHUB is not affiliated with Riot Games. It launches the official Riot Client; it does not modify game files or bypass Riot's install flow.

See [platform-integrations.md](platform-integrations.md) for the cross-platform summary table.
