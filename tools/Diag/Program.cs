using OpenGameHUB.Services;

var service = new GameLibraryService();
var games = await service.RefreshLibraryAsync();
foreach (var g in games.OrderBy(x => x.Title))
    Console.WriteLine($"{g.Platform,-10} {g.Id,-40} {g.Title}");

Console.WriteLine($"Total: {games.Count}");
