using System.Diagnostics;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Services.Games;

var before = AppResourceMonitor.Capture();
var sw = Stopwatch.StartNew();
var service = new GameLibraryService();
var progress = new Progress<string>(msg => Console.WriteLine($"[{sw.Elapsed.TotalSeconds:F1}s] {msg}"));
var games = await service.RefreshLibraryAsync(progress);
var after = AppResourceMonitor.Capture();
Console.WriteLine($"Total: {games.Count} in {sw.Elapsed.TotalSeconds:F1}s");
Console.WriteLine(
    $"Working set: {AppResourceFormatting.FormatMegabytes(before.WorkingSetBytes)} MB -> {AppResourceFormatting.FormatMegabytes(after.WorkingSetBytes)} MB");
Console.WriteLine(
    $"Managed heap: {AppResourceFormatting.FormatMegabytes(before.ManagedHeapBytes)} MB -> {AppResourceFormatting.FormatMegabytes(after.ManagedHeapBytes)} MB");
