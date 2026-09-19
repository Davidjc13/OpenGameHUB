# UI and ViewModels

## MVVM pattern

- **View** (`Views/*.axaml`) — layout, bindings, minimal code-behind logic
- **ViewModel** (`ViewModels/*`) — observable state, `RelayCommand`, text via `LocalizedStrings` / `Loc.T`
- **Services** — business logic; ViewModels do not touch SQLite or Process directly except in specific cases (settings/update)

`ViewModelBase` inherits from `ObservableObject` (CommunityToolkit).

## Main window

| File | Responsibility |
|------|----------------|
| `MainWindow.axaml` | Paginated grid or list, filters, status bar, cover actions |
| `MainWindowViewModel.cs` | Composition root: status bar, settings, wires child ViewModels |
| `ViewModels/MainWindow/MainWindowLibraryViewModel.cs` | Refresh, pagination, covers, launch, selection (`Library.*`) |
| `ViewModels/MainWindow/MainWindowSidebarViewModel.cs` | Search, filters, sort, collections (`Sidebar.*`) |
| `ViewModels/MainWindow/MainWindowOnboardingViewModel.cs` | First-run prompts (Steam API, EA, Legendary) |
| `MainWindowUpdatesViewModel.cs` | App update banner and install (`Updates.*`) |

`MainWindowViewModel` stays thin (~200 lines). Bindings use nested child ViewModels, e.g. `{Binding Library.Games}`, `{Binding Sidebar.SearchText}`.

### Key flows

| Command / area | Where | Action |
|----------------|-------|--------|
| `RefreshLibraryCommand` | Main | Delegates to `Library`, then onboarding prompts |
| `LaunchSelectedGame` | Library | `GameInstallOrchestrator` → `GameLibraryService.LaunchGame` |
| `ChangeCustomCoverAsync` / `ResetCustomCoverAsync` | Library | User cover override (detail panel) |
| `SetGridView` / `SetListView` | Library | Toggle `LibraryViewMode` (persisted) |
| `OpenSettingsAsync` | Main | Modal `SettingsWindow`; dev relaunch / clear DB |
| `ToggleFavorite` | Library | `GameLibraryService.ToggleFavorite` |
| Collection CRUD | Sidebar | `UserCollectionService` via sidebar commands |

### Onboarding (first run)

After refreshing the library, `MainWindowOnboardingViewModel.OfferPromptsIfNeededAsync` may show in sequence:

1. `SteamApiKeyPromptWindow` — Steam API benefits
2. `EaLibraryPromptWindow` — sync EA library
3. `LegendaryPromptWindow` — connect Epic

Each prompt has "Continue" and "Don't remind me again" → flags in `AppSettings`.

**Why modals after refresh:** the user already sees installed games; prompts explain how to add cloud library.

### Pagination

Page size is derived from viewport and cover quality profile in `MainWindowLibraryViewModel` — avoids creating thousands of `GameItemViewModel` with covers at once. See [metadata-and-covers.md](metadata-and-covers.md) for `ApplyVisibleCovers` and memory behavior.

### Library layout

Toolbar toggle switches between **grid** (card tiles) and **list** (horizontal rows with thumbnail). Stored in `AppSettings.LibraryViewMode`.

### Covers in the UI

- `ShowGridCovers` (Settings) — when off, only the selected game's cover may load
- Custom cover buttons in the detail panel when a game is selected
- `GameItemViewModel.ReleaseCover()` disposes bitmaps when rows leave the current page

## Settings

| File | Responsibility |
|------|----------------|
| `SettingsWindow.axaml` | Scrollable form |
| `SettingsViewModel.cs` | Steam setup, Epic connect/disconnect, metadata, updates, dev |

Sections:

- Language
- Steam Web API (opens `SteamSetupWindow`)
- Epic (connect/disconnect async)
- Display (`ThemeMode`, `CoverQualityMode`, `UiFontScale`, library view mode)
- Covers (IGDB, SteamGridDB)
- Updates (`AppUpdateService`)
- Developer (`DevModeService`, if enabled)

## Steam setup

`SteamSetupViewModel` — wizard to detect local account (`SteamLocalAccountReader`) and test API key + SteamID64 before saving.

## Localization

| Piece | Use |
|-------|-----|
| `Resources/Strings.resx` | English (default) |
| `Resources/Strings.es.resx` | Spanish |
| `LocalizationService` + `Loc.T(key, args)` | Code access |
| `LocalizedStrings` | Properties for AXAML bindings (`{Binding Strings.Settings}`) |

`Loc.Service.LanguageChanged` → `MainWindowViewModel` refreshes labels and rebuilds filters.

## Converters

- `PlatformBrushConverter` — accent color per `Platform`
- `SelectionBorderConverter` — selected card border

## `ViewLocator`

Resolves `FooViewModel` → `FooView` by convention (namespace `OpenGameHUB.Views`).

## Threads and UI

Long operations (refresh, covers, update check) use `async` + `Dispatcher.UIThread.InvokeAsync` where needed to touch `ObservableCollection`.

Cancellation: `_refreshCts`, `_coverCts` in `MainWindowLibraryViewModel` to avoid overlapping refreshes.
