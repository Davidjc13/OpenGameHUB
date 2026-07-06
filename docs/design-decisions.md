# Design decisions

This document captures the **why** behind decisions visible in the code. It is not a future roadmap.

## SQLite as local library

**Decision:** persist the unified library in `%LocalAppData%\OpenGameHUB\library.db`.

**Reasons:**

- Fast startup: `LoadCachedGames()` before any network or disk scan
- Keep favorites, cover paths, and playtime across sessions
- Single portable file, easy to delete (dev mode: "Clear local database")
- No dependency on a proprietary cloud service

**Trade-off:** scanning must be synced with the DB (`SyncScannedGames` deletes rows no longer found in scans, but re-applies saved favorites and covers).

---

## GameLib.NET as primary scanner

**Decision:** GameLib's `LauncherManager` scans Steam, Epic, GOG, Ubisoft, EA App, Battle.net, Rockstar, and Riot.

**Reasons:**

- Avoids reimplementing detection for each launcher
- Options: `LoadLocalCatalogData = true`, `SearchExecutables = true`, `QueryOnlineData = false` (no dependency on GameLib online APIs)

**Trade-off:** GameLib does not cover everything (Epic manifests in `ProgramData`, EA via registry/XML, Xbox Appx). Complementary scanners exist in `Services/`.

---

## `ICloudLibraryProvider` pattern

**Decision:** common interface for **owned but not installed** games + install strategies.

**Reasons:**

- Each platform gets its cloud library differently (API, legendary, encrypted local cache, logs)
- `GameLibraryService` does not need a giant `switch (platform)` for cloud
- Adding a platform = new implementation + register in constructor

**File:** `Services/LibraryProviders/ICloudLibraryProvider.cs`

---

## legendary for Epic (library and auth)

**Decision:** use the [legendary](https://github.com/derrod/legendary) CLI to list the Epic library and authenticate.

**Reasons:**

- Epic does not offer a public "my games" API for third-party apps
- legendary is the community reference tool (open source)
- Auto-downloads to `%LocalAppData%\OpenGameHUB\tools\legendary.exe` if not in PATH or bundled

**Credentials:** `%USERPROFILE%\.config\legendary\user.json` (not `config.json`). Mirrors what legendary does; OpenGameHUB also stores `EpicAccountId` / `EpicDisplayName` in `settings.json` for the UI.

---

## Install Epic via Epic Games Launcher (protocol), not via `legendary install`

**Decision:** when pressing Install on an uninstalled Epic cloud game, open a `com.epicgames.launcher://apps/...?action=install` URL.

**Reasons (explicit in the product):**

- The official launcher handles DRM, updates, and uninstallation
- `legendary install` downloaded to paths Epic did not always recognize well
- The app must not block waiting for GB of download — it only triggers the installer and continues

**Implementation:**

- `EpicCloudLibraryProvider` assigns `LaunchSpec.Protocol(protocolUrl)` with namespace:catalog:app from legendary
- `MainWindowViewModel.LaunchSelectedGame` detects Epic + protocol and calls `EpicLauncherClient.StartInstall`
- `GetInstallLaunchAttempts` still includes `legendary install` as a **fallback** if the protocol fails

---

## Deduplication and platform priority

**Decision:** if two entries share an install path or normalized title, keep the one with higher "priority".

**Approximate order:** Riot > Steam > Ubisoft > EA > … > Epic (lowest).

**Reason:** Epic often appears as a "wrapper" for games that actually belong to Riot, Ubisoft, EA, or Battle.net (`IsEpicWrapperForNativeLauncher`).

---

## Stable IDs

**Examples:**

- Installed: `{platform}:path:{hash}` from normalized path
- Steam cloud: `steam:{appId}`
- Epic cloud: `epic:legendary:{appName}`
- GOG cloud: `gog:catalog:{releaseKey}`
- EA catalog: `ea:catalog:{softwareId}@{slug}`

**Reason:** favorites and covers in SQLite link by `id`. Changing the ID algorithm would break existing favorites.

---

## `LaunchSpec`: three launch types

| Kind | Use |
|------|-----|
| `protocol` | `steam://`, `com.epicgames.launcher://`, `uplay://`, `goggalaxy://`, etc. |
| `executable` | Direct path to `.exe` |
| `launcher-args` | `{launcher_path}\|{arguments}` |

**Reason:** persist in SQLite **how** to launch without coupling the DB to per-platform logic. Interpretation lives in `GameLibraryService`.

---

## Cover metadata in layers

**Typical order:** local cover in install folder → disk cache → catalog URL (Steam / Ubisoft / GOG CDN or Galaxy webcache) → IGDB / SteamGridDB / Wikipedia / Riot maps.

**Reason:** minimize paid/rate-limited API calls; work without configuration (Steam CDN and Wikipedia as safety net).

API keys (IGDB, SteamGridDB) are **optional** in Settings.

---

## EA: encrypted cache + logs

**Decision:** try to decrypt EA Desktop `InstallInfo` (GameFinder + GPU hardware fingerprint); if that fails, parse EA logs.

**Reason:** EA does not expose a public library API. Local cache is the only reliable channel; logs are a documented fallback in code.

The user can open EA App from an onboarding prompt to force cache refresh (`EaDesktopSyncHelper`). See [ea-desktop.md](ea-desktop.md).

---

## GOG: local Galaxy database (no account linking)

**Decision:** read owned games from `galaxy-2.0.db` instead of scraping gog.com or bundling gogdl/comet.

**Reasons:**

- GOG has no public desktop library API for third parties
- The SQLite file is already on disk after the user signs in to GOG Galaxy
- Same data model used by community export tools; no extra credentials in OpenGameHUB

**Install:** delegate to GOG Galaxy via `goggalaxy://openGameView/{releaseKey}` (and `installGame` CLI when Galaxy is running), same rationale as Epic — official client handles downloads and DRM.

**Covers:** read `originalImages` URLs from the database; fall back to Galaxy's local webcache before hitting IGDB/SteamGridDB.

See [gog-galaxy.md](gog-galaxy.md).

---

## Updater via GitHub Releases

**Decision:** download `OpenGameHUB-Setup-*.exe` and run Inno Setup silently.

**Reasons:**

- No proprietary update server
- Same installer as manual distribution
- Version comparison by tag (`alpha-0.0.7`, etc.)

**Limitation:** only installed builds with embedded version; `dev` / `dotnet run` do not get automatic startup notice.

---

## Windows only (for now)

**Decision:** `net8.0-windows`, Registry, `C:\` paths, Xbox Appx, `.exe` installer.

**Reason:** target launchers and their data formats are from the Windows PC ecosystem. Avalonia could be ported, but each integration would need to be redone (see README).

---

## Developer mode

**Decision:** visible in DEBUG or with `OPENGAMEHUB_DEV=1`.

**Reason:** test onboarding, connection reset, and `library.db` deletion without manual scripts. Must not appear in end-user builds by default.
