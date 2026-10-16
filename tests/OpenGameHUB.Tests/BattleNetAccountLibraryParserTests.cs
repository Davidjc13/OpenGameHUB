using OpenGameHUB.Providers.BattleNet;

namespace OpenGameHUB.Tests;

public sealed class BattleNetAccountLibraryParserTests
{
    [Fact]
    public void ParseOwnedGames_reads_numeric_title_id()
    {
        const string json = """{"gameAccounts":[{"titleId":5272175,"localizedGameName":"Overwatch 2"}]}""";

        var games = BattleNetAccountLibraryParser.ParseOwnedGames(json);

        Assert.Single(games);
        Assert.Equal("5272175", games[0].TitleId);
        Assert.Equal("Overwatch 2", games[0].LocalizedName);
    }

    [Fact]
    public void ParseOwnedGames_reads_root_array()
    {
        const string json = """[{"id":"22323","name":"Warcraft III: Reforged"}]""";

        var games = BattleNetAccountLibraryParser.ParseOwnedGames(json);

        Assert.Single(games);
        Assert.Equal("22323", games[0].TitleId);
        Assert.Equal("Warcraft III: Reforged", games[0].LocalizedName);
    }
}
