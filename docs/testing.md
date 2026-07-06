# Testing

OpenGameHUB uses **xUnit** for unit tests. The test project is `tests/OpenGameHUB.Tests/` and is part of `OpenGameHUB.sln`.

## Run locally

From the repository root:

```powershell
dotnet test OpenGameHUB.sln -c Release
```

### Lint (format + analyzers)

```powershell
dotnet format OpenGameHUB.sln --verify-no-changes
dotnet build OpenGameHUB.sln -c Release
```

`Directory.Build.props` enables .NET analyzers and code style during build. `.editorconfig` defines formatting rules. To auto-fix style issues:

```powershell
dotnet format OpenGameHUB.sln
```

### Coverage (unit-test scope, ≥70%)

Coverage is measured on **logic covered by unit tests** — see `coverlet.runsettings` for the exact type list (Domain models, `Services/Games`, cover helpers, version comparer, selected provider parsers). UI, ViewModels, cloud orchestration, HTTP clients, and launcher I/O are out of scope.

```powershell
dotnet test OpenGameHUB.sln -c Release `
  --settings coverlet.runsettings `
  --collect:"XPlat Code Coverage" `
  --results-directory ./coverage/raw

dotnet tool install -g dotnet-reportgenerator-globaltool
$cobertura = Get-ChildItem ./coverage/raw,./tests/OpenGameHUB.Tests/TestResults -Recurse -Filter coverage.cobertura.xml |
  Sort-Object LastWriteTime -Descending | Select-Object -First 1
reportgenerator `
  -reports:$cobertura.FullName `
  -targetdir:./coverage/report `
  -reporttypes:TextSummary;HtmlInline_AzurePipelines
```

CI fails if line coverage on that scope drops below **70%** (`Threshold` in `coverlet.runsettings`).

Open `coverage/report/index.html` for the HTML report.

Or only the test project:

```powershell
dotnet test tests\OpenGameHUB.Tests\OpenGameHUB.Tests.csproj
```

Run before pushing — CI executes the same suite on every push and pull request.

## What is covered today

| Area | Tests |
|------|-------|
| `ReleaseVersionComparer` / `AppUpdateService.IsNewer` | Alpha/beta/stable ordering, `0.0.10` vs `0.0.9-1` |
| `GameLibraryMerger` / `GameSearchHelper` / `GameEntryFilter` | Merge, search, junk filtering |
| `MetadataSearchHelper` / `CoverPathHelper` / `SafeImageValidator` | Covers and image validation |
| `EaLogCatalogReader` / `SteamLocalLibraryReader` | Log and VDF parsers |
| `EpicKeyImageHelper` / `XboxCatalogReader` / `RockstarCoverUrls` | Provider helpers |
| `RiotCatalogReader` / `RiotLauncherClient` | Install args and product id resolution |
| `AppSettings` / `LocalizationService` | Settings and i18n helpers |

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
4. Run `dotnet test --settings coverlet.runsettings` before opening a PR.
5. When adding a type to the coverage gate, list it in `coverlet.runsettings` `Include` and add tests first.

Integration tests against real launchers belong in `tools/Diag` or `tools/LauncherDiag` — not in the unit test project.

## CI

`.github/workflows/build-installer.yml` job **CI** (single runner, steps in order):

1. **Restore**
2. **Lint** — `dotnet format --verify-no-changes`
3. **Build** — analyzers run via `EnforceCodeStyleInBuild`
4. **Test with coverage** — coverlet + ReportGenerator summary
5. On **pull requests**, post/update a sticky comment with `Summary.txt`
6. Upload `coverage-report` artifact (HTML + Cobertura)

**Publish** (`needs: ci`, tags only) — installer + GitHub Release.
