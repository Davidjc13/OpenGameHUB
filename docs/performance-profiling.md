# Performance and resource profiling

OpenGameHUB does not ship a full CPU profiler. RAM and CPU are controlled mainly around **cover bitmaps** and **library scanning** (see [metadata-and-covers.md](metadata-and-covers.md)). In **developer mode**, the main window shows a live metrics overlay (working set, managed heap, loaded cover count).

## Developer overlay

Active when:

- Debug builds (`DevModeService.IsEnabled`), or
- Release with `OPENGAMEHUB_DEV=1`

```powershell
$env:OPENGAMEHUB_DEV = "1"
dotnet run --project OpenGameHUB.csproj
```

The overlay updates every second and reads `AppResourceMonitor` plus library state from `MainWindowLibraryViewModel`.

## External tools (.NET)

### Live counters

With the app running, note the process id (`Get-Process OpenGameHUB`):

```powershell
dotnet-counters monitor --process-id <PID> --counters System.Runtime[working-set,gc-heap-size,gen-0-gc-count,threadpool-thread-count,cpu-usage]
```

Repeat while:

1. Cold start with SQLite cache
2. Full library refresh (F5)
3. Paging quickly through the grid
4. Changing **Cover quality** (Settings): Low → High → None
5. Console mode (gamepad navigation)

### CPU sampling

```powershell
dotnet-trace collect --process-id <PID> --profile cpu-sampling --duration 00:00:30
```

Inspect stacks for `CoverImageLoader`, `Bitmap.DecodeToWidth`, `ScanAllGames`, and cloud provider I/O.

### Visual Studio

Use **Diagnostic Tools** while debugging: memory snapshots before/after refresh or cover quality changes; CPU while scrolling or paging.

## Headless scan baseline (`tools/Diag`)

Console refresh timing and process memory (no UI):

```powershell
dotnet run --project tools/Diag/Diag.csproj
```

Example output shape (values depend on your library and launchers):

```
Total: <N> in <seconds>s
Working set: <MB> -> <MB>
Managed heap: <MB> -> <MB>
```

### Baseline workflow

Run `tools/Diag` on your Windows PC (requires GameLib launcher plugins and an existing `%LocalAppData%\OpenGameHUB\library.db` for meaningful timings). Record the printed **Working set** and **Managed heap** lines plus total seconds. Compare **before/after a code change** on the same machine rather than against fixed numbers.

The dev overlay and `dotnet-counters` complement Diag for UI-heavy scenarios (paging, cover quality) that Diag does not exercise.

Example `tools/Diag` run (800 games, full launcher sync, one dev machine — not a fixed benchmark):

```
Total: 800 in 20.2s
Working set: 24.3 MB -> 305.4 MB
Managed heap: 0.8 MB -> 59.2 MB
```

## Logs

`AppDiagnostics` writes errors to `%LocalAppData%\OpenGameHUB\logs\app.log`. Logs do not include RAM counters but help explain background CPU/network during cover enrichment.

## Related code

| File | Role |
|------|------|
| `Infrastructure/AppResourceSnapshot.cs` | Process + GC snapshot |
| `ViewModels/MainWindow/MainWindowDevMetricsViewModel.cs` | Dev overlay text |
| `Services/Covers/CoverQualitySettings.cs` | RAM/CPU trade-offs per quality mode |
| `ViewModels/MainWindow/MainWindowLibraryViewModel.cs` | Pagination, `ApplyVisibleCovers` |
| `Services/Covers/CoverImageLoader.cs` | Decode throttle (2 concurrent) |
