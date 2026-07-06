using System.Diagnostics;

var sw = Stopwatch.StartNew();
var service = new GameLibraryService();
var progress = new Progress<string>(msg => Console.WriteLine($"[{sw.Elapsed.TotalSeconds:F1}s] {msg}"));
var games = await service.RefreshLibraryAsync(progress);
Console.WriteLine($"Total: {games.Count} in {sw.Elapsed.TotalSeconds:F1}s");
