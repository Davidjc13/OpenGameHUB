using Microsoft.Data.Sqlite;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure.Database;
using OpenGameHUB.Services.Games;

namespace OpenGameHUB.Tests;

public sealed class UserCollectionDatabaseTests : IDisposable
{
    private readonly string _dbPath;
    private readonly GameDatabase _database;

    public UserCollectionDatabaseTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"ogh-collections-{Guid.NewGuid():N}.db");
        _database = new GameDatabase(_dbPath);
    }

    [Fact]
    public void CreateCollection_persists_and_lists_collection()
    {
        var collection = _database.CreateCollection("Backlog");

        var collections = _database.GetUserCollections();

        Assert.Single(collections);
        Assert.Equal(collection.Id, collections[0].Id);
        Assert.Equal("Backlog", collections[0].Name);
    }

    [Fact]
    public void RenameCollection_updates_name()
    {
        var collection = _database.CreateCollection("Old Name");

        _database.RenameCollection(collection.Id, "New Name");

        Assert.Equal("New Name", _database.GetUserCollections()[0].Name);
    }

    [Fact]
    public void AddGameToCollection_tracks_membership()
    {
        InsertGame("steam:test", "Test Game");
        var collection = _database.CreateCollection("Favorites Custom");

        _database.AddGameToCollection(collection.Id, "steam:test");

        var index = _database.GetMembershipIndex();
        Assert.True(index.TryGetValue("steam:test", out var ids));
        Assert.Contains(collection.Id, ids);
        Assert.Contains("steam:test", _database.GetCollectionGameIds(collection.Id));
    }

    [Fact]
    public void DeleteCollection_cascades_memberships()
    {
        InsertGame("steam:test", "Test Game");
        var collection = _database.CreateCollection("Temp");
        _database.AddGameToCollection(collection.Id, "steam:test");

        _database.DeleteCollection(collection.Id);

        Assert.Empty(_database.GetUserCollections());
        Assert.Empty(_database.GetMembershipIndex());
    }

    [Fact]
    public void PurgeOrphanCollectionGames_removes_deleted_game_memberships()
    {
        InsertGame("steam:test", "Test Game");
        var collection = _database.CreateCollection("Temp");
        _database.AddGameToCollection(collection.Id, "steam:test");

        _database.SyncScannedGames(
        [
            new UnifiedGame
            {
                Id = "steam:other",
                Platform = Platform.Steam,
                PlatformGameId = "other",
                Title = "Other Game",
                IsInstalled = true,
                LaunchSpec = LaunchSpec.Executable("other.exe")
            }
        ]);

        Assert.Empty(_database.GetCollectionGameIds(collection.Id));
    }

    [Fact]
    public void SyncScannedGames_keeps_membership_for_games_not_in_the_library_yet()
    {
        var collection = _database.CreateCollection("Backlog");
        _database.AddGameToCollection(collection.Id, "steam:later");

        _database.SyncScannedGames(
        [
            new UnifiedGame
            {
                Id = "steam:other",
                Platform = Platform.Steam,
                PlatformGameId = "other",
                Title = "Other Game",
                IsInstalled = true,
                LaunchSpec = LaunchSpec.Executable("other.exe")
            }
        ]);

        Assert.Contains("steam:later", _database.GetCollectionGameIds(collection.Id));
    }

    [Fact]
    public void MergeImported_matches_by_name_and_skips_manual_games()
    {
        InsertGame("steam:1", "Game");
        var existing = _database.CreateCollection("RPG");
        _database.AddGameToCollection(existing.Id, "custom:path:local");
        _database.AddGameToCollection(existing.Id, "steam:1");

        var service = new UserCollectionService(_database);
        service.MergeImported(
        [
            new PortableCollection
            {
                Name = "rpg",
                SortOrder = 0,
                GameIds = ["steam:2", "custom:path:other"]
            },
            new PortableCollection
            {
                Name = "Backlog",
                SortOrder = 1,
                GameIds = ["steam:later"]
            }
        ]);

        var rpgIds = _database.GetCollectionGameIds(existing.Id);
        Assert.Contains("steam:1", rpgIds);
        Assert.Contains("steam:2", rpgIds);
        Assert.Contains("custom:path:local", rpgIds);
        Assert.DoesNotContain("custom:path:other", rpgIds);

        var backlog = Assert.Single(_database.GetUserCollections(), collection => collection.Name == "Backlog");
        Assert.Contains("steam:later", _database.GetCollectionGameIds(backlog.Id));
        Assert.Equal(2, _database.GetUserCollections().Count);
    }

    [Fact]
    public void SyncScannedGames_applies_a_favorite_saved_before_the_game_exists()
    {
        _database.SetFavorite("steam:later", true);

        _database.SyncScannedGames(
        [
            new UnifiedGame
            {
                Id = "steam:later",
                Platform = Platform.Steam,
                PlatformGameId = "later",
                Title = "Later",
                IsInstalled = true,
                LaunchSpec = LaunchSpec.Executable("later.exe")
            }
        ]);

        Assert.True(_database.GetAllGames().Single(game => game.Id == "steam:later").IsFavorite);
    }

    [Fact]
    public void SetFavorite_false_clears_a_remembered_favorite()
    {
        InsertGame("steam:1", "Game");
        _database.SetFavorite("steam:1", true);
        _database.SetFavorite("steam:1", false);

        _database.SyncScannedGames(
        [
            new UnifiedGame
            {
                Id = "steam:1",
                Platform = Platform.Steam,
                PlatformGameId = "1",
                Title = "Game",
                IsInstalled = true,
                LaunchSpec = LaunchSpec.Executable("game.exe")
            }
        ]);

        Assert.False(_database.GetAllGames().Single().IsFavorite);
        Assert.DoesNotContain("steam:1", _database.GetFavoriteGameIds());
    }

    public void Dispose()
    {
        _database.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private void InsertGame(string id, string title)
    {
        _database.UpsertGames(
        [
            new UnifiedGame
            {
                Id = id,
                Platform = Platform.Steam,
                PlatformGameId = id,
                Title = title,
                IsInstalled = true,
                LaunchSpec = LaunchSpec.Executable("game.exe")
            }
        ]);
    }
}
