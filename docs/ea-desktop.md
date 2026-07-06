# EA Desktop and Origin

## Why no public API

EA does not expose a REST API for "my library" for third-party applications. Real options:

1. **Encrypted local cache** — EA Desktop writes `InstallInfo` to disk, tied to PC hardware
2. **Log parsing** — EA Desktop logs `installInfo` updates when the library syncs
3. **Registry + installer metadata** — installed games only, no owned-but-uninstalled list

OpenGameHUB uses **all three layers**: registry/XML for installed titles, decrypted cache for the cloud library, and logs as fallback when decryption fails. Install and launch are delegated to **EA Desktop / Origin** via `origin2://` and `link2ea://` protocols.

There is **no EA account linking** in OpenGameHUB — the user must already be signed in to EA Desktop on the same PC.

---

## Components

| File | Role |
|------|------|
| `Services/EaDesktopScanner.cs` | Installed EA/Origin games via registry and `installerdata.xml` |
| `Services/Ea/EaCatalogReader.cs` | Cloud library orchestration, cache status, entry filtering |
| `Services/Ea/EaInstallInfoDecryptor.cs` | Decrypt EA `InstallInfo` file (GameFinder + GPU fingerprint) |
| `Services/Ea/EaLogCatalogReader.cs` | Fallback: parse EA Desktop log lines |
| `Services/Ea/EaLibraryCacheStatus.cs` | Cache availability enum |
| `Services/Ea/EaDesktopSyncHelper.cs` | Launch EA Desktop, wait for sync after onboarding |
| `Services/LibraryProviders/EaCloudLibraryProvider.cs` | `ICloudLibraryProvider` for uninstalled owned games |
| `Views/EaLibraryPromptWindow.axaml` | Onboarding when cache cannot be read |
| `ViewModels/EaLibraryPromptViewModel.cs` | Prompt logic and user choice |

GameLib's `LauncherManager` also detects EA App installs; `EaDesktopScanner` complements it with registry paths GameLib may miss.

---

## Installed games: `EaDesktopScanner`

### Data sources

| Source | Path / key | What it provides |
|--------|------------|------------------|
| EA Games registry | `HKLM\SOFTWARE\EA Games`, `WOW6432Node\EA Games` | `Install Dir` per subkey |
| Origin registry | `HKLM\SOFTWARE\WOW6432Node\Origin Games` | `DisplayName`, offer ID |
| Installer metadata | `{installDir}\__Installer\installerdata.xml` | Title, `contentID`, launcher executable |
| EA Games folders | `C:\Program Files\EA Games`, etc. | Match by `contentID` when registry is incomplete |

### Stable IDs and launch

- ID pattern: `ea:path:{16-char-sha256-of-normalized-path}`
- `LaunchSpec` priority:
  1. Direct `.exe` from `installerdata.xml` or largest non-utility exe in install folder
  2. `link2ea://launchgame/contentids/{contentId}`
  3. Launch EA Desktop executable with empty args

Utility executables (`unins`, `setup`, `redist`, `eac`) are excluded when picking a fallback exe.

---

## Cloud library: `EaCatalogReader`

### InstallInfo cache file

```
%ProgramData%\EA Desktop\530c11479fe252fc5aabc24935b9776d4900eb3ba58fdc271e0d6229413ad40e\IS
```

Encrypted JSON containing `installInfos[]` for every game on the account. EA Desktop creates and updates this file after library sync.

### Cache status (`EaLibraryCacheStatus`)

| Status | Meaning | Cloud library |
|--------|---------|---------------|
| `NotInstalled` | No EA Desktop / Origin executable found | No |
| `NoCache` | EA installed but `IS` file missing | No (prompt offered) |
| `Available` | Cache decrypted successfully | Yes (cache + logs merged) |
| `DecryptFailedUsingLogs` | Decryption failed; logs have entries | Yes (logs only) |
| `Unavailable` | `IS` exists but neither decrypt nor logs work | No (prompt offered) |

`GetCacheStatus()` is cached in memory until `InvalidateCache()` — called before each EA cloud scan and during sync wait.

`IsCloudLibraryAvailable()` is true for `Available` or `DecryptFailedUsingLogs`.

### Decryption: `EaInstallInfoDecryptor`

Uses GameFinder's `EADesktopHandler.DecryptInstallInfoFile` (invoked via reflection). The encryption key is derived from **hardware fingerprints**, especially the GPU (`Win32_VideoController` PNP device IDs via WMI).

Flow:

1. Try default `HardwareInfoProvider`
2. For each additional GPU in the system, retry with `OverrideVideoControllerHardwareInfoProvider`
3. Accept plaintext only if it contains `"installInfos"`

If every candidate fails, `EaCatalogReader` falls back to logs.

### Filtering decrypted entries

From `installInfos`, only games that match **all** of these are included:

- `installStatus == 0` (not installed)
- No existing `baseInstallPath` on disk
- Valid `baseSlug` (kebab-case slug; not a GUID or internal ID like `Origin.SFT.*`)
- Not listed as DLC of another entry
- Slug does not look like DLC/addon (`-dlc`, `-addon`, `-expansion`, `-season-pass`, `-upgrade`)

Title is derived from slug via `SlugToTitle` (e.g. `fifa-24` → `FIFA 24`).

### Log fallback: `EaLogCatalogReader`

Reads (shared read, newest first):

```
%LocalAppData%\Electronic Arts\EA Desktop\Logs\EADesktop.log
%LocalAppData%\Electronic Arts\EA Desktop\Logs\EADesktop.bak
%LocalAppData%\Electronic Arts\EA Desktop\Logs\EADesktopVerbose.log
%LocalAppData%\EADesktop\Logs\...
```

Parses lines matching:

```
IS update: set installInfo for softwareId=[...] baseSlug=[...] installedStatus=[NotInstalled]
```

Keeps the latest snapshot per `baseSlug`. Same slug/DLC filters as the cache path.

When cache decrypt works, log entries are **merged** into cache entries; `PreferCatalogEntry` resolves duplicates (prefers `Origin.SFT.*` > `SIMS*` > other IDs > GUIDs).

### Matching installed vs cloud

`MatchesInstalledGame` excludes cloud entries already installed:

- Same `PlatformGameId` / `softwareId`
- Normalized title match
- Compact alphanumeric title match (ignores spaces/punctuation)

---

## Cloud provider: `EaCloudLibraryProvider`

Implements `ICloudLibraryProvider`:

| Method | Behavior |
|--------|----------|
| `IsAvailable()` | `EaCatalogReader.IsCloudLibraryAvailable()` |
| `GetUninstalledLibraryGames` | `ReadLibraryEntries()` minus installed matches |
| `GetInstallLaunchAttempts` | Protocol chain for install |

### Cloud game IDs

```
ea:catalog:{softwareId}@{baseSlug}
```

Example: `ea:catalog:Origin.SFT.50.0001455@fifa-24`

`LaunchSpec`: `origin2://game/launch/?offerIds={softwareId}`

### Install attempt chain

1. `origin2://game/launch/?offerIds={softwareId}`
2. `origin2://game/launch/?offerIds={baseSlug}` (from game id suffix)
3. `link2ea://launchgame/contentids/{softwareId}`
4. `EADesktop.exe` with protocol URL as argument
5. `cmd.exe /c start` fallback if shell execute fails

---

## Integration in `GameLibraryService`

On every `ScanAllGames`:

```
games.AddRange(EaDesktopScanner.Scan());

foreach cloud provider:
  if Platform.Ea:
    EaCatalogReader.InvalidateCache()
  GetUninstalledLibraryGames(currentGames)  // try/catch, silent on failure
```

Deduplication priority for EA: **88** (above Epic, below Ubisoft) — reduces duplicates when Epic wraps an EA-native title.

Installed EA games from GameLib also get launch fallbacks in `LaunchGame`:

- `link2ea://launchgame/contentids/{platformGameId}`
- `origin2://game/launch?offerIds={platformGameId}`

---

## Onboarding: `EaLibraryPromptWindow`

Shown once per session when:

- `EaCatalogReader.ShouldOfferLibraryPrompt()` — status is `NoCache` or `Unavailable`
- User has not set `DismissEaLibraryPrompt` in `settings.json`
- Prompt not already offered this session

### User choices

| Choice | Action |
|--------|--------|
| Open EA app | `LaunchEaAndRefreshLibraryAsync` (see below) |
| Continue anyway | Dismiss; only installed EA games until cache works |
| Don't ask again | Sets `DismissEaLibraryPrompt` |

If status is `DecryptFailedUsingLogs`, the prompt explains that log-based list may be incomplete.

### Sync wait flow (`LaunchEaAndRefreshLibraryAsync`)

```
InvalidateCache → record baseline status + log entry count
LaunchEaDesktop()
Wait up to 45 s for EADesktop.exe or Origin.exe process
WaitForLibraryUpdateAsync (up to 90 s, poll every 2 s):
  - Success if status becomes Available (was not before)
  - Or log entry count increases for 2 consecutive polls
RefreshLibraryCommand
```

`EaDesktopSyncHelper` reports progress via `Loc.T("WaitingEaLibrarySync")`.

---

## EA Desktop detection

`EaCatalogReader.FindEaDesktopExecutable()` checks:

1. `C:\Program Files\Electronic Arts\EA Desktop\EA Desktop\EADesktop.exe`
2. `C:\Program Files\Electronic Arts\EA Desktop\EADesktop.exe`
3. `C:\Program Files (x86)\Origin\Origin.exe`
4. Registry `HKLM\SOFTWARE\WOW6432Node\Origin` → `ClientPath`

---

## Limitations

- **Windows only** — registry, WMI, `ProgramData` paths, EA protocols
- **No playtime** — unlike Steam Web API
- **Hardware-bound cache** — decryption fails after GPU change, VM migration, or corrupted cache; user must re-sync EA Desktop
- **Log fallback is best-effort** — may miss games or show stale `NotInstalled` state
- **No OpenGameHUB EA credentials** — relies entirely on EA Desktop already being logged in
- **Official launcher required** — install/launch opens EA Desktop; OpenGameHUB does not download game files

---

## Related documents

- [platform-integrations.md](platform-integrations.md) — EA row in platform summary table
- [cloud-providers.md](cloud-providers.md) — `ICloudLibraryProvider` pattern
- [design-decisions.md](design-decisions.md) — why encrypted cache + logs
- [windows-specific.md](windows-specific.md) — registry and EA decryption dependencies
