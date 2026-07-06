# Testing

OpenGameHUB uses **xUnit** for unit tests. The test project is `tests/OpenGameHUB.Tests/` and is part of `OpenGameHUB.sln`.

## Run locally

From the repository root:

```powershell
dotnet test OpenGameHUB.sln -c Release
```

Or only the test project:

```powershell
dotnet test tests\OpenGameHUB.Tests\OpenGameHUB.Tests.csproj
```

Run before pushing — CI executes the same suite on every push and pull request.

## What is covered today

| Area | Tests |
|------|-------|
| `ReleaseVersionComparer` / `AppUpdateService.IsNewer` | Alpha/beta/stable ordering, `0.0.10` vs `0.0.9-1` |
| `GameLibraryMerger` | Deduplication, platform priority, failed cloud sync preservation |
| `GameEntryFilter` | Steam redistributables, Riot junk metadata, install paths |
| `MetadataSearchHelper` | Title normalization for search/covers |
| `LaunchSpec` | Pipe encoding for launcher-args / executable |
| `RiotCatalogReader` / `RiotLauncherClient` | Install args and product id resolution |

These are **fast, deterministic** tests — no launchers, network, or UI.

## Accessing internal types

Most test targets (`GameLibraryMerger`, `GameEntryFilter`, `ReleaseVersionComparer`, etc.) are `internal`. The main project grants access with:

```xml
<InternalsVisibleTo Include="OpenGameHUB.Tests" />
```

in `OpenGameHUB.csproj`.

## Adding tests

1. Add a new `*Tests.cs` file under `tests/OpenGameHUB.Tests/`.
2. Prefer testing **pure functions** (merge logic, parsers, comparers) over full `GameLibraryService` integration.
3. Use `TestGames.Create()` helper for `UnifiedGame` fixtures.
4. Run `dotnet test` before opening a PR.

Integration tests against real launchers belong in `tools/Diag` or `tools/LauncherDiag` — not in the unit test project.

## CI

`.github/workflows/build-installer.yml` runs three jobs in sequence:

1. **Build** — `dotnet restore` + `dotnet build -c Release`
2. **Test** (`needs: build`) — restore, build, then `dotnet test -c Release --no-build`
3. **Publish** (`needs: test`, tags only) — installer + GitHub Release

Publish does not re-run tests; it only runs when the Test job has already passed.
