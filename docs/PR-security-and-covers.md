# Secure secrets, safer downloads, cover & sync fixes

## Summary

- **API keys** (`SteamApiKey`, `IgdbClientSecret`, `SteamGridDbApiKey`) move from plain `settings.json` to DPAPI-encrypted `secrets.dat` (same as Xbox tokens). Auto-migrates old plaintext settings; versioned format (`v1`) for future changes.
- **Cover downloads** accept only `https://` (except localhost). Ubisoft `http://` catalog URLs normalized to `https://` at read time.
- **Library refresh** no longer deletes cloud catalog entries when a provider fails transiently (Steam API, Ubisoft, etc.).
- **Steam & EA covers** load on demand in the UI; stale `cover_path` entries in DB are cleared; EA gets dedicated cover lookup via Steam Store.

## Key files

`SettingsSecretsStore`, `SettingsService`, `SafeImageDownloader`, `UbisoftCatalogReader`, `GameLibraryService`, `MetadataService`, `EaCoverClient`, `MainWindowViewModel`

## Test plan

- [ ] Secrets in `secrets.dat`, not in `settings.json`; migration from old install works
- [ ] Steam / EA covers appear when browsing the library
- [ ] Refresh with Steam API down → cloud games not wiped
- [ ] Remote `http://` image URLs rejected; Ubisoft covers still work

## PR title

```
Secure API secrets (DPAPI), harden cover downloads, and fix Steam/EA cover loading
```
