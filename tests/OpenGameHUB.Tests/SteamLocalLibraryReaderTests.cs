using OpenGameHUB.Providers.Steam;

namespace OpenGameHUB.Tests;

public sealed class SteamLocalLibraryReaderTests
{
    [Fact]
    public void ToAccountId_extracts_lower_dword_from_steam_id64()
    {
        Assert.Equal(83743485L, SteamLocalLibraryReader.ToAccountId("76561198044009213"));
    }

    [Fact]
    public void ParseLocalConfigAppsFromText_reads_playtime_and_last_played()
    {
        const string vdf = """
            "UserLocalConfigStore"
            {
              "apps"
              {
                "730" {
                  "Playtime"    "1200"
                  "LastPlayed"  "1700000000"
                }
              }
            }
            """;

        var apps = SteamLocalLibraryReader.ParseLocalConfigAppsFromText(vdf);

        Assert.Single(apps);
        Assert.Equal(730, apps[0].AppId);
        Assert.Equal(1200, apps[0].PlaytimeMinutes);
        Assert.NotNull(apps[0].LastPlayed);
    }
}
