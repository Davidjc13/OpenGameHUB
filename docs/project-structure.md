# Project structure

Guide to the current OpenGameHUB architecture: layers, data flows, and how to integrate a new platform without turning `GameLibraryService` into a god class.

For historical design decisions (why SQLite, legendary, etc.) see [design-decisions.md](design-decisions.md). For per-platform flows see [platform-integrations.md](platform-integrations.md).

---

## Guiding principle

Separate **what a game is** (domain), **how it is stored or downloaded** (infrastructure), **cross-cutting app logic** (services), and **each concrete launcher** (providers).

```
┌─────────────────────────────────────────────────────────────┐
│  UI — Views / ViewModels / Converters / Localization        │
└───────────────────────────┬─────────────────────────────────┘
                            │ uses stable API
┌───────────────────────────▼─────────────────────────────────┐
│  Services — orchestration without platform logic            │
│  Games · Covers · Configuration · Updates                   │
└───────┬───────────────────────────────┬─────────────────────┘
        │                               │
        ▼                               ▼
┌───────────────┐               ┌───────────────────┐
│  Domain       │               │  Providers        │
│  Models/Enums │◄──────────────│  Steam, Epic, …   │
└───────────────┘               └─────────┬─────────┘
        ▲                                 │
        │                                 ▼
┌───────┴───────────────────────────────────────────────┐
│  Infrastructure — Database, Secrets, Http               │
└───────────────────────────────────────────────────────┘
```

This is not full hexagonal architecture, but the rule is clear: **if the code mentions Ubisoft, Steam, or Riot, it lives in `Providers/`**, not in `GameLibraryService`.

---

## Folder tree

```
OpenGameHUB/
├── Domain/
│   ├── Models/          UnifiedGame, LaunchSpec, AppSettings, …
│   └── Enums/           Platform, CoverQualityMode, UiFontScale, SortOption, …
│
├── Infrastructure/
│   ├── Database/        GameDatabase (SQLite + Dapper)
│   ├── Secrets/         encrypted settings, Xbox tokens (DPAPI)
│   └── Http/            IgdbClient, SafeImageDownloader
│
├── Services/
│   ├── Games/           library and launch orchestration
│   ├── Covers/          cover art, cache, MetadataService
│   ├── Configuration/   SettingsService, LocalizationService
│   └── Updates/         AppUpdateService (GitHub Releases)
│
├── Providers/
│   ├── Steam/
│   ├── Epic/
│   ├── GOG/
│   ├── Ubisoft/
│   ├── Xbox/
│   ├── Ea/
│   ├── Riot/
│   ├── Rockstar/
│   └── BattleNet/       placeholder (GameLib only today)
│
├── ViewModels/          MVVM — state and commands
├── Views/               AXAML
├── Converters/          Avalonia bindings
├── Localization/        Strings.resx accessor
└── Resources/           Strings.resx / Strings.es.resx
```

### Namespaces

Each folder has a matching C# namespace:

| Folder | Example namespace |
|--------|-------------------|
| `Domain/Models` | `OpenGameHUB.Domain.Models` |
| `Domain/Enums` | `OpenGameHUB.Domain.Enums` |
| `Infrastructure/Database` | `OpenGameHUB.Infrastructure.Database` |
| `Providers/Steam` | `OpenGameHUB.Providers.Steam` |
| `Providers/GOG` | `OpenGameHUB.Providers.Gog` |
| `Services/Games` | `OpenGameHUB.Services.Games` |

`GlobalUsings.cs` imports common namespaces so ViewModels and providers do not need long `using` lists.

---

## Layers in detail

### Domain

Pure types — no I/O, no launcher references.

| Type | Role |
|------|------|
| `UnifiedGame` | Core entity: title, platform, paths, favorite, `LaunchSpec` |
| `LaunchSpec` | How to launch: `protocol`, `executable`, or `launcher-args` |
| `Platform` | Platform enum |
| `AppSettings` | In-memory user preferences |

Everything persisted in SQLite is modeled here; `GameDatabase` only maps rows ↔ `UnifiedGame`.

### Infrastructure

Reusable technical implementations without library business rules.

| Module | Responsibility |
|--------|----------------|
| `GameDatabase` | CRUD on `library.db`, post-scan sync, favorites, covers |
| `SettingsSecretsStore` | DPAPI-encrypted API keys |
| `XboxTokenStore` | Xbox OAuth tokens |
| `IgdbClient` / `SafeImageDownloader` | Secure HTTP for metadata |

### Services

Application logic that **coordinates** providers and infrastructure.

#### `Services/Games/`

| Class | Responsibility | ~lines |
|-------|----------------|--------|
| `GameLibraryService` | Orchestrator: refresh, availability flags, delegation | ~300 |
| `InstalledGameScanner` | GameLib scan (`LauncherManager`) → `UnifiedGame` | ~230 |
| `GameLibraryMerger` | Deduplicate, platform priority, preserve catalog on failures | ~120 |
| `GameLaunchService` | Launch/install attempt chain | ~210 |
| `ICloudLibraryProvider` | Contract for cloud library (uninstalled games) | — |
| `GameEntryFilter` | Exclude junk entries (redistributables, Riot metadata, etc.) | — |

**Rule:** `GameLibraryService` must not grow with `DetectSteam()`, `DetectEpic()`, etc. That belongs in `Providers/`.

#### `Services/Covers/`

`MetadataService` orchestrates cover downloads (IGDB, SteamGridDB, Wikipedia, per-platform clients). Platform `*CoverClient` classes can live in `Services/Covers/` or in `Providers/<Platform>/` when highly specific.

#### `Services/Configuration/`

`SettingsService`, `LocalizationService`, `DevModeService`. Settings and locale — not library storage.

#### `Services/Updates/`

`AppUpdateService` — checks GitHub Releases and downloads the installer.

### Providers

**One folder per launcher.** Everything that only makes sense for that platform:

- Local catalog reading (registry, JSON, YAML, manifests)
- Cloud client / Web API
- Installed-game scanner (when GameLib is not enough)
- Launch/install client (`EpicLauncherClient`, `RiotLauncherClient`, …)
- `*CloudLibraryProvider` for owned-but-uninstalled games

Minimal example for `Providers/Riot/`:

```
RiotCatalogReader.cs        # read local metadata, resolve RiotClientServices.exe
RiotCloudLibraryProvider.cs # ICloudLibraryProvider
RiotLauncherClient.cs       # install/launch with correct flags
```

### UI

| Folder | Role |
|--------|------|
| `ViewModels/` | Observable state, `RelayCommand`, text via `LocalizedStrings` |
| `Views/` | AXAML windows |
| `Converters/` | Platform colors, selection borders |

The UI talks to `GameLibraryService` and configuration services, not to providers directly (except for specific prompts: Epic, Xbox auth, etc.).

---

## How it works: library lifecycle

### 1. Startup

```
MainWindowViewModel
  ├─ LocalizationService.Initialize()
  ├─ GameLibraryService.LoadCachedGames()   → instant SQLite read
  ├─ RefreshLibraryAsync()                  → background scan
  └─ AppUpdateService (installed builds only)
```

The user sees their library immediately even if scanning takes several seconds.

### 2. Refresh (`GameLibraryService.RefreshLibraryAsync`)

```
Per-platform pre-sync (credentials, tokens)
  ├─ Steam: Web API or local VDF
  ├─ Epic: legendary bootstrap + auth
  └─ Xbox: LoadLibraryAsync when authenticated

ScanAllGames (in Task.Run)
  ├─ InstalledGameScanner     → GameLib (Steam, Epic, GOG, Ubisoft, EA, Battle.net, Rockstar, Riot…)
  ├─ EaDesktopScanner
  ├─ GogDesktopScanner
  ├─ XboxGamePassScanner
  ├─ EpicManifestScanner
  └─ each available ICloudLibraryProvider
       → GetUninstalledLibraryGames()

GameLibraryMerger.Deduplicate()
GameDatabase.SyncScannedGames()
Enrichment (catalog covers, Steam playtime)
MetadataService.ReconcileCachedCovers()
```

Each cloud provider runs in its own `try/catch`: if EA fails, the rest continues.

### 3. Deduplication

`GameLibraryMerger` merges duplicate entries by:

1. Same install path → higher platform priority wins
2. Same catalog ID (`steam:…`, `riot:catalog:…`, etc.)
3. Same normalized title (last resort)

Approximate priority: Riot > Steam > Ubisoft > EA > GOG > Battle.net > Rockstar > Game Pass > Epic.

Epic often appears as a “wrapper” for native Riot/Ubisoft/EA games; the merger avoids visible duplicates.

### 4. Launch / Install

```
MainWindowViewModel.LaunchSelectedGame
  ├─ UI special cases (Epic protocol, Riot, Rockstar, Xbox)
  └─ GameLibraryService.LaunchGame
       └─ GameLaunchService.Launch
            ├─ cloud provider GetInstallLaunchAttempts() (if not installed)
            ├─ LaunchSpec (protocol | executable | launcher-args)
            └─ per-platform fallbacks (Steam -applaunch, EA link2ea, …)
```

`LaunchSpec` is the portable contract between scan and launch:

```csharp
LaunchSpec.Protocol("uplay://install/12345")
LaunchSpec.Executable(@"C:\Games\foo.exe")
LaunchSpec.LauncherArgs(@"C:\Riot Games\...\RiotClientServices.exe", "--launch-product=valorant --skip-to-install")
```

---

## Core contract: `ICloudLibraryProvider`

Defined in `Services/Games/ICloudLibraryProvider.cs`.

```csharp
public interface ICloudLibraryProvider
{
    Platform Platform { get; }
    bool IsAvailable();
    IReadOnlyList<UnifiedGame> GetUninstalledLibraryGames(...);
    IEnumerable<Action> GetInstallLaunchAttempts(UnifiedGame game);
}
```

| Method | When it runs |
|--------|--------------|
| `IsAvailable()` | Is launcher, credentials, or local cache present for cloud listing? |
| `GetUninstalledLibraryGames()` | During `ScanAllGames` — owned but not installed |
| `GetInstallLaunchAttempts()` | During install from UI — attempt chain |

`UnifiedGame.Id` conventions for catalog entries:

| Platform | ID pattern |
|----------|------------|
| Steam cloud | `steam:store:{appId}` |
| Epic | `epic:legendary:{appName}` |
| Ubisoft | `ubisoft:catalog:{uplayId}` |
| EA | `ea:catalog:{softwareId}` |
| Riot | `riot:catalog:{productId}@{patchline}` |
| GOG | `gog:catalog:{gogId}` |
| Rockstar | `rockstar:catalog:{titleId}` |
| Xbox | `gamepass:catalog:{pfn}` |

Stable prefixes let `GameLibraryMerger` avoid collapsing catalog entries with GameLib installed rows.

---

## Adding a new platform

Example: integrate **Battle.net** with its own cloud library (today only GameLib detects installed games).

### Step 1 — Domain

1. Add a value to `Domain/Enums/Platform.cs` if missing (`BattleNet` already exists).
2. Add a label in `Domain/Models/PlatformLabels.cs`.
3. Add a color in `Converters/PlatformBrushConverter.cs`.

### Step 2 — `Providers/BattleNet/` folder

Create only the files you need:

| Typical file | Required | Purpose |
|--------------|----------|---------|
| `BattleNetCatalogReader.cs` | Almost always | Read local cache, registry, API |
| `BattleNetCloudLibraryProvider.cs` | If cloud library | Implement `ICloudLibraryProvider` |
| `BattleNetDesktopScanner.cs` | If GameLib is not enough | Extra installed-game scan |
| `BattleNetLauncherClient.cs` | If install is special | Close launcher, protocols, args |
| `BattleNetCoverClient.cs` | Optional | Covers in `MetadataService` |

Namespace: `OpenGameHUB.Providers.BattleNet`.

### Step 3 — Implement `ICloudLibraryProvider`

Skeleton:

```csharp
namespace OpenGameHUB.Providers.BattleNet;

public sealed class BattleNetCloudLibraryProvider : ICloudLibraryProvider
{
    public Platform Platform => Platform.BattleNet;

    public bool IsAvailable() =>
        BattleNetCatalogReader.IsLauncherInstalled();

    public IReadOnlyList<UnifiedGame> GetUninstalledLibraryGames(
        IReadOnlyList<UnifiedGame> currentGames,
        CancellationToken cancellationToken = default)
    {
        // 1. Filter already installed (by PlatformGameId or title)
        // 2. Read local catalog / API
        // 3. Return UnifiedGame with stable Id, LaunchSpec, IsInstalled = false
    }

    public IEnumerable<Action> GetInstallLaunchAttempts(UnifiedGame game)
    {
        if (game.Platform != Platform.BattleNet || game.IsInstalled)
            yield break;

        // Multiple attempts: protocol, launcher-args, executable…
        yield return () => BattleNetLauncherClient.StartInstall(game);
    }
}
```

Full references: `Providers/Ubisoft/UbisoftCloudLibraryProvider.cs` (protocol), `Providers/Riot/RiotCloudLibraryProvider.cs` (launcher-args + dedicated client).

### Step 4 — Register in the orchestrator

In the `GameLibraryService` constructor:

```csharp
_cloudProviders =
[
    // …existing…
    new BattleNetCloudLibraryProvider(),
];
```

If installed scan needs its own scanner, add to `ScanAllGames()`:

```csharp
games.AddRange(BattleNetDesktopScanner.Scan());
```

If pre-sync is needed (tokens, API key), add a block in `RefreshLibraryAsync()` **before** `ScanAllGames`, not inside the scanner.

### Step 5 — UI and strings

| What | Where |
|------|-------|
| `IsBattleNetCloudAvailable` | Property on `GameLibraryService` + `MainWindowViewModel` |
| Scan progress message | `Loc.T("SyncingBattleNetLibrary")` in `Strings.resx` / `Strings.es.resx` |
| Status bar hint | `MainWindowViewModel` (see `RiotCloudHint`) |
| Special install in UI | Only if flow does not fit `GameLaunchService` (see Epic/Riot/Rockstar in `LaunchSelectedGame`) |
| Merger priority | `GameLibraryMerger.GetPlatformPriority()` if needed |

### Step 6 — Covers (optional)

In `MetadataService`, add a branch for `Platform.BattleNet` calling your `BattleNetCoverClient` or catalog URL.

### Step 7 — Filters and documentation

- Junk entries → `GameEntryFilter.IsExcluded()`
- Platform-specific doc → `docs/battlenet.md` (optional)
- Entry in [platform-integrations.md](platform-integrations.md)

### Step 8 — `GlobalUsings.cs`

```csharp
global using OpenGameHUB.Providers.BattleNet;
```

---

## Quick checklist

```
[ ] Platform enum + PlatformLabels + PlatformBrushConverter
[ ] Providers/<Name>/ with CatalogReader and/or CloudLibraryProvider
[ ] Register in GameLibraryService._cloudProviders
[ ] Scanner in ScanAllGames() if applicable
[ ] Pre-sync in RefreshLibraryAsync() if applicable (auth, tokens)
[ ] GetInstallLaunchAttempts() or dedicated LauncherClient
[ ] Special case in MainWindowViewModel.LaunchSelectedGame if needed
[ ] Strings.resx (EN + ES)
[ ] GameEntryFilter for junk metadata
[ ] Priority in GameLibraryMerger if colliding with Epic/others
[ ] GlobalUsings
[ ] Documentation
```

---

## Where to put things (quick decision)

| Question | Answer |
|----------|--------|
| Is it a data type or enum? | `Domain/` |
| SQLite, DPAPI, generic HTTP? | `Infrastructure/` |
| Orchestrates multiple platforms? | `Services/Games/` or `Services/Covers/` |
| Only makes sense for one launcher? | `Providers/<Platform>/` |
| Screen or user command? | `ViewModels/` + `Views/` |
| Translatable text? | `Resources/Strings*.resx` |

**Anti-pattern:** adding `DetectBattleNet()` or 200 lines of Blizzard logic inside `GameLibraryService`. Extract to `Providers/BattleNet/` and call from the orchestrator.

---

## External dependencies by layer

| Layer | Typical libraries |
|-------|-------------------|
| GameLib scan | `GameLib.NET` (`LauncherManager`) |
| Xbox installed | `GameFinder.StoreHandlers.Xbox` |
| Epic cloud | `legendary` (bundled or downloaded exe) |
| Persistence | `Microsoft.Data.Sqlite`, `Dapper` |
| UI | `Avalonia`, `CommunityToolkit.Mvvm` |

GameLib covers Steam, Epic, GOG, Ubisoft, EA, Battle.net, Rockstar, and Riot **installed** games. Providers fill gaps: Epic manifests in `ProgramData`, encrypted EA catalog, uninstalled Riot library, Xbox Game Pass, etc.

---

## CI

Workflow `.github/workflows/build-installer.yml`:

- **CI** — every push/PR: format lint, build (analyzers), tests + coverage report
- **Publish** — tags only (`alpha-*`, `beta-*`, `x.y.z`), after CI passes: Inno installer + GitHub Release

See [app-updater.md](app-updater.md).

---

## Testing

Unit tests live in `tests/OpenGameHUB.Tests/` (xUnit). They cover pure logic that should not regress: version comparison, library merge/dedup, entry filters, launch spec encoding, Riot argument parsing.

```powershell
dotnet test OpenGameHUB.sln -c Release
```

CI runs tests in a separate job after build succeeds. Add tests when you change `GameLibraryMerger`, `ReleaseVersionComparer`, `GameEntryFilter`, or provider parsing helpers.

The main project exposes `internal` types to the test assembly via `InternalsVisibleTo` in `OpenGameHUB.csproj`.

---

## Related documents

| Document | Contents |
|----------|----------|
| [architecture.md](architecture.md) | Startup, refresh, and launch flows (diagrams) |
| [cloud-providers.md](cloud-providers.md) | Each `*CloudLibraryProvider` in detail |
| [data-model.md](data-model.md) | SQLite schema and `UnifiedGame` fields |
| [metadata-and-covers.md](metadata-and-covers.md) | Cover pipeline |
| [epic-and-legendary.md](epic-and-legendary.md) · [riot.md](riot.md) · [ea-desktop.md](ea-desktop.md) | Per-platform guides |
