# OpenGameHUB

A lightweight Windows desktop meta-launcher that unifies your game libraries across multiple storefronts in one place. Browse installed and cloud games, launch or install them, and keep cover art and playtime in a single library.

## Features

- **Multi-platform library** — Detects games from Steam, Epic, GOG, Ubisoft Connect, EA App, Battle.net, Rockstar, and Riot Games via [GameLib.NET](https://github.com/Phalkion/GameLib.NET).
- **Cloud libraries** — Steam owned games (via Web API) and Epic games (via [legendary](https://github.com/derrod/legendary)) appear even when not installed.
- **Install from the app** — Uninstalled Steam games open the Steam install flow; Epic cloud games launch through legendary.
- **Cover art** — Steam CDN, Wikipedia, IGDB, SteamGridDB, and built-in mappings for Riot titles. Covers are cached locally.
- **Playtime** — Steam playtime sync when API credentials are configured.
- **Favorites & filters** — Search, filter by platform, sort by name/playtime, show favorites or installed only.
- **Localization** — English and Spanish UI.

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for development)
- Installed launchers for the platforms you use (Steam, Epic, etc.)

### Optional

| Integration | Purpose |
|-------------|---------|
| [legendary](https://github.com/derrod/legendary) | Epic cloud library and installs |
| Steam Web API key + SteamID64 | Cloud Steam library and playtime |
| IGDB Client ID + Secret | Better cover art for non-Steam games |
| SteamGridDB API key | Alternative cover art sources |

## Getting started

```powershell
git clone <repository-url>
cd OpenGameHUB
dotnet run
```

### Publish a standalone executable

```powershell
.\publish.ps1
```

Output: `publish/win-x64/OpenGameHUB.exe` (self-contained, single file).

## Configuration

Open **Settings** in the app to configure:

- **Language** — English or Spanish
- **Steam Web API** — [API key](https://steamcommunity.com/dev/apikey) and [SteamID64](https://steamid.io)
- **IGDB / SteamGridDB** — Optional cover art providers

Settings are stored at:

```
%LocalAppData%\OpenGameHUB\settings.json
```

## Data storage

| Path | Contents |
|------|----------|
| `%LocalAppData%\OpenGameHUB\library.db` | Game library (SQLite) |
| `%LocalAppData%\OpenGameHUB\covers\` | Cached cover images |

## Project structure

```
OpenGameHUB/
├── Data/              SQLite persistence
├── Models/            Domain types (UnifiedGame, Platform, …)
├── Services/          Scanning, metadata, launch logic
├── ViewModels/        MVVM layer
├── Views/             Avalonia UI (MainWindow, Settings)
├── Resources/         Localization strings (en / es)
├── Localization/      UI string bindings
├── Converters/        Avalonia value converters
└── tools/             Diagnostic utilities (not part of the main build)
```

## Tech stack

- **.NET 8** (`net8.0-windows`)
- **Avalonia UI 12** — Cross-platform desktop UI
- **CommunityToolkit.Mvvm** — MVVM helpers
- **Dapper + Microsoft.Data.Sqlite** — Local database
- **GameLib.NET** — Launcher and game detection

## License

This project is licensed under the [GNU General Public License v3.0](LICENSE).
