# Xbox PC / Game Pass

OpenGameHUB supports **installed** Xbox / Game Pass titles (local Appx scan) and an optional **cloud library** when the user connects a Microsoft account (same approach as Playnite's Xbox plugin: Xbox Live title history API).

---

## Components

| File | Role |
|------|------|
| `Services/XboxGamePassScanner.cs` | Installed packages via GameFinder |
| `Services/Xbox/XboxManifestReader.cs` | Appx manifest metadata + logos |
| `Services/Xbox/XboxAccountClient.cs` | OAuth tokens, Xbox Live / title hub API |
| `Services/Xbox/XboxAuthService.cs` | Sign-in flow (embedded WebView2 or paste fallback) |
| `Services/Xbox/XboxTokenStore.cs` | Encrypted token files (DPAPI) |
| `Services/Xbox/XboxInstallClient.cs` | Install URIs (`msxbox://`, Store PDP) |
| `Services/LibraryProviders/XboxCloudLibraryProvider.cs` | Uninstalled catalog entries |
| `Services/GameLibraryService.cs` | Scanner + cloud provider orchestration |

**Dependencies:** [GameFinder.StoreHandlers.Xbox](https://www.nuget.org/packages/GameFinder.StoreHandlers.Xbox) (installed scan), Xbox Live REST APIs (cloud).

---

## Installed games

```
RefreshLibraryAsync
  └─ ScanAllGames
       └─ XboxGamePassScanner.Scan()
            ├─ XboxHandler.FindAllGames()
            ├─ AppxManifest.xml validation
            └─ UnifiedGame (Platform.GamePass, LaunchSpec, CatalogCoverUrl)
```

Stable ID: `gamepass:path:{sha256(installPath)[:16]}`  
`PlatformGameId` = PFN / GameFinder id when available.

Launch order: manifest exe → `shell:AppsFolder\...` → `XboxPcApp.exe` + ApplicationId.

See [Launch strategies](#launch-strategies) and [Manifest parsing](#manifest-parsing-xboxmanifestreader) below.

---

## Cloud library (Microsoft account)

### User flow

1. **Settings** → *Xbox / Game Pass library* → **Connect Microsoft account**
2. Embedded WebView2 opens Microsoft OAuth (isolated profile, allowlisted hosts). The app captures `code=` from the redirect automatically.
3. **Fallback** if WebView2 is unavailable: system browser + paste redirect URL into `XboxPasteAuthWindow`.
4. Tokens saved under `%LocalAppData%\OpenGameHUB\xbox\` (DPAPI-encrypted)
5. **Refresh library** → `titlehub.xboxlive.com` title history → PC games not installed appear as `IsInstalled = false`

See [auth-browser-security.md](auth-browser-security.md) for OAuth browser controls.

Disconnect: Settings → **Disconnect Microsoft account** (deletes tokens + gamertag in settings).

### API chain (after OAuth code exchange)

1. `login.live.com/oauth20_token.srf` → Microsoft access + refresh token  
2. `user.auth.xboxlive.com/user/authenticate` → user token  
3. `xsts.auth.xboxlive.com/xsts/authorize` → XSTS token (saved)  
4. `titlehub.xboxlive.com/users/xuid({xuid})/titles/titlehistory/decoration/detail` → library  
5. Optional: `userstats.xboxlive.com/batch` → `MinutesPlayed` per title  

Uses the same public OAuth client id as community tools (Playnite Xbox plugin). **Not an official third-party API** — Microsoft may change or block it.

### Cloud catalog entries

| Field | Source |
|-------|--------|
| `Id` | `gamepass:catalog:{pfn}` |
| `PlatformGameId` | PFN (package family name) |
| `Title` | Title hub `name` (normalized) |
| `PlaytimeMinutes` | User stats API when available |
| `LaunchSpec` | `msxbox://game/?productId=…` or `ms-windows-store://pdp/?PFN=…` |

Filtering:

- `type == "Game"` and `devices` contains `"PC"`
- Skips titles already installed (match PFN or normalized title)

### Limitations (documented by Playnite)

- May **not** list every Game Pass title you can install from the Xbox app
- Games never started on PC may be missing from title history
- EA Play on Game Pass may not appear
- Auth can break if Microsoft changes OAuth — refresh tokens mitigate short-term expiry

---

## Installing Game Pass games

### Installed games

Launch only (no install action).

### Cloud entries (`IsInstalled = false`)

`MainWindowViewModel` (Game Pass branch) → `XboxInstallClient`:

| URI | Purpose |
|-----|---------|
| `msxbox://game/?productId={id}` | Xbox PC app product page |
| `ms-windows-store://pdp/?ProductId={id}` | Microsoft Store |
| `ms-windows-store://pdp/?PFN={pfn}` | Store by package family name |
| Open `XboxPcApp.exe` | Manual fallback |
| `ms-windows-store://navigatetopage/?Id=Gaming` | Store gaming hub |

OpenGameHUB does **not** download games silently — the user confirms install in Microsoft's UI. Refresh the library after install completes.

#### Store product id resolution (install fix)

The `productId` baked into the cloud entry's `LaunchSpec` comes from the title-history
fields (`windowsPhoneProductId` / `modernTitleId`), which are **legacy numeric ids**
(e.g. `2080211397`). The Xbox app's `msxbox://game/?productId=…` deep link expects the
current **Store "big-id"** instead (e.g. `9MWR1NC6VQ6L`). Passing the legacy id makes the
Xbox app open but fail with *"We couldn't load the content. Confirm you have permission to
view this product…"*.

Fix: when the user clicks **Install** on a Game Pass game, we resolve the real big-id from
the reliably-known **PFN** before launching:

```
XboxInstallClient.ResolveStoreProductIdAsync(pfn)
  └─ GET displaycatalog.mp.microsoft.com/v7.0/products/lookup
         ?market={m}&languages={l}&alternateId=PackageFamilyName&value={pfn}
  └─ Products[0].ProductId  → e.g. 9MWR1NC6VQ6L
```

- Public display-catalog endpoint, **no authentication** required.
- Runs once, only at install-click time (not during library refresh).
- On any failure (network, no match) it returns `null`, and `XboxInstallClient.StartInstall`
  falls back to the PFN-based Store PDP (`ms-windows-store://pdp/?PFN={pfn}`).

> **Protocol launch note:** `Process.Start` with `UseShellExecute = true` hands protocol
> URIs to the packaged Store/Xbox app and often returns `null` *even on success*, so
> `XboxInstallClient` no longer treats a `null` return as a failure — only a thrown
> `Win32Exception` (e.g. an unregistered protocol) counts as one.

---

## Manifest parsing (`XboxManifestReader`)

| Field | XML source | Use |
|-------|------------|-----|
| Title | `Properties/DisplayName` | `UnifiedGame.Title` |
| Executable / ApplicationId | `Applications/Application` | Launch |
| Cover | `Square150x150Logo`, `Logo`, … | `CatalogCoverUrl` (local path) |

`XboxManifestReader.EnrichCatalogCoverUrls` backfills covers after DB load.

---

## Launch strategies

1. **Executable** — manifest exe or largest non-utility `.exe`  
2. **Shell URI** — `shell:AppsFolder\{PackageFamilyName}!{AppId}` via PowerShell `Get-AppxPackage`  
3. **Xbox PC App** — `XboxPcApp.exe` + ApplicationId  
4. Fallback executable path

---

## Storage

| Path | Content |
|------|---------|
| `%LocalAppData%\OpenGameHUB\xbox\login.dat` | OAuth tokens (DPAPI) |
| `%LocalAppData%\OpenGameHUB\xbox\xsts.dat` | XSTS session (DPAPI) |
| `settings.json` → `XboxGamertag` | Display only (UI) |

---

## Settings UI

`SettingsWindow` — section **Xbox / Game Pass library** (always visible on Windows):

- Connect / disconnect Microsoft account
- Status text with gamertag when known

---

## Legal / risk

| Aspect | Level |
|--------|-------|
| Installed Appx scan | Low |
| Cloud via Xbox Live APIs + community OAuth client | Medium — undocumented for third parties; user-provided account |

---

## Related docs

- [cloud-providers.md](cloud-providers.md) — `XboxCloudLibraryProvider`
- [platform-integrations.md](platform-integrations.md) — summary table
- [metadata-and-covers.md](metadata-and-covers.md) — manifest logos
