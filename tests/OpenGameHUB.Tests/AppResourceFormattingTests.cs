using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.ViewModels;
using OpenGameHUB.ViewModels.MainWindow;

namespace OpenGameHUB.Tests;

public sealed class AppResourceFormattingTests
{
    [Theory]
    [InlineData(0, "0.0")]
    [InlineData(1048576, "1.0")]
    [InlineData(5242880, "5.0")]
    public void FormatMegabytes_uses_one_decimal(long bytes, string expected) =>
        Assert.Equal(expected, AppResourceFormatting.FormatMegabytes(bytes));

    [Fact]
    public void Capture_returns_non_negative_values()
    {
        var snapshot = AppResourceMonitor.Capture();

        Assert.True(snapshot.WorkingSetBytes > 0);
        Assert.True(snapshot.PrivateMemoryBytes > 0);
        Assert.True(snapshot.ManagedHeapBytes >= 0);
        Assert.True(snapshot.ProcessThreadCount > 0);
        Assert.True(snapshot.ThreadPoolThreadCount >= 0);
        Assert.True(snapshot.Gen0Collections >= 0);
    }

    [Fact]
    public void CountLoadedCovers_counts_has_cover_items()
    {
        var games = new[]
        {
            CreateGame("a"),
            CreateGame("b"),
            CreateGame("c")
        };

        games[0].HasCover = true;
        games[2].HasCover = true;

        Assert.Equal(2, MainWindowDevMetricsViewModel.CountLoadedCovers(games));
    }

    private static GameItemViewModel CreateGame(string id) =>
        new(new UnifiedGame
        {
            Id = id,
            Title = id,
            Platform = Platform.Steam,
            PlatformGameId = id,
            LaunchSpec = LaunchSpec.Executable("game.exe")
        });
}
