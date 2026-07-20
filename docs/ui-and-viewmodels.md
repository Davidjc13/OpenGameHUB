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
| `MainWindowViewModel.cs` | Library, search, sort, launch, covers, view mode |

### Key flows in `MainWindowViewModel`

| Command | Action |
|---------|--------|
| `RefreshLibraryCommand` | `GameLibraryService.RefreshLibraryAsync` + update UI |
| `LaunchSelectedGame` | Epic protocol case or `LaunchGame` |
| `ChangeCustomCoverAsync` / `ResetCustomCoverAsync` | User cover override (detail panel) |
| `SetGridView` / `SetListView` | Toggle `LibraryViewMode` (persisted) |
| `OpenSettingsAsync` | Modal `SettingsWindow`; handles dev relaunch / clear DB |
| `ToggleFavorite` | `GameLibraryService.ToggleFavorite` |

### Onboarding (first run)

After refreshing the library, `OfferOnboardingPromptsAsync` may show in sequence:

1. `SteamApiKeyPromptWindow` — Steam API benefits
2. `EaLibraryPromptWindow` — sync EA library
3. `LegendaryPromptWindow` — connect Epic

Each prompt has "Continue" and "Don't remind me again" → flags in `AppSettings`.

**Why modals after refresh:** the user already sees installed games; prompts explain how to add cloud library.

### Pagination

`PageSize = 24` — avoids creating thousands of `GameItemViewModel` with covers at once. See [metadata-and-covers.md](metadata-and-covers.md) for `ApplyVisibleCovers` and memory behavior.

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
- Display (`CoverQualityMode`, `UiFontScale`, library view mode)
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

Cancellation: `_refreshCts`, `_coverCts` in `MainWindowViewModel` to avoid overlapping refreshes.
