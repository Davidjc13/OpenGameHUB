using System.Text.Json;
using OpenGameHUB.Providers.BattleNet;

namespace OpenGameHUB.Tests;

public sealed class BattleNetAccountJsonTests
{
    [Fact]
    public void GameAccountDocument_reads_numeric_titleId()
    {
        const string json = """
            {
              "gameAccounts": [
                { "titleId": 5730135, "localizedGameName": "World of Warcraft" }
              ]
            }
            """;

        var document = JsonSerializer.Deserialize<BattleNetGamesAndSubsDocument>(json, BattleNetAccountJson.Options);
        Assert.NotNull(document);
        Assert.Single(document!.GameAccounts);
        Assert.Equal("5730135", document.GameAccounts[0].GetTitleIdKey());
    }
}
