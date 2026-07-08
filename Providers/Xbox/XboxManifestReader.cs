using System.Xml.Linq;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Providers.Xbox;

internal static class XboxManifestReader
{
    private static readonly XNamespace UapNamespace =
        "http://schemas.microsoft.com/appx/manifest/uap/windows10";

    public static ManifestInfo Read(string manifestPath)
    {
        var document = XDocument.Load(manifestPath);
        var root = document.Root ?? throw new InvalidDataException("Missing manifest root.");
        var ns = root.Name.Namespace;
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

        var visualElements = application?.Element(UapNamespace + "VisualElements");
        var logoCandidates = new[]
        {
            visualElements?.Attribute("Square150x150Logo")?.Value,
            properties?.Element(ns + "Logo")?.Value,
            visualElements?.Attribute("Square44x44Logo")?.Value,
            visualElements?.Attribute("Logo")?.Value
        };

        var coverPath = ResolveFirstExistingImage(manifestDir, logoCandidates);

        return new ManifestInfo(
            displayName,
            categories,
            applicationId,
            executable,
            coverPath);
    }

    public static bool IsGameManifest(ManifestInfo metadata) =>
        metadata.Categories.Any(category =>
            category.Equals("games", StringComparison.OrdinalIgnoreCase) ||
            category.Equals("game", StringComparison.OrdinalIgnoreCase))
        || !string.IsNullOrWhiteSpace(metadata.Executable);

    public static void EnrichCatalogCoverUrls(IReadOnlyList<UnifiedGame> games)
    {
        foreach (var game in games)
        {
            if (game.Platform != Platform.GamePass || !string.IsNullOrWhiteSpace(game.CatalogCoverUrl))
                continue;

            var manifestPath = ResolveManifestPath(game.InstallPath);
            if (manifestPath is null)
                continue;

            try
            {
                var metadata = Read(manifestPath);
                if (!string.IsNullOrWhiteSpace(metadata.CoverPath))
                    game.CatalogCoverUrl = metadata.CoverPath;
            }
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(XboxManifestReader),
                    operation: "EnrichCatalogCoverUrls.ReadManifest",
                    exception: ex,
                    platform: Platform.GamePass,
                    details: manifestPath);
            }
        }
    }

    public static string? ResolveManifestPath(string? installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath))
            return null;

        var direct = Path.Combine(installPath, "AppxManifest.xml");
        if (File.Exists(direct))
            return direct;

        var content = Path.Combine(installPath, "Content", "AppxManifest.xml");
        return File.Exists(content) ? content : null;
    }

    private static string? ResolveFirstExistingImage(string manifestDir, IEnumerable<string?> relativePaths)
    {
        foreach (var relativePath in relativePaths)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                continue;

            var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var candidate = Path.Combine(manifestDir, normalized);
            if (File.Exists(candidate))
                return candidate;

            var scaleMatch = TryResolveScaledAsset(manifestDir, normalized);
            if (scaleMatch is not null)
                return scaleMatch;
        }

        return null;
    }

    private static string? TryResolveScaledAsset(string manifestDir, string relativePath)
    {
        var directory = Path.GetDirectoryName(relativePath);
        var fileName = Path.GetFileNameWithoutExtension(relativePath);
        var extension = Path.GetExtension(relativePath);
        if (string.IsNullOrEmpty(fileName))
            return null;

        var searchDir = string.IsNullOrEmpty(directory)
            ? manifestDir
            : Path.Combine(manifestDir, directory);

        if (!Directory.Exists(searchDir))
            return null;

        try
        {
            return Directory
                .EnumerateFiles(searchDir, $"{fileName}*{extension}", SearchOption.TopDirectoryOnly)
                .OrderByDescending(path => new FileInfo(path).Length)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(XboxManifestReader),
                operation: "TryResolveScaledAsset",
                exception: ex,
                platform: Platform.GamePass,
                details: searchDir);
            return null;
        }
    }

    internal sealed record ManifestInfo(
        string? DisplayName,
        IReadOnlyList<string> Categories,
        string? ApplicationId,
        string? Executable,
        string? CoverPath);
}
