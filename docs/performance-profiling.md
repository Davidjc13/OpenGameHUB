# Performance and resource profiling

OpenGameHUB does not ship a full CPU profiler. RAM and CPU are controlled mainly around **cover bitmaps**, **native/GPU buffers**, and **library scanning** (see [metadata-and-covers.md](metadata-and-covers.md)). In **developer mode**, the main window shows a live metrics overlay (working set, private memory, managed heap, loaded cover count).

The managed heap is typically tens of megabytes. Working set and private bytes are often much higher: Skia/GPU, the CLR, and optional WebView2 child processes — not the `GameItemViewModel` list.

**Judge success by private bytes / working set in a normal library session**, not by managed heap alone.

## Target: under 200 MB (private bytes, idle)

Use the dev overlay **Private memory** after the library is idle (no F5 in progress, no WebView2 login). Compare like with like on the same PC.

| Configuration | Typical private bytes (order of magnitude) | Notes |
|---------------|--------------------------------------------|-------|
| `dotnet run` Debug, GPU, auto refresh on startup | 220–280 MB | Dev overlay + Avalonia dev tools |
| Release, GPU, cache loaded, **Refresh library on startup** off | 170–220 MB | Settings → uncheck refresh on startup; F5 when you need sync |
| Release + `OPENGAMEHUB_RENDER_SOFTWARE=1` | Often lower GPU heap | More CPU when scrolling |
| Release + `OPENGAMEHUB_LOW_MEMORY=1` (or software render) | Uses `RedirectionSurface` only | Skips WinUI/DirectComposition buffers |
| `tools/Diag` Release after full scan (no UI) | Working set ~280–310 MB on a large library | Scan peak, not idle UI |

Example Release Diag (800 games, one dev machine): working set 25 → 290 MB during scan; managed heap ~63 MB at end.

Recommended stack to try for &lt;200 MB:

```powershell
$env:OPENGAMEHUB_RENDER_SOFTWARE = "1"
$env:OPENGAMEHUB_LOW_MEMORY = "1"
dotnet run -c Release --project OpenGameHUB.csproj
```

Then disable **Refresh library on startup** in Settings, restart, wait idle, read overlay.

## Developer overlay

Active when:

- Debug builds (`DevModeService.IsEnabled`), or
- Release with `OPENGAMEHUB_DEV=1`

```powershell
$env:OPENGAMEHUB_DEV = "1"
dotnet run --project OpenGameHUB.csproj
```

The overlay updates every second and reads `AppResourceMonitor` plus library state from `MainWindowLibraryViewModel`. Compare **Private memory** vs **Managed heap** on the overlay.

## Measure with VMMap (Sysinternals)

Use [VMMap](https://learn.microsoft.com/en-us/sysinternals/downloads/vmmap) on `OpenGameHUB.exe` after the library is idle. Record:

| Region | What it usually means |
|--------|------------------------|
| Managed Heap | CLR objects and bitmaps that are still rooted |
| Image | Loaded DLLs / ReadyToRun images |
| Mapped File | Memory-mapped files |
| Heap (private) | Native heaps (Skia, GPU driver, Win32) |
| Stack | Thread stacks (high thread count adds up) |

### Scenarios (same machine, same library)

1. **Normal library** — grid or list, no console mode, no login dialog. This is the baseline for native-memory work.
2. **Embedded login** — Settings → connect Steam/Epic/Xbox (WebView2 open). Note **child** `msedgewebview2.exe` processes in Task Manager → Details. Task Manager often rolls those into the parent app.
3. **Console mode** — hero backdrop visible (low-res scaled cover, no live blur). Compare private bytes with scenario 1.
4. Overlay: **Private memory** vs **Managed heap** (and Gen0 collections) in each scenario.

Repeat after code changes. Do not treat a single Diag run as a contract.

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

## Software vs GPU rendering (A/B)

Default on Windows is ANGLE (`Win32RenderingMode.AngleEgl`) with software fallback. GPU drivers can add private memory; software rendering can lower that and raise CPU during scroll.

```powershell
$env:OPENGAMEHUB_RENDER_SOFTWARE = "1"
dotnet run --project OpenGameHUB.csproj
```

Configured in `Program.cs` via `Win32PlatformOptions.RenderingMode`. Compare VMMap / overlay private bytes for scenario 1 with and without the variable.

`OPENGAMEHUB_LOW_MEMORY=1` (or software rendering) also sets `CompositionMode` to `RedirectionSurface` only, avoiding WinUI/DirectComposition extra buffers.

## Runtime and publish flags

[`runtimeconfig.template.json`](../runtimeconfig.template.json) (copied into the output `.runtimeconfig.json`):

| Property | Intent | Risk |
|----------|--------|------|
| `System.GC.Concurrent` = false | No background GC thread / extra reservation | Slightly longer GC pauses (usually fine for a launcher UI) |
| `System.GC.ConserveMemory` = 8 | Compact more aggressively | Compare idle working set; watch GC pause feel |
| `System.Globalization.UseNls` = true | Windows NLS instead of ICU | Check playtime dates (`ToString("d")`) in en-US and es-ES |

Release publish also sets `PublishReadyToRun` (less JIT / code memory at startup, larger on disk). **Do not enable `PublishTrimmed`** without a smoke pass per provider (Dapper, protobuf-net, and YamlDotNet use reflection).

After changing these, re-run VMMap scenario 1.

## WebView2 child processes

Embedded login uses WebView2 (`msedgewebview2.exe`) **outside** the OpenGameHUB process. Task Manager may group them under the app.

Checklist:

1. Note `msedgewebview2.exe` count **before** opening Settings login.
2. Open embedded sign-in, complete or cancel, close the dialog.
3. Confirm the extra WebView2 processes exit (Task Manager → Details). A leftover process is a leak in `WebView2Host.Dispose` / auth dialog lifetime, not library covers.
4. If you measured ~250 MB **with the login browser open**, a large share is not OpenGameHUB’s managed heap.

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

The dev overlay, VMMap, and `dotnet-counters` complement Diag for UI-heavy scenarios (paging, cover quality, GPU) that Diag does not exercise.

Example `tools/Diag` run (800 games, full launcher sync, one dev machine — not a fixed benchmark):

```
Total: 800 in 20.2s
Working set: 24.3 MB -> 305.4 MB
Managed heap: 0.8 MB -> 59.2 MB
```

## Logs

`AppDiagnostics` writes errors to `%LocalAppData%\OpenGameHUB\logs\app.log`. Logs do not include RAM counters but help explain background CPU/network during cover enrichment.

## Post-sync memory release

After a full library refresh (F5), the app tries to return to a **cold-start-without-sync** footprint:

1. **When sync finishes** — UI reloads from SQLite (`LoadCachedGames` path, new view models); Steam/Xbox scan caches are cleared.
2. **When background cover enrichment finishes** (or immediately if cover quality is None) — reload from SQLite again, drop Epic in-memory index, `PRAGMA shrink_memory`, full GC compact, and **working-set trim** (default).

Opt out:

```powershell
$env:OPENGAMEHUB_NO_POST_SYNC_IDLE = "1"   # skip GC + trim entirely
$env:OPENGAMEHUB_NO_POST_SYNC_TRIM = "1"   # GC/compact only, no SetProcessWorkingSetSize
```

Trim can make the next scroll slightly slower while pages fault back in. Private bytes in the overlay may still exceed Task Manager — that is normal.

## Related code

| File | Role |
|------|------|
| `Infrastructure/AppResourceSnapshot.cs` | Process + GC snapshot |
| `ViewModels/MainWindow/MainWindowDevMetricsViewModel.cs` | Dev overlay text |
| `Services/Covers/CoverQualitySettings.cs` | RAM/CPU trade-offs per quality mode |
| `ViewModels/MainWindow/MainWindowLibraryViewModel.cs` | Pagination, `ApplyVisibleCovers` |
| `Services/Covers/CoverImageLoader.cs` | Decode throttle (2 concurrent), 512px fallback cap |
| `Infrastructure/Input/XInputGamepadPoller.cs` | Idle 400 ms poll; 33 ms in console or with a pad |
| `Program.cs` | `OPENGAMEHUB_RENDER_SOFTWARE`, `OPENGAMEHUB_LOW_MEMORY`, Win32 rendering/composition |
| `runtimeconfig.template.json` | GC / NLS experiment flags |
| Settings → **Refresh library on startup** | Skip full scan when local cache exists (F5 to sync) |
| `GameLibraryService.ReleaseTransientSyncMemory` | Post-sync cloud catalog / Epic index cleanup |
| `OPENGAMEHUB_NO_POST_SYNC_IDLE` / `NO_POST_SYNC_TRIM` | Disable post-sync idle baseline |
