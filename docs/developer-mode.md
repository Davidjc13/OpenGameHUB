# Developer mode

## When it is active

`DevModeService.IsEnabled`:

```csharp
#if DEBUG
    true;
#else
    Environment.GetEnvironmentVariable("OPENGAMEHUB_DEV") == "1";
#endif
```

In a published Release build, normal users **do not see** the Developer section in Settings.

Enable manually:

```powershell
$env:OPENGAMEHUB_DEV = "1"
dotnet run
```

## Component

File: `Services/DevModeService.cs`

## Actions in Settings

### Reset connections

```
DevModeService.ResetPlatformConnections()
  → Disconnect Epic (legendary, 8 s timeout)
  → EaCatalogReader.InvalidateCache()
  → LegendaryClient.InvalidateExecutableCache()

DevModeService.ResetConnectionSettings(settings)
  → Clears Steam API, Epic auth, Dismiss*Prompt flags
  → Keeps language, IGDB, SteamGridDB, ShowGridCovers

_onDevSessionReset → MainWindowViewModel.ResetDevSession
  → Allows replay of onboarding prompts when closing Settings
```

**Why:** test first-run flows without deleting the entire DB.

### Reset and relaunch

Same as reset connections, plus:

```
_onDevRelaunchRequested
  → MainWindow closes settings
  → GameLibraryService.Dispose()
  → ClearLocalLibraryCache()  // library.db + covers/
  → DevModeService.RelaunchApp()  // new process + Exit(0)
```

**Why relaunch:** SQLite and open connections; cleaner to restart the process than recreate `GameDatabase` hot on all paths.

### Clear local database

```
_onDevClearLocalDatabase
  → GameLibraryService.ResetLocalCache()
       Dispose DB → delete files → new GameDatabase + MetadataService
  → On closing Settings: RefreshLibraryCommand
```

**Why without relaunch:** test scan "from scratch" while keeping Epic/Steam connections and UI session.

**What it deletes:**

- `%LocalAppData%\OpenGameHUB\library.db`
- `%LocalAppData%\OpenGameHUB\covers\`

**What it does not delete:** `settings.json`, legendary, program installation.

## `ClearLocalLibraryCache`

Shared implementation with "Reset and relaunch":

```csharp
TryDeleteFile(library.db)
TryDeleteDirectory(covers)
```

Errors ignored — best-effort operation.

## `RelaunchApp`

- Detects if host is `dotnet.exe` → relaunches `OpenGameHUB.dll`
- Otherwise → relaunches `Environment.ProcessPath` with `UseShellExecute`

Useful when developing with `dotnet run`.

## Diagnostic tools

`tools/` folder — **not compiled** into the main exe:

| Tool | Project | Use |
|------|---------|-----|
| Diag | `tools/Diag/` | General diagnostics |
| LauncherDiag | `tools/LauncherDiag/` | Inspect GameLib launchers |
| GenerateIcon | `tools/GenerateIcon/` | Generate `app.ico` from PNG |

Run separately with `dotnet run --project tools/Diag/Diag.csproj` (example).

## Best practices

- Do not enable `OPENGAMEHUB_DEV` in end-user installers
- After "Clear local database", refresh library to repopulate from scan
- After "Reset connections", close Settings so onboarding prompts run
