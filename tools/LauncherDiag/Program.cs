using GameLib;
using GameLib.Core;

var manager = new LauncherManager(new LauncherOptions
{
    QueryOnlineData = true,
    LoadLocalCatalogData = true,
    SearchExecutables = true
});

manager.Refresh();

foreach (var launcher in manager.GetLaunchers())
{
    Console.WriteLine($"[{launcher.Name}] installed={launcher.IsInstalled} games={launcher.Games.Count()} exe={launcher.Executable}");
    foreach (var game in launcher.Games.Take(5))
        Console.WriteLine($"  - {game.Name} | id={game.Id} | dir={game.InstallDir} | launch={game.LaunchString}");
}
