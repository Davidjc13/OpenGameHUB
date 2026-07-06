using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Tests;

public sealed class SettingsSecretsTests
{
    [Fact]
    public void From_and_ApplyTo_round_trip_secrets()
    {
        var settings = new AppSettings
        {
            SteamApiKey = "steam",
            IgdbClientSecret = "igdb",
            SteamGridDbApiKey = "sgdb"
        };

        var secrets = SettingsSecrets.From(settings);
        var target = new AppSettings();

        secrets.ApplyTo(target);

        Assert.Equal("steam", target.SteamApiKey);
        Assert.Equal("igdb", target.IgdbClientSecret);
        Assert.Equal("sgdb", target.SteamGridDbApiKey);
    }
}
