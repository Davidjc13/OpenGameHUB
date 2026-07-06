# Overall architecture

## Stack

| Layer | Technology | Reason |
|-------|------------|--------|
| UI | Avalonia 12 + Fluent | Desktop UI in .NET; consistent dark theme |
| MVVM | CommunityToolkit.Mvvm | Commands, `ObservableProperty`, little boilerplate |
| Persistence | SQLite + Dapper | Single local file, no server |
| Game detection | GameLib.NET + GameFinder | Mature ecosystem for PC launchers |
| Epic cloud | [legendary](https://github.com/derrod/legendary) | No public Epic library API for third parties |

Target: `net8.0-windows` (see [windows-specific.md](windows-specific.md)).

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

File: `Services/GameLibraryService.cs`

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

`.github/workflows/build-installer.yml` — on pushing tags `alpha-*`, `beta-*`, or `x.y.z`:

1. `build-installer.ps1 -AppVersion <tag>`
2. Uploads `OpenGameHUB-Setup-<tag>.exe` to GitHub Releases

See [app-updater.md](app-updater.md).
