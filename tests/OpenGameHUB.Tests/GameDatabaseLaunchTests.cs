using Microsoft.Data.Sqlite;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure.Database;

namespace OpenGameHUB.Tests;

public sealed class GameDatabaseLaunchTests : IDisposable
{
    private readonly string _dbPath;
    private readonly GameDatabase _database;

    public GameDatabaseLaunchTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"ogh-launch-{Guid.NewGuid():N}.db");
        _database = new GameDatabase(_dbPath);
    }

    [Fact]
    public void RecordLauncherLaunch_persists_timestamp()
    {
        InsertGame("steam:test", "Test Game");

        var launchedAt = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        _database.RecordLauncherLaunch("steam:test", launchedAt);

        var game = _database.GetAllGames().Single();
        Assert.Equal(launchedAt, game.LastPlayed?.ToUniversalTime());
    }

    [Fact]
    public void SyncScannedGames_preserves_launcher_last_played()
    {
        var launcherLastPlayed = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        InsertGame("steam:test", "Test Game", launcherLastPlayed);

        _database.SyncScannedGames(
        [
            new UnifiedGame
            {
                Id = "steam:test",
                Platform = Platform.Steam,
                PlatformGameId = "test",
                Title = "Test Game",
                IsInstalled = true,
                LastPlayed = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LaunchSpec = LaunchSpec.Executable("game.exe")
            }
        ]);

        var game = _database.GetAllGames().Single();
        Assert.Equal(launcherLastPlayed, game.LastPlayed?.ToUniversalTime());
    }

    [Fact]
    public void PersistPlaytimes_updates_playtime_without_touching_last_played()
    {
        var launcherLastPlayed = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        InsertGame("steam:test", "Test Game", launcherLastPlayed, playtimeMinutes: 10);

        _database.PersistPlaytimes(
        [
            new UnifiedGame
            {
                Id = "steam:test",
                Platform = Platform.Steam,
                PlatformGameId = "test",
                Title = "Test Game",
                IsInstalled = true,
                PlaytimeMinutes = 99,
                LastPlayed = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LaunchSpec = LaunchSpec.Executable("game.exe")
            }
        ]);

        var game = _database.GetAllGames().Single();
        Assert.Equal(99, game.PlaytimeMinutes);
        Assert.Equal(launcherLastPlayed, game.LastPlayed?.ToUniversalTime());
    }

    public void Dispose()
    {
        _database.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private void InsertGame(
        string id,
        string title,
        DateTime? lastPlayed = null,
        int playtimeMinutes = 0)
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
                PlaytimeMinutes = playtimeMinutes,
                LastPlayed = lastPlayed,
                LaunchSpec = LaunchSpec.Executable("game.exe")
            }
        ]);
    }
}
