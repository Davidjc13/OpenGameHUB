using GameHub.Data;
using GameHub.Services;
using System.Diagnostics;

var db = new GameDatabase();
var games = db.GetAllGames();
Console.WriteLine($"Games: {games.Count}");
foreach (var g in games)
{
    Console.WriteLine($"{g.Title} | kind={g.LaunchSpec.Kind} | value={g.LaunchSpec.Value}");
    var svc = new GameLibraryService();
    try {
        svc.LaunchGame(g);
        Console.WriteLine("  LAUNCH OK");
    } catch (Exception ex) {
        Console.WriteLine($"  LAUNCH FAIL: {ex.Message}");
    }
    break;
}
