# Epic Games and legendary

## Why legendary

Epic does not publish a REST API for "my library" for third-party applications. Real options:

1. **legendary** (open source CLI) — used by the community and alternative launchers
2. Launcher scraping — fragile and may violate ToS
3. Local manifests only — installed games only, no cloud library

OpenGameHUB chooses **legendary** to list owned games and authenticate, with installation delegated to the **official Epic Games Launcher**.

---

## Components

| File | Role |
|------|------|
| `Services/LegendaryClient.cs` | Wrapper around the legendary executable |
| `Services/Epic/LegendaryBootstrap.cs` | Ensures legendary exists on disk |
| `Services/Epic/EpicAuthHelper.cs` | Copies auth from legendary → `settings.json` |
| `Services/Epic/EpicLauncherClient.cs` | Opens install protocol URL |
| `Services/Epic/EpicManifestScanner.cs` | Installed Epic games via `.item` JSON |
| `Services/LibraryProviders/EpicCloudLibraryProvider.cs` | Cloud library |

---

## Obtaining legendary

Search order (`LegendaryClient.FindExecutable`):

1. In-memory cache
2. Bundled: `{app}/tools/legendary.exe` (optional in publish)
3. Managed: `%LocalAppData%\OpenGameHUB\tools\legendary.exe`
4. `where legendary` on PATH
5. Known paths (pip, pipx, `.local/bin`, etc.)

If missing, `LegendaryBootstrap.EnsureInstalledAsync` downloads from:

`https://github.com/derrod/legendary/releases/latest/download/legendary.exe`

**Why bundled .exe:** Windows users without Python can use the app without installing legendary manually.

---

## Epic authentication

### Connect (Settings)

**Preferred (WebView2 available):**

```
LegendaryBootstrap.EnsureInstalledAsync
  → EmbeddedBrowserService + EpicAuthCaptureStrategy
  → User signs in on legendary.gl / epicgames.com (allowlisted hosts only)
  → App captures authorizationCode from Epic redirect response
  → LegendaryClient.RunAuthWithCodeAsync(code)
  → Credentials in ~/.config/legendary/user.json
  → EpicAuthHelper.PersistFromLegendary
```

**Fallback (no WebView2 runtime):**

```
LegendaryClient.RunAuth()   // hidden process, legendary opens browser
```

See [auth-browser-security.md](auth-browser-security.md) for the OAuth browser threat model.

### Persistence in OpenGameHUB

`EpicAuthHelper.PersistFromLegendary` reads `user.json` and stores:

- `EpicAccountId`
- `EpicDisplayName`

**Why duplicate in settings:** the UI can show connected state without reading JSON on every binding; `HasEpicAuth` complements `LegendaryClient.HasStoredCredentials()`.

### Disconnect

```
LegendaryClient.ClearStoredCredentials()  // deletes user.json, entitlements, metadata
EpicAuthHelper.Clear(settings)
LegendaryClient.RunDisconnectAsync()    // legendary auth --delete (8 s timeout)
```

Order: clear local first so the UI responds even if legendary hangs.

### When is the prompt offered?

`ShouldOfferLegendaryPrompt` in `GameLibraryService`:

- Epic Launcher installed
- legendary available
- No credentials and no `HasEpicAuth`
- User did not check "Don't remind me" (`DismissLegendaryPrompt`)

---

## Cloud library

```
legendary list --json
  → ParseCatalogJson
  → LegendaryCatalogEntry(AppName, AppTitle, CatalogNamespace, CatalogItemId)
```

Filters DLC (`is_dlc`). `namespace` + `id` metadata feed the install URL.

`EpicCloudLibraryProvider` excludes games already installed (by `app_name` or normalized title).

---

## Installation: protocol vs legendary

### Primary path (UI)

`MainWindowViewModel.LaunchSelectedGame`:

```
If Epic + not installed + LaunchSpec.protocol + Epic Launcher installed:
  EpicLauncherClient.StartInstall(url)
  // UseShellExecute = true → Windows opens the protocol
  // App shows message and does NOT wait
```

Typical URL:

```
com.epicgames.launcher://apps/{namespace}%3A{catalogId}%3A{appName}?action=install
```

**Why `%3A`:** URL encoding of `:` in Epic's composite identifier (Sandbox:Catalog:Artifact).

### Fallbacks (`GetInstallLaunchAttempts`)

1. `legendary install {appName}`
2. Direct protocol
3. EpicGamesLauncher.exe with URL as argument
4. `legendary launch {appName}`

For when the protocol is not registered or the launcher does not respond.

### Why we do NOT use legendary install as the primary path

- Download managed by OpenGameHUB/legendary, not Epic → uninstall and path issues
- UI blocked during large downloads
- User explicitly asked for Epic to install and the app to stay free

`LegendaryInstallProgress.cs` exists as possible future UI but is **not connected** to the current flow.

---

## Installed Epic games (manifests)

`EpicManifestScanner` reads:

```
C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests\*.item
```

JSON with `AppName`, install paths, executable.

**Why in addition to GameLib:** GameLib sometimes misses Epic titles; manifests are the launcher's canonical source.

---

## Launch installed games

- If manifest has executable → `LaunchSpec.Executable`
- Otherwise → protocol `com.epicgames.launcher://apps/{appName}?action=launch&silent=true`

---

## Credentials: legendary files

Directory: `%USERPROFILE%\.config\legendary\` (or `LEGENDARY_CONFIG_PATH`)

| File | Contents |
|------|----------|
| `user.json` | Account, tokens (legendary) |
| `entitlements.json` | Game entitlements |
| `metadata.json` | Catalog metadata |

OpenGameHUB **does not upload** these files to any server; it only uses them locally via CLI.
