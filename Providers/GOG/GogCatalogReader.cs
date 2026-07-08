using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Providers.Gog;

internal sealed record GogCatalogEntry(
    long GogId,
    string ReleaseKey,
    string Title,
    string? InstallPath = null);

internal static class GogCatalogReader
{
    private const int OriginalTitleGamePieceTypeId = 407;

    private static readonly string DefaultDatabasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GOG.com",
        "Galaxy",
        "storage",
        "galaxy-2.0.db");

    public static bool IsLauncherInstalled() => FindGalaxyClientExecutable() is not null;

    public static bool IsCloudLibraryAvailable() =>
        IsLauncherInstalled() && File.Exists(FindDatabasePath() ?? string.Empty);

    public static string? FindDatabasePath() =>
        File.Exists(DefaultDatabasePath) ? DefaultDatabasePath : null;

    public static string? FindGalaxyClientExecutable()
    {
        var candidates = new[]
        {
            @"C:\Program Files (x86)\GOG Galaxy\GalaxyClient.exe",
            @"C:\Program Files\GOG Galaxy\GalaxyClient.exe"
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\GalaxyClient");
            var path = key?.GetValue("clientExecutable") as string;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                return path;
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(GogCatalogReader),
                operation: "FindGalaxyClientExecutable.ReadRegistry",
                exception: ex,
                platform: Platform.Gog);
        }

        return null;
    }

    public static IReadOnlyList<GogCatalogEntry> ReadLibraryEntries()
    {
        var databasePath = FindDatabasePath()
            ?? throw new FileNotFoundException(Loc.T("GogDatabaseMissing"), "galaxy-2.0.db");

        const string sql = """
            SELECT
                ptr.gogId AS GogId,
                ptr.releaseKey AS ReleaseKey,
                gp.value AS TitleJson
            FROM LibraryReleases lr
            INNER JOIN ProductsToReleaseKeys ptr ON ptr.releaseKey = lr.releaseKey
            INNER JOIN ReleaseProperties rp ON rp.releaseKey = lr.releaseKey
            LEFT JOIN GamePieces gp ON gp.releaseKey = lr.releaseKey AND gp.gamePieceTypeId = @TitlePieceType
            WHERE rp.isDlc = 0 AND rp.isVisibleInLibrary = 1
            ORDER BY ptr.releaseKey
            """;

        return QueryEntries(databasePath, sql, new Dictionary<string, object>
        {
            ["TitlePieceType"] = OriginalTitleGamePieceTypeId
        });
    }

    public static IReadOnlyList<GogCatalogEntry> ReadInstalledEntries()
    {
        var databasePath = FindDatabasePath();
        if (databasePath is null)
            return [];

        const string sql = """
            SELECT
                ibp.productId AS GogId,
                ptr.releaseKey AS ReleaseKey,
                gp.value AS TitleJson,
                ibp.installationPath AS InstallPath
            FROM InstalledBaseProducts ibp
            LEFT JOIN ProductsToReleaseKeys ptr ON ptr.gogId = ibp.productId
            LEFT JOIN GamePieces gp ON gp.releaseKey = ptr.releaseKey AND gp.gamePieceTypeId = @TitlePieceType
            WHERE ibp.installationPath IS NOT NULL AND ibp.installationPath != ''
            ORDER BY ibp.installationPath
            """;

        return QueryEntries(databasePath, sql, new Dictionary<string, object>
        {
            ["TitlePieceType"] = OriginalTitleGamePieceTypeId
        }, requireInstallPath: true);
    }

    public static bool MatchesInstalledGame(UnifiedGame game, GogCatalogEntry entry)
    {
        if (game.Platform != Platform.Gog)
            return false;

        if (!string.IsNullOrWhiteSpace(game.PlatformGameId)
            && long.TryParse(game.PlatformGameId, out var gameId)
            && gameId == entry.GogId)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(game.InstallPath)
            && !string.IsNullOrWhiteSpace(entry.InstallPath)
            && string.Equals(
                NormalizePath(game.InstallPath),
                NormalizePath(entry.InstallPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var gameTitle = MetadataSearchHelper.NormalizeTitle(game.Title).ToLowerInvariant();
        var entryTitle = MetadataSearchHelper.NormalizeTitle(entry.Title).ToLowerInvariant();
        return gameTitle == entryTitle;
    }

    public static string BuildInstallProtocolUrl(string releaseKey) =>
        $"goggalaxy://openGameView/{releaseKey}";

    public static string BuildLaunchArguments(long gogId, string? installPath = null, bool install = false)
    {
        if (install)
            return $"/command=launch /gameId={gogId}";

        if (string.IsNullOrWhiteSpace(installPath))
            return $"/command=runGame /gameId={gogId}";

        return $"/command=runGame /gameId={gogId} /path=\"{installPath}\"";
    }

    private static List<GogCatalogEntry> QueryEntries(
        string databasePath,
        string sql,
        IReadOnlyDictionary<string, object> parameters,
        bool requireInstallPath = false)
    {
        var entries = new List<GogCatalogEntry>();
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var gogId = reader.GetInt64(0);
            var releaseKey = reader.IsDBNull(1) ? $"gog_{gogId}" : reader.GetString(1);
            var titleJson = reader.IsDBNull(2) ? null : reader.GetString(2);
            string? installPath = null;
            if (requireInstallPath)
            {
                if (reader.IsDBNull(3))
                    continue;

                installPath = reader.GetString(3);
                if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
                    continue;
            }

            var title = ParseTitle(titleJson) ?? $"GOG {gogId}";
            entries.Add(new GogCatalogEntry(gogId, releaseKey, title, installPath));
        }

        return entries;
    }

    private static string? ParseTitle(string? titleJson)
    {
        if (string.IsNullOrWhiteSpace(titleJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(titleJson);
            if (document.RootElement.TryGetProperty("title", out var titleElement)
                && titleElement.ValueKind == JsonValueKind.String)
            {
                return titleElement.GetString()?.Trim();
            }
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(GogCatalogReader),
                operation: "ParseTitle",
                exception: ex,
                platform: Platform.Gog,
                details: titleJson is null ? null : $"titleJsonLength={titleJson.Length}");
        }

        return null;
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
