using Microsoft.Data.Sqlite;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure.Database;
using OpenGameHUB.Services.Games;

namespace OpenGameHUB.Tests;

public sealed class CustomGameServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _gameDir;
    private readonly GameDatabase _database;
    private readonly CustomGameService _service;

    public CustomGameServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"ogh-custom-{Guid.NewGuid():N}.db");
        _gameDir = Path.Combine(Path.GetTempPath(), $"ogh-custom-game-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_gameDir);
        _database = new GameDatabase(_dbPath);
        _service = new CustomGameService(_database);
    }

    [Fact]
    public void Add_persists_custom_game_and_Remove_deletes_it()
    {
        var exe = Path.Combine(_gameDir, "indie.exe");
        File.WriteAllBytes(exe, [0]);

        var added = _service.Add("Indie Title", exe);

        Assert.StartsWith("custom:path:", added.Id, StringComparison.Ordinal);
        Assert.Equal(Platform.Custom, added.Platform);
        Assert.True(_service.Exists(exe));
        Assert.Single(_service.LoadAll());

        var collection = _database.CreateCollection("Backlog");
        _database.AddGameToCollection(collection.Id, added.Id);

        Assert.True(_service.Remove(added.Id));
        Assert.Empty(_service.LoadAll());
        Assert.False(_service.Exists(exe));
        Assert.Empty(_database.GetCollectionGameIds(collection.Id));
    }

    [Fact]
    public void Remove_rejects_store_games()
    {
        _database.UpsertGames(
        [
            new UnifiedGame
            {
                Id = "steam:store:570",
                Platform = Platform.Steam,
                PlatformGameId = "570",
                Title = "Dota",
                IsInstalled = true,
                LaunchSpec = LaunchSpec.Protocol("steam://install/570")
            }
        ]);

        Assert.False(_service.Remove("steam:store:570"));
        Assert.Single(_database.GetAllGames());
    }

    public void Dispose()
    {
        _database.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
        if (Directory.Exists(_gameDir))
            Directory.Delete(_gameDir, recursive: true);
    }
}
