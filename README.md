# OpenGameHUB

[Español](README.es.md)

A lightweight Windows desktop meta-launcher that unifies your game libraries across multiple storefronts in one place. Browse installed and cloud games, launch or install them, and keep cover art and playtime in a single library.

> **Platform:** Windows 10/11 only for now. Linux is not supported yet.

Pre-built installers are published on [GitHub Releases](https://github.com/Davidjc13/OpenGameHUB/releases).

## Features

- **Multi-platform library** — Detects games from Steam, Epic, GOG, Ubisoft Connect, EA App, Battle.net, Rockstar, Riot Games, and **Xbox PC / Game Pass** (installed) via [GameLib.NET](https://github.com/Phalkion/GameLib.NET) and [GameFinder](https://github.com/erri120/GameFinder).
- **Cloud libraries** — Steam owned games (Web API or local Steam account) and Epic games (via [legendary](https://github.com/derrod/legendary)) appear even when not installed. Ubisoft and EA cloud catalogs are supported where available.
- **Install from the app** — Uninstalled Steam games open the Steam install flow. Epic cloud games trigger the **Epic Games Launcher** install flow (the app does not wait for the download to finish).
- **Epic account** — Connect or disconnect your Epic account from Settings to sync the cloud library.
- **Cover art** — Steam CDN, Wikipedia, IGDB, SteamGridDB, and built-in mappings for Riot titles. Covers are cached locally.
- **Playtime** — Steam playtime sync when API credentials are configured.
- **Favorites & filters** — Search, filter by platform, sort by name/playtime, show favorites or installed only.
- **In-app updates** — Installed builds can check GitHub Releases and download the latest installer from Settings.
- **Localization** — English and Spanish UI.

## Requirements

- Windows 10/11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for development)
- Installed launchers for the platforms you use (Steam, Epic Games Launcher, etc.)

### Optional

| Integration | Purpose |
|-------------|---------|
| Epic Games Launcher + legendary | Epic cloud library, account sign-in, and installs |
| Steam Web API key + SteamID64 | Cloud Steam library and playtime (without API, local Steam account data is used when Steam is installed) |
| IGDB Client ID + Secret | Better cover art for non-Steam games |
| SteamGridDB API key | Alternative cover art sources |

## Download

Download the latest `OpenGameHUB-Setup-*.exe` from [Releases](https://github.com/Davidjc13/OpenGameHUB/releases) and run it. No admin rights required.

Installed location:

```
%LocalAppData%\Programs\OpenGameHUB\
```

## Getting started (development)

```powershell
git clone https://github.com/Davidjc13/OpenGameHUB.git
cd OpenGameHUB
dotnet run
```

### Publish a standalone executable

```powershell
.\publish.ps1
```

Output: `publish/win-x64/OpenGameHUB.exe` (self-contained, single file).

### Build a Windows installer (`setup.exe`)

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php) (installed automatically via winget if missing).

```powershell
.\build-installer.ps1
```

With an explicit version (recommended for releases):

```powershell
.\build-installer.ps1 -AppVersion "alpha-0.0.7"
```

Output: `dist/OpenGameHUB-Setup-<version>.exe`

The installer:
- Installs to `%LocalAppData%\Programs\OpenGameHUB` (per-user, no admin required)
- Creates Start Menu shortcut and optional desktop icon
- Registers an uninstaller in Windows Settings
- Includes the Spanish language pack (`es/`)
- Shows the GPL-3.0 license during setup

### Release workflow (CI)

Pushing a tag matching `alpha-*`, `beta-*`, or `x.y.z` triggers [`.github/workflows/build-installer.yml`](.github/workflows/build-installer.yml), which builds the installer and attaches it to the GitHub Release.

Example:

```powershell
git tag alpha-0.0.8
git push origin alpha-0.0.8
```

## Configuration

Open **Settings** in the app to configure:

- **Language** — English or Spanish
- **Steam Web API** — [API key](https://steamcommunity.com/dev/apikey) and [SteamID64](https://steamid.io)
- **Epic account** — Connect or disconnect Epic for cloud library sync
- **Display** — Toggle cover art in the game grid
- **IGDB / SteamGridDB** — Optional cover art providers
- **Updates** — Check for updates and install the latest release from GitHub

Settings are stored at:

```
%LocalAppData%\OpenGameHUB\settings.json
```

## Documentation

Technical docs (architecture, integrations, design decisions): **[docs/](docs/README.md)**.

## Data storage

| Path | Contents |
|------|----------|
| `%LocalAppData%\OpenGameHUB\library.db` | Game library (SQLite) |
| `%LocalAppData%\OpenGameHUB\covers\` | Cached cover images |
| `%LocalAppData%\OpenGameHUB\tools\` | Downloaded legendary helper (if needed) |
| `%USERPROFILE%\.config\legendary\` | Epic credentials managed by legendary |

## Developer mode

When building in **Debug**, or when `OPENGAMEHUB_DEV=1` is set, Settings shows a **Developer** section:

- **Reset connections** — Clears Steam API, Epic auth, and onboarding prompts
- **Reset & relaunch** — Same as above, clears local library cache, and restarts the app
- **Clear local database** — Deletes `library.db` and cover cache, then refreshes the library

```powershell
$env:OPENGAMEHUB_DEV = "1"
dotnet run
```

## Project structure

```
OpenGameHUB/
├── Data/              SQLite persistence
├── Models/            Domain types (UnifiedGame, Platform, …)
├── Services/          Scanning, metadata, launch logic, updater
│   ├── Epic/          Epic manifests, legendary bootstrap, launcher client
│   ├── Ea/            EA Desktop catalog helpers
│   └── LibraryProviders/  Cloud library providers (Steam, Epic, Ubisoft, EA)
├── ViewModels/        MVVM layer
├── Views/             Avalonia UI (MainWindow, Settings, onboarding prompts)
├── Resources/         Localization strings (en / es)
├── Localization/      UI string bindings
├── Converters/        Avalonia value converters
├── installer/         Inno Setup script
└── tools/             Diagnostic utilities (not part of the main build)
```

## Tech stack

- **.NET 8** (`net8.0-windows`)
- **Avalonia UI 12** — Desktop UI
- **CommunityToolkit.Mvvm** — MVVM helpers
- **Dapper + Microsoft.Data.Sqlite** — Local database
- **GameLib.NET + GameFinder** — Launcher and game detection
- **legendary** — Epic Games cloud library and authentication

## Disclaimer

OpenGameHUB is an independent project. It is **not affiliated with or endorsed by** Steam, Epic Games, Ubisoft, EA, Microsoft, or any other platform. Official launchers are required to install and play games. Software is provided **without warranty**; see [LICENSE](LICENSE).

## License

This project is licensed under the [GNU General Public License v3.0](LICENSE).
