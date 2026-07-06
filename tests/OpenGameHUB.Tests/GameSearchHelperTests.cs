using OpenGameHUB.Services.Games;

namespace OpenGameHUB.Tests;

public sealed class GameSearchHelperTests
{
    [Theory]
    [InlineData("Counter-Strike 2", "counter", true)]
    [InlineData("Counter-Strike 2", "cs2", false)]
    [InlineData("Counter-Strike 2", "counter strike", true)]
    [InlineData("Grand Theft Auto V", "grand theft", true)]
    [InlineData("Café Simulator", "cafe", true)]
    [InlineData("Some Game", "", true)]
    [InlineData("", "query", false)]
    public void MatchesTitle_handles_queries(string title, string query, bool expected)
    {
        Assert.Equal(expected, GameSearchHelper.MatchesTitle(title, query));
    }

    [Fact]
    public void MatchesTitle_matches_compact_form_without_spaces()
    {
        Assert.True(GameSearchHelper.MatchesTitle("DOOM Eternal", "doometernal"));
    }
}
