# Overall architecture

## Stack

| Layer | Technology | Reason |
|-------|------------|--------|
| UI | Avalonia 12 + Fluent | Desktop UI in .NET; light/dark themes (system default) |
| MVVM | CommunityToolkit.Mvvm | Commands, `ObservableProperty`, little boilerplate |
| Persistence | SQLite + Dapper | Single local file, no server |
| Game detection | GameLib.NET + GameFinder | Mature ecosystem for PC launchers |
| Epic cloud | [legendary](https://github.com/derrod/legendary) | No public Epic library API for third parties |

Target: `net8.0-windows` (see [windows-specific.md](windows-specific.md)).

## Project layout

Domain and infrastructure are separated from platform integrations and UI services:

```
Domain/
├── Models/          # UnifiedGame, AppSettings, LaunchSpec, …
└── Enums/           # Platform, CoverQualityMode, UiFontScale, ThemeMode, SortOption, …

Infrastructure/
├── Database/        # GameDatabase (SQLite + Dapper)
├── Secrets/         # DPAPI secrets store, Xbox token store
└── Http/            # IgdbClient, SafeImageDownloader

Services/
├── Covers/          # MetadataService, cover cache, image processing
├── Games/           # GameLibraryService, ICloudLibraryProvider
├── Configuration/   # SettingsService, LocalizationService, DevModeService
└── Updates/         # AppUpdateService

Providers/
├── Steam/
├── Epic/
├── GOG/
├── Ubisoft/
├── Xbox/
├── Ea/              # EA App (no public API; local catalog)
├── Riot/
├── Rockstar/
└── BattleNet/       # placeholder — today handled via GameLib only

ViewModels/  Views/  Converters/  Localization/
```

`GlobalUsings.cs` imports the common namespaces so ViewModels and providers do not need long `using` lists.

See [project-structure.md](project-structure.md) for a full guide to layers, data flows, and adding new platforms.

## Application startup

```
Program.Main [STAThread]
  └─ App.OnFrameworkInitializationCompleted
       └─ MainWindow { DataContext = MainWindowViewModel }

MainWindowViewModel (constructor):
  1. Initialize language (LocalizationService)
  2. LoadCachedGames()     → immediate read from library.db
  3. RefreshLibraryAsync() → full scan in background
  4. CheckForAppUpdateOnStartupAsync() → notice if new release (installed builds only)
```

**Why load cache before scanning:** the user sees their library when opening the app even if scanning takes several seconds (network, legendary, many launchers).

## Central orchestrator: `GameLibraryService`

File: `Services/Games/GameLibraryService.cs`

Responsibilities:

- Scan installed and cloud games
- Merge and deduplicate entries
- Persist to `GameDatabase`
- Enrich playtime and catalog cover URLs
- Launch / install games (`LaunchGame`)

Main dependencies:

- `LauncherManager` (GameLib) — installed launchers
- `ICloudLibraryProvider` — Steam, Epic, Ubisoft, EA, Riot, GOG
- Auxiliary scanners — `EpicManifestScanner`, `EaDesktopScanner`, `GogDesktopScanner`, `XboxGamePassScanner` (+ `XboxManifestReader`)
- `MetadataService` — covers and custom covers
- `SettingsService` — credentials and preferences

### Keep it thin (~500 lines max)

`GameLibraryService` is an **orchestrator**, not a dumping ground. Launch, GameLib scan, and dedup live in dedicated classes:

| Class | Responsibility |
|-------|----------------|
| `GameLibraryService` | Refresh orchestration, availability flags, DB/metadata delegation |
| `GameLaunchService` | Launch attempts, process/protocol execution |
| `InstalledGameScanner` | GameLib `LauncherManager` scan and `LaunchSpec` mapping |
| `GameLibraryMerger` | Deduplicate games, preserve catalog on failed cloud sync |

**Already extracted** (do not move back in):

| Concern | Where it lives |
|---------|----------------|
| Per-platform cloud library | `Providers/*/…CloudLibraryProvider` |
| Per-platform installed scan | `Providers/*/*Scanner`, `EpicManifestScanner`, … |
| Cover download & cache | `Services/Covers/MetadataService` |
| SQLite persistence | `Infrastructure/Database/GameDatabase` |
| Settings & locale | `Services/Configuration/` |

**Rule of thumb:** if you add a platform-specific `DetectX()` method to `GameLibraryService`, stop — put it in `Providers/<Platform>/` and call it from the orchestrator.

`GameLibraryService` public API stays stable for ViewModels: `LoadCachedGames`, `RefreshLibraryAsync`, `LaunchGame`, `ToggleFavorite`, availability flags.

## Flow: refresh library

```
RefreshLibraryAsync
│
├─ [Steam] If API key → GetOwnedGames (Web API)
│          If not, but Steam installed → read local VDF (active account)
│
├─ [Epic] If Epic Launcher installed:
│         LegendaryBootstrap.EnsureInstalledAsync (max 90 s)
│         EpicAuthHelper.PersistFromLegendary → settings.json
│
├─ ScanAllGames (in Task.Run, does not block UI)
│   ├─ ScanInstalledGames → GameLib LauncherManager
│   ├─ EaDesktopScanner
│   ├─ GogDesktopScanner
│   ├─ XboxGamePassScanner
│   ├─ EpicManifestScanner (.item manifests)
│   └─ For each available ICloudLibraryProvider:
│        GetUninstalledLibraryGames(current games)
│
├─ DeduplicateGames (install path → platform priority → title)
│
├─ GameDatabase.SyncScannedGames
│   (upsert + delete stale; keeps favorites and cached covers)
│
├─ Enrich catalog URLs (Steam CDN, Ubisoft CDN)
├─ Enrich Steam playtime (if owned list available)
└─ MetadataService.ReconcileCachedCovers
```

Cloud providers run in individual `try/catch`: if EA or Ubisoft fail, the rest continues.

## Flow: launch or install a game

```
MainWindowViewModel.LaunchSelectedGame
│
├─ Epic special case (not installed):
│   LaunchSpec.Kind == "protocol"
│   + Epic Launcher installed
│   → EpicLauncherClient.StartInstall(url)
│   → App does NOT wait for download (message and continues)
│
└─ Otherwise → GameLibraryService.LaunchGame
     ├─ If not installed: GetInstallLaunchAttempts from cloud provider
     ├─ Interpret LaunchSpec: protocol | executable | launcher-args
     └─ Chain attempts until one does not throw
```

See [ea-desktop.md](ea-desktop.md) for EA flow details.
See [epic-and-legendary.md](epic-and-legendary.md) for Epic flow details.
See [riot.md](riot.md) for Riot Client catalog and install.
See [metadata-and-covers.md](metadata-and-covers.md) for cover loading and custom covers.

## UI layers

| Layer | Folder | Role |
|-------|--------|------|
| Views | `Views/` | AXAML (MainWindow, Settings, prompts) |
| ViewModels | `ViewModels/` | State, commands, localized text |
| Converters | `Converters/` | Platform colors, selection borders |
| Localization | `Localization/`, `Resources/` | `Strings.resx` / `Strings.es.resx` |

`ViewLocator` resolves ViewModel → View by naming convention.

## Tools outside the main build

`tools/Diag`, `tools/LauncherDiag`, `tools/GenerateIcon` — excluded in `OpenGameHUB.csproj` (`Compile Remove="tools/**"`). Used to debug GameLib/launchers without opening the full UI.

## CI and releases

`.github/workflows/build-installer.yml`:

| Trigger | CI job | Publish job |
|---------|--------|-------------|
| Push / PR to `main` | format + build + test + coverage | — |
| Tag `alpha-*`, `beta-*`, `x.y.z` | same | installer + GitHub Release |
| Manual `workflow_dispatch` | same | — |

CI runs as one job with separate steps (restore → lint → build → test/coverage). Publish runs only after CI passes on tags.

Publish runs `build-installer.ps1 -AppVersion <tag>` and attaches `OpenGameHUB-Setup-<tag>.exe` to the release.

See [app-updater.md](app-updater.md).
