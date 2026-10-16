# Battle.net

OpenGameHUB combines GameLib’s installed scan with **`BattleNetCloudLibraryProvider`**, which lists owned but not installed titles when the Battle.net client and/or a connected account are available. Install and launch use **`BattleNetLauncherClient`** (`Battle.net.exe` with `--exec="install …"` / `--exec="launch …"`).

## Installed games

- **GameLib** (`InstalledGameScanner`) — reads `%ProgramData%\Battle.net\Agent\product.db` via GameLib’s Battle.net plugin
- **`BattleNetDesktopScanner`** — registry uninstall entries with `Battle.net … --uid=` when GameLib misses a title

## Cloud library (not installed)

**Local cache — `product.db`:**

- Path: `%ProgramData%\Battle.net\Agent\product.db` (protobuf)
- Entries with `playable == true` and `installed == false` are treated as owned but not installed
- Requires the Battle.net client to have synced at least once

**Account sync (Settings → Connect Battle.net account):**

- WebView2 login against `account.battle.net`
- After login, OpenGameHUB fetches:
  - `https://account.battle.net/api/games-and-subs`
  - `https://account.battle.net/api/classic-games`
- Results are stored in `%LocalAppData%\OpenGameHUB\battlenet-library.json` and merged with `product.db` during library refresh
- Session cookies live in `%LocalAppData%\OpenGameHUB\BattleNetProfile\`

## Catalog mapping

Game names and launch product codes come from embedded **`Providers/BattleNet/Resources/BattleNetGames.json`** (GameLib catalog + newer titles). New Battle.net releases may require updating this file.

## Installation

```
Battle.net.exe --exec="install {ProductId}"
```

Example: `--exec="install D3"` for Diablo III.

## Limitations

- No official Blizzard API key flow; account sync uses the same browser session approach as other community tools
- `product.db` alone does not prove ownership for titles another user installed on the same PC (same caveat as Playnite)
- WoW variants and classic splits may need manual catalog updates
- macOS/Linux are not supported for Battle.net integration in OpenGameHUB
