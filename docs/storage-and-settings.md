# Storage and settings

## Disk paths

Everything under the Windows user profile (no admin):

| Path | Contents | Written by |
|------|----------|------------|
| `%LocalAppData%\OpenGameHUB\settings.json` | Preferences | `SettingsService` |
| `%LocalAppData%\OpenGameHUB\library.db` | SQLite library | `GameDatabase` |
| `%LocalAppData%\OpenGameHUB\covers\` | JPEG/PNG covers | `MetadataService` |
| `%LocalAppData%\OpenGameHUB\tools\legendary.exe` | legendary CLI | `LegendaryBootstrap` |
| `%TEMP%\OpenGameHUB\updates\` | Downloaded installer | `AppUpdateService` (temporary) |

Program installation (Inno Setup):

```
%LocalAppData%\Programs\OpenGameHUB\
```

## `SettingsService`

File: `Services/SettingsService.cs`

- Loads JSON on startup (`Current`)
- `Save(AppSettings)` — atomic write to disk
- If file missing → `AppSettings` defaults

Does not encrypt JSON: API keys are stored in plain text on the user's PC (like many desktop clients). **Do not commit** `settings.json`.

## `AppSettings` fields

See [data-model.md](data-model.md).

### "Dismiss*Prompt" flags

Prevent repeating onboarding. Dev mode **resets** these flags (`DevModeService.ResetConnectionSettings`) for testing.

### Epic in settings vs legendary

| Source | Data |
|--------|------|
| `user.json` (legendary) | Real tokens for CLI |
| `settings.json` | `EpicAccountId`, `EpicDisplayName` for UI |

If they drift apart, `HasStoredCredentials()` and `HasEpicAuth` may diverge — on refresh, `EpicAuthHelper.PersistFromLegendary` re-syncs from legendary.

## `library.db`

- Created automatically on first startup
- `CREATE TABLE IF NOT EXISTS` in `GameDatabase.Initialize`
- Safe deletion in dev: `DevModeService.ClearLocalLibraryCache` or `GameLibraryService.ResetLocalCache` (reopens connection)

**What is NOT lost when deleting library.db:** `settings.json`, legendary credentials, installers in `Programs\OpenGameHUB`.

**What IS lost:** favorites, cached playtime, cover paths in DB (files in `covers/` are also deleted on dev clear cache).

## Covers

`CoverPathHelper.CacheDirectory` → `covers/`

Names derived from game `Id` (filesystem-safe hash).

`LocalCoverScanner` can also point to images inside `InstallPath` without copying them.

## Transparency for the user

The app only reads/writes the paths above and standard launcher folders (Steam, Epic, EA, Ubisoft). It does not send library or credentials to OpenGameHUB servers (none exist). The only optional network traffic is user-configured APIs and GitHub Releases for updates.
