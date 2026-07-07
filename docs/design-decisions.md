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
- EA catalog: `ea:catalog:{softwareId}@{slug}`

**Reason:** favorites and covers in SQLite link by `id`. Changing the ID algorithm would break existing favorites.

---

## `LaunchSpec`: three launch types

| Kind | Use |
|------|-----|
| `protocol` | `steam://`, `com.epicgames.launcher://`, `uplay://`, etc. |
| `executable` | Direct path to `.exe` |
| `launcher-args` | `{launcher_path}\|{arguments}` |

**Reason:** persist in SQLite **how** to launch without coupling the DB to per-platform logic. Interpretation lives in `GameLibraryService`.

---

## Cover metadata in layers

**Typical order:** local cover in install folder → disk cache → Steam CDN (if AppId) → IGDB / SteamGridDB / Wikipedia / Riot maps.

**Reason:** minimize paid/rate-limited API calls; work without configuration (Steam CDN and Wikipedia as safety net).

API keys (IGDB, SteamGridDB) are **optional** in Settings.

---

## EA: encrypted cache + logs

**Decision:** try to decrypt EA Desktop `InstallInfo` (GameFinder + GPU hardware fingerprint); if that fails, parse EA logs.

**Reason:** EA does not expose a public library API. Local cache is the only reliable channel; logs are a documented fallback in code.

The user can open EA App from an onboarding prompt to force cache refresh (`EaDesktopSyncHelper`). See [ea-desktop.md](ea-desktop.md).

---

## Game Pass install: resolve Store big-id from PFN at click time

**Decision:** when pressing Install on an uninstalled Game Pass game, resolve the current
Store product "big-id" from the package family name (PFN) via the public display-catalog
lookup, then open `msxbox://game/?productId={bigId}`.

**Reason:** the Xbox title-history API only exposes **legacy numeric** product ids
(`windowsPhoneProductId` / `modernTitleId`, e.g. `2080211397`). The Xbox app deep link
expects the modern Store big-id (e.g. `9MWR1NC6VQ6L`); the legacy id makes the app open but
show *"We couldn't load the content. Confirm you have permission…"*. The PFN is stored
reliably, so we map PFN → big-id on demand.

**Implementation:**

- `XboxInstallClient.ResolveStoreProductIdAsync(pfn)` → `GET displaycatalog.mp.microsoft.com/v7.0/products/lookup?alternateId=PackageFamilyName&value={pfn}` (public, no auth)
- Called once at install-click time from `MainWindowViewModel` (not during refresh)
- On failure returns `null`; `StartInstall` falls back to `ms-windows-store://pdp/?PFN={pfn}`
- Protocol launches via `UseShellExecute` no longer treat a `null` `Process.Start` result as
  failure (ShellExecute hands the URI to the packaged app and returns `null` on success)

See [xbox.md](xbox.md#store-product-id-resolution-install-fix).

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
