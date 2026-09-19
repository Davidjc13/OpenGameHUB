using Microsoft.Data.Sqlite;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure.Database;

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
