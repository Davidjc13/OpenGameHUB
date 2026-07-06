# OpenGameHUB Documentation

Transparent technical documentation: what each piece does, where it gets data from, and why that approach was chosen.

> The app is **Windows 10/11** for now. There is no Linux support.

## Index

| Document | Contents |
|----------|----------|
| [architecture.md](architecture.md) | Overview, startup, main flows |
| [design-decisions.md](design-decisions.md) | Why SQLite, legendary, Epic protocols, etc. |
| [data-model.md](data-model.md) | `UnifiedGame`, `LaunchSpec`, SQLite schema |
| [platform-integrations.md](platform-integrations.md) | Steam, Epic, GOG, EA, Ubisoft, Xbox… by source type |
| [cloud-providers.md](cloud-providers.md) | `ICloudLibraryProvider` pattern |
| [epic-and-legendary.md](epic-and-legendary.md) | Auth, cloud library, installation |
| [gog-galaxy.md](gog-galaxy.md) | Local DB library, installation, covers |
| [ea-desktop.md](ea-desktop.md) | Encrypted cache, logs, registry scan, onboarding |
| [metadata-and-covers.md](metadata-and-covers.md) | Cover pipeline and optional APIs |
| [ui-and-viewmodels.md](ui-and-viewmodels.md) | Avalonia, MVVM, windows and onboarding |
| [storage-and-settings.md](storage-and-settings.md) | Disk paths, `settings.json` |
| [app-updater.md](app-updater.md) | GitHub Releases update checks |
| [windows-specific.md](windows-specific.md) | Registry, paths, Inno Setup installer |
| [developer-mode.md](developer-mode.md) | Debug tools and reset |

## Project principles

1. **No affiliation** — We are not Steam, Epic, EA, etc. We use launchers and local data/public APIs where they exist.
2. **Official launchers to play** — The app lists games and opens install/launch flows; it does not replace official clients.
3. **Local cache first** — The library appears instantly from SQLite; full scanning runs in the background.
4. **Optional integrations** — Each cloud platform fails silently if credentials, cache, or tools are missing.

## Quick code map

```
Program.cs → App.axaml.cs → MainWindowViewModel
                                    │
                    GameLibraryService (orchestrator)
                    ├── GameDatabase (SQLite)
                    ├── GameLib LauncherManager (installed)
                    ├── Scanners (Epic manifests, EA, Xbox…)
                    ├── ICloudLibraryProvider × 5 (cloud)
                    └── MetadataService (covers)
```

## Runtime data

| Path | What it stores |
|------|----------------|
| `%LocalAppData%\OpenGameHUB\settings.json` | User settings |
| `%LocalAppData%\OpenGameHUB\library.db` | Unified library |
| `%LocalAppData%\OpenGameHUB\covers\` | Downloaded covers |
| `%LocalAppData%\OpenGameHUB\tools\legendary.exe` | legendary CLI (auto-downloaded) |
| `%USERPROFILE%\.config\legendary\` | Epic credentials (legendary) |
