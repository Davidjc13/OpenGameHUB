using Dapper;
using Microsoft.Data.Sqlite;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Infrastructure.Database;

public sealed class GameDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public GameDatabase()
        : this(null)
    {
    }

    internal GameDatabase(string? databasePath)
    {
        var dbPath = databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenGameHUB",
            "library.db");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        _connection.Execute("PRAGMA foreign_keys = ON;");
        Initialize();
        Migrate();
    }

    private void Migrate()
    {
        var columns = _connection.Query<TableColumnInfo>("PRAGMA table_info(games)");
        if (columns.All(column => !string.Equals(column.name, "custom_cover", StringComparison.OrdinalIgnoreCase)))
        {
            _connection.Execute(
                "ALTER TABLE games ADD COLUMN custom_cover INTEGER NOT NULL DEFAULT 0");
        }

        _connection.Execute("""
            CREATE TABLE IF NOT EXISTS collections (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                sort_order INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS collection_games (
                collection_id TEXT NOT NULL REFERENCES collections(id) ON DELETE CASCADE,
                game_id TEXT NOT NULL,
                added_at TEXT NOT NULL,
                PRIMARY KEY (collection_id, game_id)
            );

            CREATE INDEX IF NOT EXISTS idx_collection_games_game
                ON collection_games(game_id);
            """);
    }

    private void Initialize()
    {
        _connection.Execute("""
            CREATE TABLE IF NOT EXISTS games (
                id TEXT PRIMARY KEY,
                platform INTEGER NOT NULL,
                platform_game_id TEXT NOT NULL,
                title TEXT NOT NULL,
                is_installed INTEGER NOT NULL,
                install_path TEXT,
                cover_path TEXT,
                playtime_minutes INTEGER NOT NULL DEFAULT 0,
                last_played TEXT,
                is_favorite INTEGER NOT NULL DEFAULT 0,
                launch_kind TEXT NOT NULL,
                launch_value TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """);
    }

    public IReadOnlyList<UnifiedGame> GetAllGames()
    {
        const string sql = """
            SELECT
                id AS Id,
                platform AS Platform,
                platform_game_id AS PlatformGameId,
                title AS Title,
                is_installed AS IsInstalled,
                install_path AS InstallPath,
                cover_path AS CoverPath,
                custom_cover AS HasCustomCover,
                playtime_minutes AS PlaytimeMinutes,
                last_played AS LastPlayed,
                is_favorite AS IsFavorite,
                launch_kind AS LaunchKind,
                launch_value AS LaunchValue
            FROM games
            ORDER BY title COLLATE NOCASE
            """;

        var rows = _connection.Query<GameRow>(sql);
        return rows.Select(r => r.ToUnifiedGame()).ToList();
    }

    public void UpsertGames(IEnumerable<UnifiedGame> games)
    {
        const string sql = """
            INSERT INTO games (
                id, platform, platform_game_id, title, is_installed, install_path,
                cover_path, custom_cover, playtime_minutes, last_played, is_favorite, launch_kind,
                launch_value, updated_at
            ) VALUES (
                @Id, @Platform, @PlatformGameId, @Title, @IsInstalled, @InstallPath,
                @CoverPath, @HasCustomCover, @PlaytimeMinutes, @LastPlayed, @IsFavorite, @LaunchKind,
                @LaunchValue, @UpdatedAt
            )
            ON CONFLICT(id) DO UPDATE SET
                platform = excluded.platform,
                platform_game_id = excluded.platform_game_id,
                title = excluded.title,
                is_installed = excluded.is_installed,
                install_path = excluded.install_path,
                cover_path = COALESCE(NULLIF(excluded.cover_path, ''), games.cover_path),
                custom_cover = excluded.custom_cover,
                playtime_minutes = excluded.playtime_minutes,
                last_played = excluded.last_played,
                launch_kind = excluded.launch_kind,
                launch_value = excluded.launch_value,
                updated_at = excluded.updated_at;
            """;

        var now = DateTime.UtcNow.ToString("O");
        foreach (var game in games)
        {
            _connection.Execute(sql, new
            {
                game.Id,
                Platform = (int)game.Platform,
                game.PlatformGameId,
                game.Title,
                IsInstalled = game.IsInstalled ? 1 : 0,
                game.InstallPath,
                game.CoverPath,
                HasCustomCover = game.HasCustomCover ? 1 : 0,
                game.PlaytimeMinutes,
                LastPlayed = game.LastPlayed?.ToString("O"),
                IsFavorite = game.IsFavorite ? 1 : 0,
                LaunchKind = game.LaunchSpec.Kind,
                LaunchValue = game.LaunchSpec.Value,
                UpdatedAt = now
            });
        }
    }

    public void SyncScannedGames(IEnumerable<UnifiedGame> scannedGames)
    {
        var list = scannedGames.ToList();
        if (list.Count == 0)
            return;

        var favorites = _connection.Query<(string Id, int IsFavorite)>(
                "SELECT id, is_favorite FROM games WHERE is_favorite = 1")
            .ToDictionary(x => x.Id, x => x.IsFavorite == 1);

        var favoriteKeys = _connection.Query<(int Platform, string PlatformGameId, string? InstallPath)>(
                "SELECT platform, platform_game_id, install_path FROM games WHERE is_favorite = 1")
            .Select(x => FavoriteKey(x.Platform, x.PlatformGameId, x.InstallPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var favoriteTitles = _connection.Query<string>(
                "SELECT title FROM games WHERE is_favorite = 1")
            .Select(title => MetadataSearchHelper.NormalizeTitle(title).ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        var existingCovers = _connection.Query<(string Id, string? CoverPath)>(
                "SELECT id, cover_path FROM games")
            .ToDictionary(x => x.Id, x => x.CoverPath, StringComparer.Ordinal);

        var existingCustomCovers = _connection.Query<(string Id, int HasCustomCover)>(
                "SELECT id, custom_cover FROM games WHERE custom_cover = 1")
            .ToDictionary(x => x.Id, x => x.HasCustomCover == 1, StringComparer.Ordinal);

        var existingLastPlayed = _connection.Query<(string Id, string LastPlayed)>(
                "SELECT id, last_played FROM games WHERE last_played IS NOT NULL AND last_played != ''")
            .ToDictionary(x => x.Id, x => DateTime.Parse(x.LastPlayed), StringComparer.Ordinal);

        foreach (var game in list)
        {
            game.IsFavorite = favorites.TryGetValue(game.Id, out var fav) && fav
                || favoriteKeys.Contains(FavoriteKey((int)game.Platform, game.PlatformGameId, game.InstallPath))
                || favoriteTitles.Contains(MetadataSearchHelper.NormalizeTitle(game.Title).ToLowerInvariant());

            if (existingCustomCovers.TryGetValue(game.Id, out var hasCustomCover))
                game.HasCustomCover = hasCustomCover;

            if (string.IsNullOrWhiteSpace(game.CoverPath)
                && existingCovers.TryGetValue(game.Id, out var coverPath)
                && !string.IsNullOrWhiteSpace(coverPath))
            {
                game.CoverPath = coverPath;
            }

            var cachedCover = CoverPathHelper.ResolveExistingPath(game);
            if (cachedCover is not null)
                game.CoverPath = cachedCover;

            if (existingLastPlayed.TryGetValue(game.Id, out var lastPlayed))
                game.LastPlayed = lastPlayed;
        }

        UpsertGames(list);

        var scannedIds = list.Select(g => g.Id).ToArray();
        _connection.Execute("DELETE FROM games WHERE id NOT IN @Ids", new { Ids = scannedIds });
        PurgeOrphanCollectionGames();
    }

    public IReadOnlyList<UserCollection> GetUserCollections()
    {
        const string sql = """
            SELECT id AS Id, name AS Name, sort_order AS SortOrder
            FROM collections
            ORDER BY sort_order, name COLLATE NOCASE
            """;

        return _connection.Query<CollectionRow>(sql)
            .Select(row => row.ToUserCollection())
            .ToList();
    }

    public UserCollection CreateCollection(string name)
    {
        var now = DateTime.UtcNow.ToString("O");
        var maxOrder = _connection.ExecuteScalar<int?>("SELECT MAX(sort_order) FROM collections") ?? -1;
        var collection = new UserCollection(Guid.NewGuid().ToString("N"), name.Trim(), maxOrder + 1);

        _connection.Execute(
            """
            INSERT INTO collections (id, name, sort_order, created_at, updated_at)
            VALUES (@Id, @Name, @SortOrder, @CreatedAt, @UpdatedAt)
            """,
            new
            {
                collection.Id,
                collection.Name,
                collection.SortOrder,
                CreatedAt = now,
                UpdatedAt = now
            });

        return collection;
    }

    public void RenameCollection(string id, string name)
    {
        _connection.Execute(
            """
            UPDATE collections
            SET name = @Name, updated_at = @UpdatedAt
            WHERE id = @Id
            """,
            new { Id = id, Name = name.Trim(), UpdatedAt = DateTime.UtcNow.ToString("O") });
    }

    public void DeleteCollection(string id) =>
        _connection.Execute("DELETE FROM collections WHERE id = @Id", new { Id = id });

    public void AddGameToCollection(string collectionId, string gameId)
    {
        _connection.Execute(
            """
            INSERT OR IGNORE INTO collection_games (collection_id, game_id, added_at)
            VALUES (@CollectionId, @GameId, @AddedAt)
            """,
            new
            {
                CollectionId = collectionId,
                GameId = gameId,
                AddedAt = DateTime.UtcNow.ToString("O")
            });
    }

    public void RemoveGameFromCollection(string collectionId, string gameId) =>
        _connection.Execute(
            """
            DELETE FROM collection_games
            WHERE collection_id = @CollectionId AND game_id = @GameId
            """,
            new { CollectionId = collectionId, GameId = gameId });

    public Dictionary<string, HashSet<string>> GetMembershipIndex()
    {
        var rows = _connection.Query<(string CollectionId, string GameId)>(
            "SELECT collection_id, game_id FROM collection_games");

        var index = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (collectionId, gameId) in rows)
        {
            if (!index.TryGetValue(gameId, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                index[gameId] = set;
            }

            set.Add(collectionId);
        }

        return index;
    }

    public HashSet<string> GetCollectionGameIds(string collectionId)
    {
        var ids = _connection.Query<string>(
            "SELECT game_id FROM collection_games WHERE collection_id = @CollectionId",
            new { CollectionId = collectionId });

        return ids.ToHashSet(StringComparer.Ordinal);
    }

    public int GetCollectionGameCount(string collectionId) =>
        _connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM collection_games WHERE collection_id = @CollectionId",
            new { CollectionId = collectionId });

    internal void PurgeOrphanCollectionGames() =>
        _connection.Execute(
            "DELETE FROM collection_games WHERE game_id NOT IN (SELECT id FROM games)");

    private static string FavoriteKey(int platform, string platformGameId, string? installPath) =>
        $"{platform}|{NormalizePath(installPath) ?? platformGameId}";

    public void UpdateCoverPath(string id, string coverPath)
    {
        _connection.Execute(
            "UPDATE games SET cover_path = @CoverPath WHERE id = @Id",
            new { Id = id, CoverPath = coverPath });
    }

    public void SetCustomCover(string id, bool hasCustomCover)
    {
        _connection.Execute(
            "UPDATE games SET custom_cover = @HasCustomCover WHERE id = @Id",
            new { Id = id, HasCustomCover = hasCustomCover ? 1 : 0 });
    }

    private sealed class TableColumnInfo
    {
        public string name { get; init; } = string.Empty;
    }

    public void RecordLauncherLaunch(string id, DateTime launchedAt)
    {
        _connection.Execute(
            """
            UPDATE games
            SET last_played = @LastPlayed
            WHERE id = @Id
            """,
            new
            {
                Id = id,
                LastPlayed = launchedAt.ToString("O")
            });
    }

    public void UpdatePlaytime(string id, int playtimeMinutes, DateTime? lastPlayed)
    {
        _connection.Execute(
            """
            UPDATE games
            SET playtime_minutes = @PlaytimeMinutes,
                last_played = @LastPlayed
            WHERE id = @Id
            """,
            new
            {
                Id = id,
                PlaytimeMinutes = playtimeMinutes,
                LastPlayed = lastPlayed?.ToString("O")
            });
    }

    public void PersistPlaytimes(IEnumerable<UnifiedGame> games)
    {
        foreach (var game in games)
        {
            _connection.Execute(
                """
                UPDATE games
                SET playtime_minutes = @PlaytimeMinutes
                WHERE id = @Id
                """,
                new { Id = game.Id, PlaytimeMinutes = game.PlaytimeMinutes });
        }
    }

    public void SetFavorite(string id, bool isFavorite)
    {
        _connection.Execute(
            "UPDATE games SET is_favorite = @IsFavorite WHERE id = @Id",
            new { Id = id, IsFavorite = isFavorite ? 1 : 0 });
    }

    private static string NormalizePath(string? path) =>
        string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private sealed class CollectionRow
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public int SortOrder { get; init; }

        public UserCollection ToUserCollection() => new(Id, Name, SortOrder);
    }

    private sealed class GameRow
    {
        public string Id { get; init; } = "";
        public int Platform { get; init; }
        public string PlatformGameId { get; init; } = "";
        public string Title { get; init; } = "";
        public int IsInstalled { get; init; }
        public string? InstallPath { get; init; }
        public string? CoverPath { get; init; }
        public int HasCustomCover { get; init; }
        public int PlaytimeMinutes { get; init; }
        public string? LastPlayed { get; init; }
        public int IsFavorite { get; init; }
        public string LaunchKind { get; init; } = "";
        public string LaunchValue { get; init; } = "";

        public UnifiedGame ToUnifiedGame() => new()
        {
            Id = Id,
            Platform = (Platform)Platform,
            PlatformGameId = PlatformGameId,
            Title = Title,
            IsInstalled = IsInstalled == 1,
            InstallPath = InstallPath,
            CoverPath = CoverPath,
            HasCustomCover = HasCustomCover == 1,
            PlaytimeMinutes = PlaytimeMinutes,
            LastPlayed = LastPlayed is null ? null : DateTime.Parse(LastPlayed),
            IsFavorite = IsFavorite == 1,
            LaunchSpec = new LaunchSpec(LaunchKind, LaunchValue)
        };
    }

    public void Dispose() => _connection.Dispose();
}
