using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace OpenGameHUB.Services.Gog;

internal sealed record GogCatalogEntry(string ReleaseKey, string ProductId, string Title, string? CoverUrl);

internal static class GogCatalogReader
{
    private const string DefaultDatabaseRelativePath = @"GOG.com\Galaxy\storage\galaxy-2.0.db";
    private const string WebcacheRelativePath = @"GOG.com\Galaxy\webcache";

    public static bool IsLauncherInstalled() =>
        !string.IsNullOrWhiteSpace(FindLauncherExecutable())
        || !string.IsNullOrWhiteSpace(FindLauncherInstallDir());

    public static string? FindDatabasePath()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var candidate = Path.Combine(programData, DefaultDatabaseRelativePath);
        return File.Exists(candidate) ? candidate : null;
    }

    public static bool IsCloudLibraryAvailable() =>
        IsLauncherInstalled() && FindDatabasePath() is not null;

    public static IReadOnlyList<GogCatalogEntry> ReadLibraryEntries()
    {
        var databasePath = FindDatabasePath()
            ?? throw new FileNotFoundException(Loc.T("GogCacheMissing"), "galaxy-2.0.db");

        using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
        connection.Open();

        var hiddenReleases = ReadHiddenReleases(connection);
        var dlcReleases = ReadDlcReleases(connection);
        var entries = new List<GogCatalogEntry>();
        var seenProductIds = new HashSet<string>(StringComparer.Ordinal);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT DISTINCT ppd.gameReleaseKey, titlePiece.value, imagesPiece.value
            FROM ProductPurchaseDates ppd
            INNER JOIN GamePieces titlePiece ON titlePiece.releaseKey = ppd.gameReleaseKey
            INNER JOIN GamePieceTypes titleType
                ON titleType.id = titlePiece.gamePieceTypeId
               AND titleType.type = 'title'
            LEFT JOIN GamePieces imagesPiece ON imagesPiece.releaseKey = ppd.gameReleaseKey
            LEFT JOIN GamePieceTypes imagesType
                ON imagesType.id = imagesPiece.gamePieceTypeId
               AND imagesType.type = 'originalImages'
            WHERE ppd.gameReleaseKey GLOB 'gog_*'
            ORDER BY titlePiece.value;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var releaseKey = reader.GetString(0);
            if (hiddenReleases.Contains(releaseKey) || dlcReleases.Contains(releaseKey))
                continue;

            var title = ParseTitle(reader.IsDBNull(1) ? null : reader.GetString(1));
            if (string.IsNullOrWhiteSpace(title))
                continue;

            var productId = ExtractProductId(releaseKey);
            if (productId is null || !seenProductIds.Add(productId))
                continue;

            var coverUrl = ParseCoverUrl(reader.IsDBNull(2) ? null : reader.GetString(2))
                ?? FindWebcacheCover(productId);

            entries.Add(new GogCatalogEntry(releaseKey, productId, title, coverUrl));
        }

        if (entries.Count == 0)
            throw new InvalidDataException(Loc.T("GogCacheMissing"));

        return entries;
    }

    public static void EnrichCatalogCoverUrls(IReadOnlyList<Models.UnifiedGame> games)
    {
        if (!IsCloudLibraryAvailable())
            return;

        IReadOnlyList<GogCatalogEntry> entries;
        try
        {
            entries = ReadLibraryEntries();
        }
        catch
        {
            return;
        }

        var byProductId = entries.ToDictionary(e => e.ProductId, StringComparer.Ordinal);
        foreach (var game in games)
        {
            if (game.Platform != Models.Platform.Gog || !string.IsNullOrWhiteSpace(game.CoverPath))
                continue;

            if (!byProductId.TryGetValue(game.PlatformGameId, out var entry))
                continue;

            if (!string.IsNullOrWhiteSpace(entry.CoverUrl))
            {
                game.CatalogCoverUrl ??= entry.CoverUrl;
                continue;
            }

            var webcacheCover = FindWebcacheCover(game.PlatformGameId);
            if (webcacheCover is not null)
                game.CatalogCoverUrl ??= webcacheCover;
        }
    }

    public static string? FindWebcacheCover(string productId)
    {
        if (string.IsNullOrWhiteSpace(productId))
            return null;

        var webcacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            WebcacheRelativePath);
        if (!Directory.Exists(webcacheRoot))
            return null;

        try
        {
            foreach (var overlayDir in Directory.EnumerateDirectories(webcacheRoot))
            {
                var gameDir = Path.Combine(overlayDir, "gog", productId);
                if (!Directory.Exists(gameDir))
                    continue;

                var cover = Directory
                    .EnumerateFiles(gameDir, "*vertical_cover*", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(IsUsableCoverFile);

                if (cover is not null)
                    return cover;

                cover = Directory
                    .EnumerateFiles(gameDir, "*square_icon*", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(IsUsableCoverFile);

                if (cover is not null)
                    return cover;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    public static bool MatchesInstalledGame(Models.UnifiedGame installed, GogCatalogEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(installed.PlatformGameId)
            && string.Equals(installed.PlatformGameId, entry.ProductId, StringComparison.Ordinal))
        {
            return true;
        }

        return MetadataSearchHelper.NormalizeTitle(installed.Title)
            .Equals(MetadataSearchHelper.NormalizeTitle(entry.Title), StringComparison.OrdinalIgnoreCase);
    }

    public static string? FindLauncherExecutable()
    {
        var installDir = FindLauncherInstallDir();
        if (string.IsNullOrWhiteSpace(installDir))
            return null;

        var clientPath = Path.Combine(installDir, "GalaxyClient.exe");
        return File.Exists(clientPath) ? clientPath : null;
    }

    public static bool IsGalaxyRunning() =>
        Process.GetProcessesByName("GOG Galaxy Notifications Renderer").Length > 0;

    internal static string? ExtractProductId(string releaseKey)
    {
        if (!releaseKey.StartsWith("gog_", StringComparison.OrdinalIgnoreCase))
            return null;

        var remainder = releaseKey[4..];
        var separator = remainder.IndexOf('_');
        var idPart = separator > 0 ? remainder[..separator] : remainder;
        return idPart.Length > 0 && idPart.All(char.IsDigit) ? idPart : null;
    }

    private static string? FindLauncherInstallDir()
    {
        foreach (var root in new[] { @"SOFTWARE\WOW6432Node\GOG.com\GalaxyClient\paths", @"SOFTWARE\GOG.com\GalaxyClient\paths" })
        {
            using var key = Registry.LocalMachine.OpenSubKey(root);
            var clientPath = key?.GetValue("client") as string;
            if (!string.IsNullOrWhiteSpace(clientPath) && Directory.Exists(clientPath))
                return clientPath;
        }

        return null;
    }

    private static HashSet<string> ReadHiddenReleases(SqliteConnection connection)
    {
        var hidden = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT releaseKey FROM UserReleaseProperties WHERE isHidden = 1;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                hidden.Add(reader.GetString(0));
        }
        catch
        {
            // Optional table/column; ignore if unavailable.
        }

        return hidden;
    }

    private static HashSet<string> ReadDlcReleases(SqliteConnection connection)
    {
        var dlcs = new HashSet<string>(StringComparer.Ordinal);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT gp.value
            FROM GamePieces gp
            INNER JOIN GamePieceTypes gpt ON gpt.id = gp.gamePieceTypeId AND gpt.type = 'dlcs';
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.IsDBNull(0))
                continue;

            AddDlcIds(dlcs, reader.GetString(0));
        }

        return dlcs;
    }

    private static void AddDlcIds(HashSet<string> dlcs, string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("dlcs", out var items) || items.ValueKind != JsonValueKind.Array)
                return;

            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        dlcs.Add(value);
                }
            }
        }
        catch
        {
            // Ignore malformed DLC metadata.
        }
    }

    private static string? ParseTitle(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("title", out var title))
                return title.GetString()?.Trim();
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? ParseCoverUrl(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            foreach (var propertyName in new[] { "verticalCover", "squareIcon", "background" })
            {
                if (!root.TryGetProperty(propertyName, out var value))
                    continue;

                var url = value.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(url))
                    return url;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static bool IsUsableCoverFile(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            return new FileInfo(path).Length >= 1_000;
        }
        catch
        {
            return false;
        }
    }
}
