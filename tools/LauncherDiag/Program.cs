using GameFinder.Common;
using GameFinder.StoreHandlers.Xbox;
using GameLib;
using GameLib.Core;
using NexusMods.Paths;
using System.Xml.Linq;

var manager = new LauncherManager(new LauncherOptions
{
    QueryOnlineData = true,
    LoadLocalCatalogData = true,
    SearchExecutables = true
});

manager.Refresh();

Console.WriteLine("=== GameLib launchers ===");
foreach (var launcher in manager.GetLaunchers())
{
    Console.WriteLine($"[{launcher.Name}] installed={launcher.IsInstalled} games={launcher.Games.Count()} exe={launcher.Executable}");
    foreach (var game in launcher.Games.Take(5))
        Console.WriteLine($"  - {game.Name} | id={game.Id} | dir={game.InstallDir} | launch={game.LaunchString}");
}

Console.WriteLine();
Console.WriteLine("=== Xbox / Game Pass (GameFinder) ===");
var xboxHandler = new XboxHandler(FileSystem.Shared);
var xboxCount = 0;
foreach (var result in xboxHandler.FindAllGames())
{
    if (!result.TryGetGame(out var xboxGame))
        continue;

    var installPath = xboxGame.Path.ToString();
    var manifestPath = ResolveManifestPath(installPath);
    if (manifestPath is null)
    {
        Console.WriteLine($"[skip] {xboxGame.DisplayName} | no manifest | path={installPath}");
        continue;
    }

    try
    {
        var info = ReadManifestSummary(manifestPath);
        if (!IsGameManifest(info))
        {
            Console.WriteLine($"[skip] {info.DisplayName ?? xboxGame.DisplayName} | not a game manifest");
            continue;
        }

        xboxCount++;
        Console.WriteLine(
            $"[{xboxCount}] {info.DisplayName ?? xboxGame.DisplayName} | id={xboxGame.Id.Value}");
        Console.WriteLine($"      path={installPath}");
        Console.WriteLine($"      manifest={manifestPath}");
        Console.WriteLine($"      exe={info.Executable ?? "(none)"} | appId={info.ApplicationId ?? "(none)"}");
        Console.WriteLine($"      cover={info.CoverPath ?? "(none)"}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[error] {xboxGame.DisplayName} | {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"Xbox game manifests: {xboxCount}");

static string? ResolveManifestPath(string installPath)
{
    var direct = Path.Combine(installPath, "AppxManifest.xml");
    if (File.Exists(direct))
        return direct;

    var content = Path.Combine(installPath, "Content", "AppxManifest.xml");
    return File.Exists(content) ? content : null;
}

static ManifestSummary ReadManifestSummary(string manifestPath)
{
    var document = XDocument.Load(manifestPath);
    var root = document.Root ?? throw new InvalidDataException("Missing manifest root.");
    var ns = root.Name.Namespace;
    var uap = XNamespace.Get("http://schemas.microsoft.com/appx/manifest/uap/windows10");
    var manifestDir = Path.GetDirectoryName(manifestPath)!;

    var properties = root.Element(ns + "Properties");
    var displayName = properties?.Element(ns + "DisplayName")?.Value;
    var categories = properties?
        .Elements(ns + "Category")
        .Select(element => element.Value.Trim())
        .Where(value => value.Length > 0)
        .ToList() ?? [];

    var application = root
        .Element(ns + "Applications")
        ?.Elements(ns + "Application")
        .FirstOrDefault(element => element.Attribute("Executable") is not null);

    var applicationId = application?.Attribute("Id")?.Value;
    var executable = application?.Attribute("Executable")?.Value;
    var visualElements = application?.Element(uap + "VisualElements");

    var logoCandidates = new[]
    {
        visualElements?.Attribute("Square150x150Logo")?.Value,
        properties?.Element(ns + "Logo")?.Value,
        visualElements?.Attribute("Square44x44Logo")?.Value
    };

    string? coverPath = null;
    foreach (var relative in logoCandidates)
    {
        if (string.IsNullOrWhiteSpace(relative))
            continue;

        var candidate = Path.Combine(manifestDir, relative.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(candidate))
        {
            coverPath = candidate;
            break;
        }
    }

    return new ManifestSummary(displayName, categories, applicationId, executable, coverPath);
}

static bool IsGameManifest(ManifestSummary info) =>
    info.Categories.Any(category =>
        category.Equals("games", StringComparison.OrdinalIgnoreCase) ||
        category.Equals("game", StringComparison.OrdinalIgnoreCase))
    || !string.IsNullOrWhiteSpace(info.Executable);

internal sealed record ManifestSummary(
    string? DisplayName,
    IReadOnlyList<string> Categories,
    string? ApplicationId,
    string? Executable,
    string? CoverPath);
